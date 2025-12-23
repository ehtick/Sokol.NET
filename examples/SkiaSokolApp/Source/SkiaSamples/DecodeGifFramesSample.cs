using System;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using static Sokol.SApp;

public class DecodeGifFramesSample : AnimatedSampleBase
{
	private int currentFrame = 0;
	private SKCodec codec = null;
	private SKImageInfo info = SKImageInfo.Empty;
	private SKBitmap bitmap = null;
	private SKCodecFrameInfo[] frames;

	double accumulatedTime = 0;

	public DecodeGifFramesSample()
	{
	}

	public override string Title => "Decode Gif Frames";

	public override SampleCategories Category => SampleCategories.BitmapDecoding;

	protected override async Task OnInit()
	{
		var stream = new SKManagedStream(SampleMedia.Images.AnimatedHeartGif, true);
		codec = SKCodec.Create(stream);
		frames = codec.FrameInfo;

		info = codec.Info;
		info = new SKImageInfo(info.Width, info.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);

		bitmap = new SKBitmap(info);

		await base.OnInit();
	}

	protected override async Task OnUpdate(CancellationToken token)
	{
		var duration = frames[currentFrame].Duration;
		if (duration <= 0)
			duration = 100;

#if !WEB
		await Task.Delay(duration, token);
		// next frame
		currentFrame++;
		if (currentFrame >= frames.Length)
			currentFrame = 0;
#else
		accumulatedTime += sapp_frame_duration() * 1000; // Convert seconds to milliseconds
		if (accumulatedTime >= duration)
		{
			currentFrame++;
			accumulatedTime = 0;
		}
		if (currentFrame >= frames.Length)
			currentFrame = 0;
		await Task.CompletedTask;
#endif
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();

		codec?.Dispose();
		codec = null;
	}

	protected override void OnDrawSample(SKCanvas canvas, int width, int height)
	{
		canvas.Clear(SKColors.Black);
		canvas.Scale(2, 2);

		var opts = new SKCodecOptions(currentFrame);
		if (codec?.GetPixels(info, bitmap.GetPixels(), opts) == SKCodecResult.Success)
		{
			bitmap.NotifyPixelsChanged();
			canvas.DrawBitmap(bitmap, 0, 0);
		}
	}
}

