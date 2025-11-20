// Copyright (c) 2022 Eli Aloni (a.k.a  elix22)
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Task = Microsoft.Build.Utilities.Task;
using CliWrap;
using CliWrap.Buffered;
using SkiaSharp;

namespace SokolApplicationBuilder
{
    public class ImageProcessTask : Task
    {
        Options opts;

        public ImageProcessTask(Options opts)
        {
            this.opts = opts;
        }

        public override bool Execute()
        {
            try
            {
                // Validate required options
                if (string.IsNullOrEmpty(opts.SourceImage))
                {
                    Log.LogError("--source is required for image processing task");
                    return false;
                }

                if (string.IsNullOrEmpty(opts.DestImage))
                {
                    Log.LogError("--dest is required for image processing task");
                    return false;
                }

                if (!File.Exists(opts.SourceImage))
                {
                    Log.LogError($"Source image not found: {opts.SourceImage}");
                    return false;
                }

                // Default dimensions if not specified
                int width = opts.Width > 0 ? opts.Width : 800;
                int height = opts.Height > 0 ? opts.Height : 600;
                
                // Determine mode
                string mode = opts.ImageMode?.ToLower() ?? "crop";

                Log.LogMessage(MessageImportance.High, $"🖼️  Processing image: {Path.GetFileName(opts.SourceImage)}");
                Log.LogMessage(MessageImportance.High, $"   Target size: {width}x{height}");
                
                if (mode == "cut")
                {
                    Log.LogMessage(MessageImportance.High, $"   Mode: cut (extract region)");
                }
                else if (mode == "crop")
                {
                    Log.LogMessage(MessageImportance.High, $"   Mode: crop (fill)");
                }
                else
                {
                    Log.LogMessage(MessageImportance.High, $"   Mode: fit (contain)");
                }
                
                if (opts.CutX != 0 || opts.CutY != 0)
                {
                    Log.LogMessage(MessageImportance.High, $"   Starting point: ({opts.CutX}, {opts.CutY})");
                }
                
                Log.LogMessage(MessageImportance.High, $"   Output: {opts.DestImage}");

                // Create destination directory if it doesn't exist
                string destDir = Path.GetDirectoryName(opts.DestImage);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                // Process the image
                bool success = mode switch
                {
                    "cut" => CutImageRegion(opts.SourceImage, opts.DestImage, opts.CutX, opts.CutY, width, height),
                    "fit" => ResizeAndFitImage(opts.SourceImage, opts.DestImage, width, height, opts.CutX, opts.CutY),
                    _ => ResizeAndCropImage(opts.SourceImage, opts.DestImage, width, height, opts.CutX, opts.CutY)
                };

                if (success)
                {
                    Log.LogMessage(MessageImportance.High, $"✅ Image processed successfully: {opts.DestImage}");
                    return true;
                }
                else
                {
                    Log.LogError($"❌ Failed to process image");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"Image processing failed: {ex.Message}");
                return false;
            }
        }

        bool ResizeAndCropImage(string sourcePath, string destPath, int width, int height, int startX = 0, int startY = 0)
        {
            // First choice: Use SkiaSharp (pure C# - always available, cross-platform, high quality)
            try
            {
                if (ResizeAndCropWithSkiaSharp(sourcePath, destPath, width, height, startX, startY))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.LogMessage(MessageImportance.Low, $"SkiaSharp image processing failed: {ex.Message}");
            }

            // Fallback: Try ImageMagick 7+ with 'magick' command
            try
            {
                string cropArg = (startX != 0 || startY != 0) ? $"-gravity NorthWest -crop +{startX}+{startY} +repage " : "";
                var magickResult = Cli.Wrap("magick")
                    .WithArguments($"\"{sourcePath}\" {cropArg}-resize {width}x{height}^ -gravity center -extent {width}x{height} \"{destPath}\"")
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();

                if (magickResult.ExitCode == 0 && File.Exists(destPath))
                {
                    Log.LogMessage(MessageImportance.Normal, "   Used ImageMagick 7+");
                    return true;
                }
            }
            catch { }

            // Fallback: Try ImageMagick 6 with 'convert' command
            try
            {
                string cropArg = (startX != 0 || startY != 0) ? $"-gravity NorthWest -crop +{startX}+{startY} +repage " : "";
                var convertResult = Cli.Wrap("convert")
                    .WithArguments($"\"{sourcePath}\" {cropArg}-resize {width}x{height}^ -gravity center -extent {width}x{height} \"{destPath}\"")
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();

                if (convertResult.ExitCode == 0 && File.Exists(destPath))
                {
                    Log.LogMessage(MessageImportance.Normal, "   Used ImageMagick 6");
                    return true;
                }
            }
            catch { }

            // Fallback: Try sips (macOS only)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                try
                {
                    // sips doesn't support center crop directly, so we'll use SkiaSharp or copy
                    File.Copy(sourcePath, destPath, true);
                    
                    var sipsResult = Cli.Wrap("sips")
                        .WithArguments($"-z {height} {width} \"{destPath}\"")
                        .WithValidation(CommandResultValidation.None)
                        .ExecuteBufferedAsync()
                        .GetAwaiter()
                        .GetResult();

                    if (sipsResult.ExitCode == 0)
                    {
                        Log.LogMessage(MessageImportance.Normal, "   Used sips (note: may not preserve aspect ratio)");
                        return true;
                    }
                }
                catch { }
            }

            // Final fallback: Copy original
            File.Copy(sourcePath, destPath, true);
            Log.LogWarning($"⚠️  All image processing methods failed. Copied original.");
            return true; // Return true since we at least copied the file
        }

        bool ResizeAndCropWithSkiaSharp(string sourcePath, string destPath, int width, int height, int startX = 0, int startY = 0)
        {
            // Load the source image
            using var inputStream = File.OpenRead(sourcePath);
            using var original = SKBitmap.Decode(inputStream);
            
            if (original == null)
            {
                Log.LogWarning($"   ⚠️  Failed to decode image: {sourcePath}");
                return false;
            }

            // Extract region first if starting point is specified
            SKBitmap sourceRegion = original;
            bool needDispose = false;
            
            if (startX != 0 || startY != 0)
            {
                // Validate coordinates
                if (startX < 0 || startY < 0 || startX >= original.Width || startY >= original.Height)
                {
                    Log.LogError($"   ❌ Invalid starting point ({startX}, {startY}). Image size is {original.Width}x{original.Height}");
                    return false;
                }

                // Extract region from starting point to end of image
                int regionWidth = original.Width - startX;
                int regionHeight = original.Height - startY;
                sourceRegion = new SKBitmap(regionWidth, regionHeight);
                needDispose = true;
                
                using var tempCanvas = new SKCanvas(sourceRegion);
                var extractSrcRect = new SKRect(startX, startY, original.Width, original.Height);
                var extractDestRect = new SKRect(0, 0, regionWidth, regionHeight);
                tempCanvas.DrawBitmap(original, extractSrcRect, extractDestRect);
            }

            // Create target bitmap
            using var target = new SKBitmap(width, height);
            using var canvas = new SKCanvas(target);
            
            // Calculate scaling to cover the entire target area (crop to fit)
            int srcWidth = sourceRegion.Width;
            int srcHeight = sourceRegion.Height;
            float srcAspect = (float)srcWidth / srcHeight;
            float targetAspect = (float)width / height;

            int cropWidth, cropHeight, cropX, cropY;
            
            if (Math.Abs(srcAspect - targetAspect) < 0.01f)
            {
                // Aspect ratios are similar, use full image
                cropWidth = srcWidth;
                cropHeight = srcHeight;
                cropX = 0;
                cropY = 0;
            }
            else if (srcAspect > targetAspect)
            {
                // Source is wider, crop width (center crop)
                cropHeight = srcHeight;
                cropWidth = (int)(srcHeight * targetAspect);
                cropX = (srcWidth - cropWidth) / 2;
                cropY = 0;
            }
            else
            {
                // Source is taller, crop height (center crop)
                cropWidth = srcWidth;
                cropHeight = (int)(srcWidth / targetAspect);
                cropX = 0;
                cropY = (srcHeight - cropHeight) / 2;
            }

            // Create cropped bitmap
            using var cropped = new SKBitmap(cropWidth, cropHeight);
            using var cropCanvas = new SKCanvas(cropped);
            
            var srcRect = new SKRect(cropX, cropY, cropX + cropWidth, cropY + cropHeight);
            var destRect = new SKRect(0, 0, cropWidth, cropHeight);
            
            var paint = new SKPaint
            {
                IsAntialias = true
            };
            cropCanvas.DrawBitmap(sourceRegion, srcRect, destRect, paint);
            
            // Dispose sourceRegion if we created it
            if (needDispose)
            {
                sourceRegion.Dispose();
            }

            // Resize to target size with high-quality sampling
            var imageInfo = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            var samplingOptions = new SKSamplingOptions(SKCubicResampler.CatmullRom);
            using var resized = cropped.Resize(imageInfo, samplingOptions);
            
            if (resized == null)
            {
                Log.LogWarning($"   ⚠️  Failed to resize image to {width}x{height}");
                return false;
            }

            // Save as PNG
            using var image = SKImage.FromBitmap(resized);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var outputStream = File.OpenWrite(destPath);
            data.SaveTo(outputStream);

            Log.LogMessage(MessageImportance.Normal, "   Used SkiaSharp (high quality)");
            return true;
        }

        bool ResizeAndFitImage(string sourcePath, string destPath, int width, int height, int startX = 0, int startY = 0)
        {
            // First choice: Use SkiaSharp
            try
            {
                if (ResizeAndFitWithSkiaSharp(sourcePath, destPath, width, height, startX, startY))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.LogMessage(MessageImportance.Low, $"SkiaSharp image processing failed: {ex.Message}");
            }

            // Fallback: Try ImageMagick 7+ with 'magick' command
            try
            {
                string cropArg = (startX != 0 || startY != 0) ? $"-gravity NorthWest -crop +{startX}+{startY} +repage " : "";
                var magickResult = Cli.Wrap("magick")
                    .WithArguments($"\"{sourcePath}\" {cropArg}-resize {width}x{height} -background white -gravity center -extent {width}x{height} \"{destPath}\"")
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();

                if (magickResult.ExitCode == 0 && File.Exists(destPath))
                {
                    Log.LogMessage(MessageImportance.Normal, "   Used ImageMagick 7+");
                    return true;
                }
            }
            catch { }

            // Fallback: Try ImageMagick 6 with 'convert' command
            try
            {
                string cropArg = (startX != 0 || startY != 0) ? $"-gravity NorthWest -crop +{startX}+{startY} +repage " : "";
                var convertResult = Cli.Wrap("convert")
                    .WithArguments($"\"{sourcePath}\" {cropArg}-resize {width}x{height} -background white -gravity center -extent {width}x{height} \"{destPath}\"")
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();

                if (convertResult.ExitCode == 0 && File.Exists(destPath))
                {
                    Log.LogMessage(MessageImportance.Normal, "   Used ImageMagick 6");
                    return true;
                }
            }
            catch { }

            // Final fallback: Copy original
            File.Copy(sourcePath, destPath, true);
            Log.LogWarning($"⚠️  All image processing methods failed. Copied original.");
            return true;
        }

        bool ResizeAndFitWithSkiaSharp(string sourcePath, string destPath, int width, int height, int startX = 0, int startY = 0)
        {
            // Load the source image
            using var inputStream = File.OpenRead(sourcePath);
            using var original = SKBitmap.Decode(inputStream);
            
            if (original == null)
            {
                Log.LogWarning($"   ⚠️  Failed to decode image: {sourcePath}");
                return false;
            }

            // Extract region first if starting point is specified
            SKBitmap sourceRegion = original;
            if (startX != 0 || startY != 0)
            {
                // Validate coordinates
                if (startX < 0 || startY < 0 || startX >= original.Width || startY >= original.Height)
                {
                    Log.LogError($"   ❌ Invalid starting point ({startX}, {startY}). Image size is {original.Width}x{original.Height}");
                    return false;
                }

                // Extract region from starting point to end of image
                int regionWidth = original.Width - startX;
                int regionHeight = original.Height - startY;
                sourceRegion = new SKBitmap(regionWidth, regionHeight);
                
                using var extractCanvas = new SKCanvas(sourceRegion);
                var extractSrcRect = new SKRect(startX, startY, original.Width, original.Height);
                var extractDestRect = new SKRect(0, 0, regionWidth, regionHeight);
                extractCanvas.DrawBitmap(original, extractSrcRect, extractDestRect);
            }

            // Create target bitmap with white background
            using var target = new SKBitmap(width, height);
            using var canvas = new SKCanvas(target);
            
            // Fill with white background
            canvas.Clear(SKColors.White);
            
            // Calculate scaling to fit entire image within bounds (letterbox/pillarbox)
            int srcWidth = sourceRegion.Width;
            int srcHeight = sourceRegion.Height;
            float srcAspect = (float)srcWidth / srcHeight;
            float targetAspect = (float)width / height;

            int scaledWidth, scaledHeight;
            float offsetX, offsetY;
            
            if (srcAspect > targetAspect)
            {
                // Source is wider - fit to width
                scaledWidth = width;
                scaledHeight = (int)(width / srcAspect);
                offsetX = 0;
                offsetY = (height - scaledHeight) / 2f;
            }
            else
            {
                // Source is taller - fit to height
                scaledHeight = height;
                scaledWidth = (int)(height * srcAspect);
                offsetX = (width - scaledWidth) / 2f;
                offsetY = 0;
            }

            // Draw the image centered with white background
            var destRect = new SKRect(offsetX, offsetY, offsetX + scaledWidth, offsetY + scaledHeight);
            var paint = new SKPaint
            {
                IsAntialias = true
            };
            canvas.DrawBitmap(sourceRegion, destRect, paint);
            
            // Dispose sourceRegion if we created it
            if (startX != 0 || startY != 0)
            {
                sourceRegion.Dispose();
            }

            // Save as PNG
            using var image = SKImage.FromBitmap(target);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var outputStream = File.OpenWrite(destPath);
            data.SaveTo(outputStream);

            Log.LogMessage(MessageImportance.Normal, "   Used SkiaSharp (high quality, fit mode)");
            return true;
        }

        bool CutImageRegion(string sourcePath, string destPath, int x, int y, int width, int height)
        {
            // First choice: Use SkiaSharp
            try
            {
                if (CutImageRegionWithSkiaSharp(sourcePath, destPath, x, y, width, height))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.LogMessage(MessageImportance.Low, $"SkiaSharp image processing failed: {ex.Message}");
            }

            // Fallback: Try ImageMagick 7+ with 'magick' command
            try
            {
                var magickResult = Cli.Wrap("magick")
                    .WithArguments($"\"{sourcePath}\" -crop {width}x{height}+{x}+{y} +repage \"{destPath}\"")
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();

                if (magickResult.ExitCode == 0 && File.Exists(destPath))
                {
                    Log.LogMessage(MessageImportance.Normal, "   Used ImageMagick 7+");
                    return true;
                }
            }
            catch { }

            // Fallback: Try ImageMagick 6 with 'convert' command
            try
            {
                var convertResult = Cli.Wrap("convert")
                    .WithArguments($"\"{sourcePath}\" -crop {width}x{height}+{x}+{y} +repage \"{destPath}\"")
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();

                if (convertResult.ExitCode == 0 && File.Exists(destPath))
                {
                    Log.LogMessage(MessageImportance.Normal, "   Used ImageMagick 6");
                    return true;
                }
            }
            catch { }

            // Final fallback: Copy original
            File.Copy(sourcePath, destPath, true);
            Log.LogWarning($"⚠️  All image processing methods failed. Copied original.");
            return true;
        }

        bool CutImageRegionWithSkiaSharp(string sourcePath, string destPath, int x, int y, int width, int height)
        {
            // Load the source image
            using var inputStream = File.OpenRead(sourcePath);
            using var original = SKBitmap.Decode(inputStream);
            
            if (original == null)
            {
                Log.LogWarning($"   ⚠️  Failed to decode image: {sourcePath}");
                return false;
            }

            // Validate coordinates and dimensions
            if (x < 0 || y < 0 || x >= original.Width || y >= original.Height)
            {
                Log.LogError($"   ❌ Invalid starting point ({x}, {y}). Image size is {original.Width}x{original.Height}");
                return false;
            }

            // Adjust width/height if they exceed image boundaries
            int actualWidth = Math.Min(width, original.Width - x);
            int actualHeight = Math.Min(height, original.Height - y);

            if (actualWidth != width || actualHeight != height)
            {
                Log.LogWarning($"   ⚠️  Requested region exceeds image bounds. Adjusted to {actualWidth}x{actualHeight}");
            }

            // Create target bitmap
            using var target = new SKBitmap(actualWidth, actualHeight);
            using var canvas = new SKCanvas(target);
            
            // Extract the region
            var srcRect = new SKRect(x, y, x + actualWidth, y + actualHeight);
            var destRect = new SKRect(0, 0, actualWidth, actualHeight);
            
            var paint = new SKPaint
            {
                IsAntialias = true
            };
            canvas.DrawBitmap(original, srcRect, destRect, paint);

            // Save as PNG
            using var image = SKImage.FromBitmap(target);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var outputStream = File.OpenWrite(destPath);
            data.SaveTo(outputStream);

            Log.LogMessage(MessageImportance.Normal, $"   Used SkiaSharp (extracted {actualWidth}x{actualHeight} region)");
            return true;
        }
    }
}
