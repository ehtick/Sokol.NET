using System;

using SkiaSharp;


public class DrawVerticesSample : SampleBase
{

	public DrawVerticesSample()
	{
	}

	public override string Title => "Draw Vertices";

	public override SampleCategories Category => SampleCategories.General;

	protected override void OnDrawSample(SKCanvas canvas, int width, int height)
	{
		canvas.Clear(SKColors.White);
		
		var paint = new SKPaint
		{
			IsAntialias = true,
			Color = SKColors.White // Set to white to allow vertex colors to show
		};

		float centerX = width / 2f;
		float centerY = height / 2f;
		float triangleWidth = 300;
		float triangleHeight = 360;

		var vertices = new[] { 
			new SKPoint(centerX, centerY - triangleHeight / 2), // Top
			new SKPoint(centerX + triangleWidth / 2, centerY + triangleHeight / 2), // Bottom right
			new SKPoint(centerX - triangleWidth / 2, centerY + triangleHeight / 2) // Bottom left
		};
		var colors = new[] { SKColors.Red, SKColors.Green, SKColors.Blue };

		using var skVertices = SKVertices.CreateCopy(SKVertexMode.Triangles, vertices, colors);
		canvas.DrawVertices(skVertices, SKBlendMode.Modulate, paint);
	}
}

