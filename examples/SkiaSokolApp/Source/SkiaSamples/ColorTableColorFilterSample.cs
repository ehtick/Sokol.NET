using SkiaSharp;


public class ColorTableColorFilterSample : SampleBase
{
	public ColorTableColorFilterSample()
	{
	}

	public override string Title => "Color Table Color Filter";

	public override SampleCategories Category => SampleCategories.ColorFilters;

	protected override void OnDrawSample(SKCanvas canvas, int width, int height)
	{
		canvas.Clear(SKColors.White);

		var ct = new byte[256];
		for (var i = 0; i < 256; ++i)
		{
			var x = (i - 96) * 255 / 64;
			ct[i] = x < 0 ? (byte)0 : x > 255 ? (byte)255 : (byte)x;
		}

		// Create identity table for alpha channel (no change)
		var alphaTable = new byte[256];
		for (var i = 0; i < 256; ++i)
		{
			alphaTable[i] = (byte)i;
		}

		// load the image from the embedded resource stream
		using (var stream = new SKManagedStream(SampleMedia.Images.Baboon))
		using (var bitmap = SKBitmap.Decode(stream))
		using (var cf = SKColorFilter.CreateTable(alphaTable, ct, ct, ct))
		using (var paint = new SKPaint())
		{
			paint.ColorFilter = cf;

			canvas.DrawBitmap(bitmap, SKRect.Create(width, height), paint);
		}
	}
}
