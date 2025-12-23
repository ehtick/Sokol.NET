using System;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;

public abstract class SampleBase
{
	protected SKMatrix Matrix = SKMatrix.Identity;

	private SKMatrix startPanMatrix = SKMatrix.Identity;
	private SKMatrix startPinchMatrix = SKMatrix.Identity;
	private SKPoint startPinchOrigin = SKPoint.Empty;
	private float totalPinchScale = 1f;

	public abstract string Title { get; }

	public virtual string Description { get; } = string.Empty;

	public virtual SamplePlatforms SupportedPlatform { get; } = SamplePlatforms.All;


	public virtual SampleCategories Category { get; } = SampleCategories.General;

	public bool IsInitialized { get; private set; } = false;

 //On Web , every Sample is drawn once by default to avoid unnecessary redraws
#if WEB
	public bool IsDrawOnce { get;  set; } = true;
#else
	public bool IsDrawOnce { get;  set; } = false;
#endif
	public bool IsDrawn { get; private set; } = false;

	public virtual void DrawSample(SKCanvas canvas, int width, int height)
	{
		if (IsInitialized)
		{
#if WEB
			if(IsDrawOnce && IsDrawn)
				return;
			IsDrawn = true;
#endif
			canvas.SetMatrix(Matrix);
			OnDrawSample(canvas, width, height);
		}
	}

	protected abstract void OnDrawSample(SKCanvas canvas, int width, int height);

	public async void Init()
	{
		// reset the matrix for the new sample
		Matrix = SKMatrix.Identity;

		if (!IsInitialized)
		{
			await OnInit();

			IsInitialized = true;
			IsDrawn = false;

			Refresh();
		}
	}

	public void Destroy()
	{
		if (IsInitialized)
		{
			OnDestroy();

			IsInitialized = false;
		}
	}

	protected virtual Task OnInit()
	{
		return Task.FromResult(true);
	}

	protected virtual void OnDestroy()
	{
	}

	public void Tap()
	{
		if (IsInitialized)
		{
			OnTapped();
		}
	}

	protected virtual void OnTapped()
	{
	}

	public void Pan(GestureState state, SKPoint translation)
	{
		switch (state)
		{
			case GestureState.Started:
				startPanMatrix = Matrix;
				break;
			case GestureState.Running:
				var canvasTranslation = SKMatrix.CreateTranslation(translation.X, translation.Y);
				SKMatrix.Concat(ref Matrix, canvasTranslation, startPanMatrix);
				break;
			default:
				startPanMatrix = SKMatrix.Identity;
				break;
		}
	}

	public void Pinch(GestureState state, float scale, SKPoint origin)
	{
		switch (state)
		{
			case GestureState.Started:
				startPinchMatrix = Matrix;
				startPinchOrigin = origin;
				totalPinchScale = 1f;
				break;
			case GestureState.Running:
				totalPinchScale *= scale;
				var pinchTranslation = origin - startPinchOrigin;
				var canvasTranslation = SKMatrix.CreateTranslation(pinchTranslation.X, pinchTranslation.Y);
				var canvasScaling = SKMatrix.CreateScale(totalPinchScale, totalPinchScale, origin.X, origin.Y);
				var canvasCombined = SKMatrix.Identity;
				SKMatrix.Concat(ref canvasCombined, canvasScaling, canvasTranslation);
				SKMatrix.Concat(ref Matrix, canvasCombined, startPinchMatrix);
				break;
			default:
				startPinchMatrix = SKMatrix.Identity;
				startPinchOrigin = SKPoint.Empty;
				totalPinchScale = 1f;
				break;
		}
	}

	public virtual bool MatchesFilter(string searchText)
	{
		if (string.IsNullOrWhiteSpace(searchText))
			return true;

		return
			Title.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) != -1 ||
			Description.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) != -1;
	}

	public event EventHandler? RefreshRequested;

	protected void Refresh()
	{
		IsDrawn = false;
		RefreshRequested?.Invoke(this, EventArgs.Empty);
	}
}

public abstract class AnimatedSampleBase : SampleBase
{
	private CancellationTokenSource? cts;

	public AnimatedSampleBase()
	{
	}

	protected override async Task OnInit()
	{
		await base.OnInit();

#if !WEB
		cts = new CancellationTokenSource();
		var loop = Task.Run(async () =>
		{
			while (!cts.IsCancellationRequested)
			{
				try
				{
					await OnUpdate(cts.Token);
					Refresh();
				}
				catch (OperationCanceledException)
				{
					// Expected when cancellation is requested
					break;
				}
			}
		}, cts.Token);
#endif
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();

		cts?.Cancel();
	}

#if WEB
	public override void DrawSample(SKCanvas canvas, int width, int height)
	{
		// On Web, update synchronously before drawing (fire and forget)
		_ = OnUpdate(CancellationToken.None);
		base.DrawSample(canvas, width, height);
	}
#endif

	protected abstract Task OnUpdate(CancellationToken token);
}

public enum GestureState
{
	Started,
	Running,
	Completed,
	Canceled
}

