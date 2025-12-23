using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using SkiaSharp.Skottie;


	public class SkottieSample : AnimatedSampleBase
	{
		private Animation? _animation;
		private Stopwatch _watch = new Stopwatch();
		private bool _isSupported = true;

		public SkottieSample()
		{
		}

		public override string Title => "Skottie";

		public override SampleCategories Category => SampleCategories.General;

		protected override async Task OnInit()
		{
		var stream = SampleMedia.Images.LottieLogo;
		if (stream == null)
		{
			Console.WriteLine("Failed to load LottieLogo1.json from embedded resources");
			_isSupported = false;
			await base.OnInit();
			return;
		}

		try
		{
			if (Animation.TryCreate(stream, out _animation))
			{
				_animation.Seek(0, null);
				_watch.Start();
			}
			else
			{
				Console.WriteLine("Failed to create Animation from LottieLogo stream");
				_isSupported = false;
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error initializing Skottie animation: {ex.Message}");
			_isSupported = false;
		}

		await base.OnInit();
	}

	protected override async Task OnUpdate(CancellationToken token)
	{
		if (!_isSupported || _animation == null)
			return;

#if !WEB
		await Task.Delay(25, token);
#endif

		_animation.SeekFrameTime(_watch.Elapsed);

		if (_watch.Elapsed > _animation.Duration)
			_watch.Restart();

#if WEB
		await Task.CompletedTask;
#endif
	}

	protected override void OnDrawSample(SKCanvas canvas, int width, int height)
	{
		canvas.Clear(SKColors.White);

		if (!_isSupported)
		{
			// Draw "not supported" message
			using var paint = new SKPaint
			{
				Color = SKColors.Black,
				IsAntialias = true
			};
			using var font = new SKFont
			{
				Size = 24
			};
			
			canvas.DrawText("Skottie animation failed to load", 
				width / 2, height / 2, SKTextAlign.Center, font, paint);
			return;
		}

		if (_animation == null)
			return;

		_animation.Render(canvas, new SKRect(0, 0, width, height));
	}
}
