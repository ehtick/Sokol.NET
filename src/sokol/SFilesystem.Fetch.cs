
using System.Runtime.InteropServices;
using static Sokol.SFetch;
using static Sokol.SLog;
using static Sokol.Utils;

namespace Sokol
{
    public enum SFileLoadStatus
    {
        Success,
        Failed,
        NotFound,
        BufferTooSmall,
        Cancelled
    }

    public delegate void SFileLoadCallback(string filePath, byte[]? buffer, SFileLoadStatus status);

    internal struct SFileLoadRequest
    {
        public string FilePath;
        public SFileLoadCallback Callback;
        public SharedBuffer Buffer;
        public sfetch_handle_t Handle;
        public bool IsActive;
        public uint OriginalBufferSize;
        public bool IsRetry;
        public int RetryCount;
    }

    public static unsafe partial class SFilesystem
    {
        private static readonly object _fetchLock = new object();

        private const int MAX_CONCURRENT_REQUESTS = 64;
        // sfetch request-pool SLOTS (small per-request structs, NOT the 1 MB buffers). Decoupled from —
        // and much larger than — MAX_CONCURRENT_REQUESTS so a burst of queued loads (e.g. a scene with a
        // dozen glTFs; FishAndShark alone fetches 23 images) doesn't exhaust the pool. The C# layer still
        // caps ACTIVE requests + the 1 MB buffer pool at MAX_CONCURRENT_REQUESTS, so memory is unchanged.
        private const int SFETCH_MAX_REQUESTS = 1024;
        private const int NUM_CHANNELS = 2;
        private const int NUM_LANES = 4;
        private const int DEFAULT_BUFFER_SIZE = 1024 * 1024;
        private const int MAX_RETRY_ATTEMPTS = 4;
        private const int RETRY_BUFFER_MULTIPLIER = 4;

        private static readonly Queue<SFileLoadRequest> _pendingRequests = new Queue<SFileLoadRequest>();
        private static readonly Dictionary<uint, SFileLoadRequest> _activeRequests = new Dictionary<uint, SFileLoadRequest>();
        private static readonly List<SharedBuffer> _bufferPool = new List<SharedBuffer>();

        private static bool _fetchInitialized = false;

        public static void Initialize()
        {
            if (_fetchInitialized)
                return;

            Info("FileSystem: Initializing sokol-fetch...");

            sfetch_setup(new sfetch_desc_t()
            {
                max_requests = SFETCH_MAX_REQUESTS,
                num_channels = NUM_CHANNELS,
                num_lanes = NUM_LANES,
                logger = {
                    func = &slog_func,
                }
            });

            for (int i = 0; i < MAX_CONCURRENT_REQUESTS; i++)
            {
                _bufferPool.Add(SharedBuffer.Create(DEFAULT_BUFFER_SIZE));
            }

            _fetchInitialized = true;
            Info($"FileSystem: Initialized with {MAX_CONCURRENT_REQUESTS} max requests, {NUM_CHANNELS} channels, {NUM_LANES} lanes");
        }

        public static void Shutdown()
        {
            if (!_fetchInitialized)
                return;

            Info("FileSystem: Shutting down...");

            foreach (var request in _activeRequests.Values)
            {
                sfetch_cancel(request.Handle);
                request.Buffer.Dispose();
            }
            _activeRequests.Clear();

            while (_pendingRequests.Count > 0)
            {
                var request = _pendingRequests.Dequeue();
                request.Buffer.Dispose();
            }

            foreach (var buffer in _bufferPool)
            {
                buffer.Dispose();
            }
            _bufferPool.Clear();

            sfetch_shutdown();
            _fetchInitialized = false;
        }

        public static void LoadFileAsync(string filePath, SFileLoadCallback callback, uint bufferSize = DEFAULT_BUFFER_SIZE)
        {
            if (!_fetchInitialized)
            {
                throw new InvalidOperationException("FileSystem must be initialized before use. Call Initialize() first.");
            }

            if (string.IsNullOrEmpty(filePath))
            {
                callback?.Invoke(filePath ?? "", null, SFileLoadStatus.Failed);
                return;
            }

            if (callback == null)
            {
                Info($"FileSystem: Warning - No callback provided for file: {filePath}");
                return;
            }

            var buffer = bufferSize == DEFAULT_BUFFER_SIZE && _bufferPool.Count > 0
                ? GetPooledBuffer()
                : SharedBuffer.Create(bufferSize);

            var request = new SFileLoadRequest
            {
                FilePath = filePath,
                Callback = callback,
                Buffer = buffer,
                IsActive = false,
                OriginalBufferSize = bufferSize,
                IsRetry = false,
                RetryCount = 0
            };

            lock (_fetchLock)
            {
                _pendingRequests.Enqueue(request);
                Info($"FileSystem: Queued file load request: {filePath} (Queue size: {_pendingRequests.Count})");
            }

            ProcessPendingRequests();
        }

        public static (byte[]? data, SFileLoadStatus status) LoadFileSync(string filePath, uint bufferSize = DEFAULT_BUFFER_SIZE, int timeoutMs = 10000)
        {
            if (!_fetchInitialized)
            {
                throw new InvalidOperationException("FileSystem must be initialized before use. Call Initialize() first.");
            }

            if (string.IsNullOrEmpty(filePath))
            {
                return (null, SFileLoadStatus.Failed);
            }

            byte[]? resultData = null;
            SFileLoadStatus resultStatus = SFileLoadStatus.Failed;
            bool completed = false;

            SFileLoadCallback syncCallback = (path, data, status) =>
            {
                resultData = data;
                resultStatus = status;
                completed = true;
            };

            LoadFileAsync(filePath, syncCallback, bufferSize);

            var startTime = System.Diagnostics.Stopwatch.StartNew();
            while (!completed && startTime.ElapsedMilliseconds < timeoutMs)
            {
                Update();
                System.Threading.Thread.Sleep(1);
            }

            if (!completed)
            {
                Info($"FileSystem: Sync load timeout for {filePath} after {timeoutMs}ms");
                return (null, SFileLoadStatus.Failed);
            }

            return (resultData, resultStatus);
        }

        public static void Update()
        {
            if (!_fetchInitialized)
                return;

            sfetch_dowork();
            ProcessPendingRequests();
        }

        public static int PendingRequestCount
        {
            get
            {
                lock (_fetchLock)
                {
                    return _pendingRequests.Count;
                }
            }
        }

        public static int ActiveRequestCount
        {
            get
            {
                lock (_fetchLock)
                {
                    return _activeRequests.Count;
                }
            }
        }

        private static SharedBuffer GetPooledBuffer()
        {
            if (_bufferPool.Count > 0)
            {
                var buffer = _bufferPool[_bufferPool.Count - 1];
                _bufferPool.RemoveAt(_bufferPool.Count - 1);
                return buffer;
            }
            return SharedBuffer.Create(DEFAULT_BUFFER_SIZE);
        }

        private static void ReturnPooledBuffer(SharedBuffer buffer)
        {
            if (buffer.Size == (uint)DEFAULT_BUFFER_SIZE && _bufferPool.Count < MAX_CONCURRENT_REQUESTS)
            {
                _bufferPool.Add(buffer);
            }
            else
            {
                buffer.Dispose();
            }
        }

        private static void ProcessPendingRequests()
        {
            lock (_fetchLock)
            {
                while (_activeRequests.Count < MAX_CONCURRENT_REQUESTS && _pendingRequests.Count > 0)
                {
                    var request = _pendingRequests.Dequeue();
                    StartRequest(request);
                }
            }
        }

        private static void StartRequest(SFileLoadRequest request)
        {
            string platformPath = util_get_file_path(request.FilePath);
            Info($"FileSystem: Starting file load: {request.FilePath} -> {platformPath}");

            uint channel = (uint)(_activeRequests.Count % NUM_CHANNELS);

            var sfetchRequest = new sfetch_request_t
            {
                path = platformPath,
                channel = channel,
                callback = &FetchCallback_Internal,
                buffer = SFETCH_RANGE(request.Buffer)
            };

            var handle = sfetch_send(sfetchRequest);

            if (sfetch_handle_valid(handle))
            {
                request.Handle = handle;
                request.IsActive = true;
                _activeRequests[handle.id] = request;
                Info($"FileSystem: Started request for {request.FilePath} (handle: {handle.id}, channel: {channel})");
            }
            else
            {
                Info($"FileSystem: Pool exhausted for {request.FilePath} — re-queuing for next frame");
                var currentRequests = new List<SFileLoadRequest>();
                while (_pendingRequests.Count > 0)
                    currentRequests.Add(_pendingRequests.Dequeue());
                _pendingRequests.Enqueue(request);
                foreach (var r in currentRequests)
                    _pendingRequests.Enqueue(r);
            }
        }

        [UnmanagedCallersOnly]
        private static void FetchCallback_Internal(sfetch_response_t* response)
        {
            HandleFileLoadResponse(response);
        }

        private static void HandleFileLoadResponse(sfetch_response_t* response)
        {
            if (!_activeRequests.TryGetValue(response->handle.id, out var request))
            {
                Info($"FileSystem: Received response for unknown handle: {response->handle.id}");
                return;
            }

            if (response->finished)
            {
                SFileLoadStatus status;
                byte[]? buffer = null;

                if (response->failed)
                {
                    if (response->error_code == sfetch_error_t.SFETCH_ERROR_BUFFER_TOO_SMALL &&
                        request.RetryCount < MAX_RETRY_ATTEMPTS)
                    {
                        RetryWithLargerBuffer(request);
                        return;
                    }

                    status = response->error_code switch
                    {
                        sfetch_error_t.SFETCH_ERROR_FILE_NOT_FOUND => SFileLoadStatus.NotFound,
                        sfetch_error_t.SFETCH_ERROR_BUFFER_TOO_SMALL => SFileLoadStatus.BufferTooSmall,
                        sfetch_error_t.SFETCH_ERROR_CANCELLED => SFileLoadStatus.Cancelled,
                        _ => SFileLoadStatus.Failed
                    };
                    Info($"FileSystem: Failed to load {request.FilePath}: {response->error_code}");
                }
                else
                {
                    status = SFileLoadStatus.Success;
                    buffer = new byte[response->data.size];
                    Marshal.Copy((IntPtr)response->data.ptr, buffer, 0, (int)response->data.size);
                    Info($"FileSystem: Successfully loaded {request.FilePath} ({response->data.size} bytes)");
                }

                lock (_fetchLock)
                {
                    _activeRequests.Remove(response->handle.id);
                }

                try
                {
                    request.Callback?.Invoke(request.FilePath, buffer, status);
                }
                catch (Exception ex)
                {
                    Info($"FileSystem: Exception in callback for {request.FilePath}: {ex.Message}");
                }

                ReturnPooledBuffer(request.Buffer);
                ProcessPendingRequests();
            }
            else if (response->fetched)
            {
                Info($"FileSystem: Fetching {request.FilePath} - {response->data.size} bytes received");
            }
        }

        private static void RetryWithLargerBuffer(SFileLoadRequest originalRequest)
        {
            uint newBufferSize = (uint)(originalRequest.Buffer.Size * RETRY_BUFFER_MULTIPLIER);

            Info($"FileSystem: Retrying {originalRequest.FilePath} with larger buffer ({newBufferSize} bytes, attempt {originalRequest.RetryCount + 1})");

            var retryRequest = new SFileLoadRequest
            {
                FilePath = originalRequest.FilePath,
                Callback = originalRequest.Callback,
                Buffer = SharedBuffer.Create(newBufferSize),
                Handle = default,
                IsActive = false,
                OriginalBufferSize = originalRequest.OriginalBufferSize,
                IsRetry = true,
                RetryCount = originalRequest.RetryCount + 1
            };

            lock (_fetchLock)
            {
                _activeRequests.Remove(originalRequest.Handle.id);
            }

            ReturnPooledBuffer(originalRequest.Buffer);

            lock (_fetchLock)
            {
                var currentRequests = new List<SFileLoadRequest>();
                while (_pendingRequests.Count > 0)
                {
                    currentRequests.Add(_pendingRequests.Dequeue());
                }

                _pendingRequests.Enqueue(retryRequest);

                foreach (var req in currentRequests)
                {
                    _pendingRequests.Enqueue(req);
                }
            }

            ProcessPendingRequests();
        }
    }
}
