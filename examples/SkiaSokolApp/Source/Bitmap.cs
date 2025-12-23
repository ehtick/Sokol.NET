using SkiaSharp;
using static Sokol.SG;

namespace Sokol
{
    public class Bitmap
    {
        SKBitmap bitmap;
        SKSurface surface;
        public SKCanvas canvas;

        public Texture SokolTexture { get; private set; }

        public int Width { get; private set; }
        public int Height { get; private set; }

        sg_image_data image_data = default;

        public unsafe Bitmap(int width, int height)
        {
            this.Width = width;
            this.Height = height;

            bitmap = new(width, height);
            SokolTexture = new Texture(width, height);
            image_data.mip_levels[0] = new sg_range() { ptr = (void*)bitmap.GetPixels(), size = (uint)(bitmap.Width * bitmap.Height * 4) };
            surface = SKSurface.Create(bitmap.Info, bitmap.GetPixels(out _), bitmap.BytesPerPixel * bitmap.Width);
            canvas = surface.Canvas;
        }

        public void Prepare()
        {
            canvas?.Clear(SKColor.Empty);
            canvas?.ResetMatrix();
        }

        public void FlushCanvas()
        {
            canvas?.Flush();
        }

        public unsafe void UpdateTexture()
        {
            if (SokolTexture.IsValid)
            {
                sg_update_image(SokolTexture.Image, image_data);
            }
        }

        public unsafe void Flush()
        {
            FlushCanvas();
            UpdateTexture();
        }

        public void Dispose()
        {
            surface?.Dispose();
            canvas?.Dispose();
            canvas = null;
            surface = null;
            bitmap.Dispose();
            bitmap = null;
            SokolTexture.Dispose();
            SokolTexture = null;
        }


    }
}