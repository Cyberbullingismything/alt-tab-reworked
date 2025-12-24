using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Collections.Concurrent;

namespace SmoothTabTransition.Services
{
    public class WindowThumbnailService
    {

        private const uint PW_RENDERFULLCONTENT = 0x2;

        private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdc, int x, int y, int cx, int cy, IntPtr hdcSrc, int x1, int y1, uint rop);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

        private const uint SRCCOPY = 0x00CC0020;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private static readonly ConcurrentDictionary<IntPtr, CachedThumbnail> _thumbnailCache = new();
        private static readonly TimeSpan CacheExpiry = TimeSpan.FromSeconds(1);

        private class CachedThumbnail
        {
            public ImageSource? Image { get; set; }
            public DateTime CapturedAt { get; set; }
        }


        public static ImageSource? CaptureWindowThumbnail(IntPtr hWnd, int maxWidth = 800, int maxHeight = 500)
        {
            try
            {
                if (!IsWindow(hWnd))
                    return null;

                if (_thumbnailCache.TryGetValue(hWnd, out var cached))
                {
                    if (DateTime.Now - cached.CapturedAt < CacheExpiry && cached.Image != null)
                    {
                        return cached.Image;
                    }
                }

                RECT rect;
                int result = DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out rect, Marshal.SizeOf<RECT>());
                
                if (result != 0)
                {
                    GetWindowRect(hWnd, out rect);
                }

                int windowWidth = rect.Right - rect.Left;
                int windowHeight = rect.Bottom - rect.Top;

                if (windowWidth <= 0 || windowHeight <= 0)
                    return null;

                Bitmap? fullCapture = CaptureWindowFull(hWnd, windowWidth, windowHeight);
                
                if (fullCapture == null)
                    return null;

                double aspectRatio = (double)windowWidth / windowHeight;
                int thumbWidth, thumbHeight;

                if (aspectRatio > (double)maxWidth / maxHeight)
                {
                    thumbWidth = maxWidth;
                    thumbHeight = Math.Max(1, (int)(maxWidth / aspectRatio));
                }
                else
                {
                    thumbHeight = maxHeight;
                    thumbWidth = Math.Max(1, (int)(maxHeight * aspectRatio));
                }

                var thumbnail = CreateHighQualityThumbnail(fullCapture, thumbWidth, thumbHeight);

                if (thumbnail != null)
                {
                    _thumbnailCache[hWnd] = new CachedThumbnail
                    {
                        Image = thumbnail,
                        CapturedAt = DateTime.Now
                    };
                }

                return thumbnail;
            }
            catch
            {
                return null;
            }
        }


        private static Bitmap? CaptureWindowFull(IntPtr hWnd, int width, int height)
        {
            Bitmap? bitmap = null;
            
            try
            {

                bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(System.Drawing.Color.Black);
                    
                    IntPtr hdcBitmap = graphics.GetHdc();
                    
                    try
                    {

                        bool success = PrintWindow(hWnd, hdcBitmap, PW_RENDERFULLCONTENT);
                        
                        if (!success)
                        {

                            success = PrintWindow(hWnd, hdcBitmap, 0);
                        }

                        if (!success)
                        {
                            bitmap.Dispose();
                            return null;
                        }
                    }
                    finally
                    {
                        graphics.ReleaseHdc(hdcBitmap);
                    }
                }

                if (IsEmptyBitmap(bitmap))
                {
                    bitmap.Dispose();
                    return null;
                }

                return bitmap;
            }
            catch
            {
                bitmap?.Dispose();
                return null;
            }
        }


        private static ImageSource? CreateHighQualityThumbnail(Bitmap source, int targetWidth, int targetHeight)
        {
            try
            {
                using (source)
                {

                    using var thumbnail = new Bitmap(targetWidth, targetHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    
                    using (var graphics = Graphics.FromImage(thumbnail))
                    {

                        graphics.CompositingMode = CompositingMode.SourceCopy;
                        graphics.CompositingQuality = CompositingQuality.HighQuality;
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.SmoothingMode = SmoothingMode.HighQuality;
                        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                        graphics.Clear(System.Drawing.Color.Transparent);

                        using var attributes = new ImageAttributes();
                        attributes.SetWrapMode(WrapMode.TileFlipXY);

                        var destRect = new Rectangle(0, 0, targetWidth, targetHeight);
                        graphics.DrawImage(
                            source, 
                            destRect, 
                            0, 0, source.Width, source.Height, 
                            GraphicsUnit.Pixel, 
                            attributes);
                    }

                    return ConvertToImageSource(thumbnail);
                }
            }
            catch
            {
                return null;
            }
        }


        private static ImageSource? ConvertToImageSource(Bitmap bitmap)
        {
            IntPtr hBitmap = bitmap.GetHbitmap();
            try
            {
                var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                bitmapSource.Freeze();
                return bitmapSource;
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }


        private static bool IsEmptyBitmap(Bitmap bitmap)
        {
            try
            {
                var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
                var bmpData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                try
                {
                    int bytesPerPixel = 4;
                    int stride = bmpData.Stride;
                    IntPtr scan0 = bmpData.Scan0;

                    int sampleCount = 0;
                    int blackCount = 0;
                    
                    int stepX = Math.Max(1, bitmap.Width / 8);
                    int stepY = Math.Max(1, bitmap.Height / 8);

                    unsafe
                    {
                        byte* ptr = (byte*)scan0;

                        for (int y = stepY; y < bitmap.Height - stepY; y += stepY)
                        {
                            for (int x = stepX; x < bitmap.Width - stepX; x += stepX)
                            {
                                int offset = y * stride + x * bytesPerPixel;
                                byte b = ptr[offset];
                                byte g = ptr[offset + 1];
                                byte r = ptr[offset + 2];

                                sampleCount++;
                                if (r < 8 && g < 8 && b < 8)
                                    blackCount++;
                            }
                        }
                    }

                    return sampleCount > 0 && (double)blackCount / sampleCount > 0.92;
                }
                finally
                {
                    bitmap.UnlockBits(bmpData);
                }
            }
            catch
            {
                return false;
            }
        }


        public static async Task<ImageSource?[]> CaptureMultipleThumbnailsAsync(IntPtr[] windowHandles, int maxWidth = 800, int maxHeight = 500)
        {
            var results = new ImageSource?[windowHandles.Length];

            int batchSize = Math.Max(2, Environment.ProcessorCount);
            
            for (int i = 0; i < windowHandles.Length; i += batchSize)
            {
                var tasks = new Task[Math.Min(batchSize, windowHandles.Length - i)];
                
                for (int j = 0; j < tasks.Length; j++)
                {
                    int index = i + j;
                    tasks[j] = Task.Run(() =>
                    {
                        results[index] = CaptureWindowThumbnail(windowHandles[index], maxWidth, maxHeight);
                    });
                }
                
                await Task.WhenAll(tasks);
            }
            
            return results;
        }

        public static void ClearCache()
        {
            _thumbnailCache.Clear();
        }
    }
}
