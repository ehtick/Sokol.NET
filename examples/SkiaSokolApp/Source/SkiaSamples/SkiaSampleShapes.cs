using System;
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static Sokol.SApp;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.SG.sg_vertex_format;
using static Sokol.SG.sg_index_type;
using static Sokol.SG.sg_cull_mode;
using static Sokol.SG.sg_compare_func;
using static Sokol.Utils;
using System.Diagnostics;
using static Sokol.SLog;
using static System.Numerics.Matrix4x4;
using static Sokol.SDebugUI;
using static skia_shader_cs.Shaders;
using SkiaSharp;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

public class SkiaSampleShapes : SampleBase
{
    private float timeAccumulator;

    public override string Title => "Simple Shapes";
    public override string Description => "Animated shapes with sin wave and circular motion";

    public SkiaSampleShapes()
    {

    }

    protected override void OnDrawSample(SKCanvas canvas, int width, int height)
    {
        timeAccumulator += (float)(sapp_frame_duration()); // Approximate frame time

        // Colours
        var red = new SKColor(255, 0, 0);
        var green = new SKColor(0, 255, 0);
        var blue = new SKColor(0, 0, 255);

        // Dark blue background
        canvas.DrawRect(new SKRect(0, 0, width, height), new SKPaint()
        {
            Color = new SKColor(10, 20, 70),
            IsStroke = false,
            IsAntialias = false,
        });

        // Blue rectangle - animated with rotation and size fluctuation
        float rectSize = 350 + MathF.Sin(timeAccumulator * 2f) * 50; // Fluctuates between 300-400
        float rectCenterX = 250;
        float rectCenterY = 250;
        float rotation = timeAccumulator * 30f; // Rotate over time

        canvas.Save();
        canvas.Translate(rectCenterX, rectCenterY);
        canvas.RotateDegrees(rotation);
        canvas.DrawRect(new SKRect(-rectSize/2, -rectSize/2, rectSize/2, rectSize/2), new SKPaint()
        {
            Color = blue,
            IsStroke = false,
            IsAntialias = false,
        });
        canvas.Restore();

        // Red sin wave - optimized with DrawPoints
        var sinPoints = new SKPoint[width];
        for (int x = 0; x < width; x++)
        {
            var sin = MathF.Sin((x / (float)width * MathF.PI * 2) + timeAccumulator * 2f);
            var y = (sin + 1) / 2 * height;
            sinPoints[x] = new SKPoint(x, y);
        }
        canvas.DrawPoints(SKPointMode.Points, sinPoints, new SKPaint()
        {
            Color = red,
            StrokeWidth = 5,
            IsAntialias = false
        });

        // Animated circle - position moves in circular motion, size resonates 60-80
        float centerX = 400 + MathF.Cos(timeAccumulator) * 250;  // Circular motion within bounds
        float centerY = 400 + MathF.Sin(timeAccumulator) * 250;
        float radius = 70 + MathF.Sin(timeAccumulator * 3f) * 10;  // Resonates between 60-80

        canvas.DrawCircle(new SKPoint(centerX, centerY), radius, new SKPaint()
        {
            Color = green,
            IsStroke = true,
            IsAntialias = true,
            StrokeWidth = 8
        });
    }

    protected override Task OnInit()
    {
        timeAccumulator = 0;
        return base.OnInit();
    }
}