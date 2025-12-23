using System;
using static Sokol.SG;
using static Sokol.StbImage;
using static Sokol.SBasisu;
using static Sokol.Utils;

namespace Sokol
{
    public class Texture : IDisposable
    {
        public sg_image Image { get; private set; }
        public sg_view View { get; private set; }
        public sg_sampler Sampler { get; private set; }
        public bool IsValid => Image.id != 0;

        private bool disposed;

        public Texture(int width, int height, sg_pixel_format format = sg_pixel_format.SG_PIXELFORMAT_RGBA8, string label = "skia", SamplerSettings? samplerSettings = null)
        {
            samplerSettings ??= new SamplerSettings(); // Use defaults if null
            // Create image with mipmaps
            // Setting num_mipmaps = 0 tells Sokol to auto-calculate the mip count based on dimensions
            // and auto-generate the mipmap chain on the GPU
            var img_desc = new sg_image_desc
            {
                width = width,
                height = height,
                pixel_format = format,
                num_mipmaps = 0,  // 0 = auto-calculate and generate mipmaps
                label = label
            };
            img_desc.usage.stream_update = true;
            Image = sg_make_image(img_desc);

            // Create view
            View = sg_make_view(new sg_view_desc
            {
                texture = new sg_texture_view_desc { image = Image },
                label = $"{label}-view"
            });

            // Create sampler with proper settings from glTF
            Sampler = sg_make_sampler(new sg_sampler_desc
            {
                min_filter = samplerSettings.MinFilter,
                mag_filter = samplerSettings.MagFilter,
                mipmap_filter = samplerSettings.MipmapFilter,
                wrap_u = samplerSettings.WrapU,
                wrap_v = samplerSettings.WrapV,
                label = $"{label}-sampler"
            });
        }


        private Texture() { }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {

                // Destroy sokol graphics resources
                if (Image.id != 0)
                {
                    sg_destroy_sampler(Sampler);
                    sg_destroy_view(View);
                    sg_destroy_image(Image);
                    Image = default;
                    View = default;
                    Sampler = default;
                }

                disposed = true;
            }
        }

        ~Texture()
        {
            Dispose(false);
        }
    }
}
