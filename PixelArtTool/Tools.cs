using Microsoft.Win32;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PixelArtTool
{
    public static class Tools
    {
        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out CustomPoint lpPoint);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetDesktopWindow();
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetWindowDC(IntPtr window);
        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern uint GetPixel(IntPtr dc, int x, int y);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern int ReleaseDC(IntPtr window, IntPtr dc);

        // returns windows cursor position
        public static Point GetCursorPosition()
        {
            CustomPoint lpPoint;
            GetCursorPos(out lpPoint);
            return lpPoint;
        }

        // return win32 screen pixel color https://stackoverflow.com/a/24759418/5452781
        public static Color Win32GetScreenPixel(int x, int y)
        {
            IntPtr desk = GetDesktopWindow();
            IntPtr dc = GetWindowDC(desk);
            int a = (int)GetPixel(dc, x, y);
            ReleaseDC(desk, dc);
            return Color.FromArgb(255, (byte)((a >> 0) & 0xff), (byte)((a >> 8) & 0xff), (byte)((a >> 16) & 0xff));
        }

        public static PixelColor Win32GetScreenPixelColor()
        {
            CustomPoint cursor;
            GetCursorPos(out cursor);
            IntPtr desk = GetDesktopWindow();
            IntPtr dc = GetWindowDC(desk);
            int a = (int)GetPixel(dc, cursor.X, cursor.Y);
            ReleaseDC(desk, dc);
            var c = new PixelColor();
            c.Alpha = 255;
            c.Red = (byte)((a >> 0) & 0xff);
            c.Green = (byte)((a >> 8) & 0xff);
            c.Blue = (byte)((a >> 16) & 0xff);
            return c;
        }


        // fix savefiledialog extension https://stackoverflow.com/a/6104319/5452781
        public static void UseDefaultExtensionAsFilterIndex(FileDialog dialog)
        {
            var ext = "*." + dialog.DefaultExt;
            var filter = dialog.Filter;
            var filters = filter.Split('|');
            for (int i = 1; i < filters.Length; i += 2)
            {
                if (filters[i] == ext)
                {
                    dialog.FilterIndex = 1 + (i - 1) / 2;
                    return;
                }
            }
        }

        // get pixel colors from bitmapsource https://stackoverflow.com/a/1740553/5452781
        public unsafe static void CopyPixels2(BitmapSource source, PixelColor[,] resultPixels, int stride, int offset, bool dummy)
        {
            fixed (PixelColor* buffer = &resultPixels[0, 0])
                source.CopyPixels(new Int32Rect(0, 0, source.PixelWidth, source.PixelHeight),
                    (IntPtr)(buffer + offset), resultPixels.GetLength(0) * resultPixels.GetLength(1) * sizeof(PixelColor), stride);
        }


        // load bitmap palette, 1x png files only https://lospec.com/palette-list
        public static PixelColor[] LoadPalette(string path, WriteableBitmap targetBitmap, int paletteResolutionX, int paletteResolutionY)
        {
            Uri uri = new Uri(path);
            var img = new BitmapImage(uri);

            // get colors
            var pixels = GetPixels(img);

            var width = pixels.GetLength(0);
            var height = pixels.GetLength(1);

            var palette = new PixelColor[width * height];

            int index = 0;
            int x = 0;
            int y = 0;
            for (y = 0; y < height; y++)
            {
                for (x = 0; x < width; x++)
                {
                    var c = pixels[x, y];
                    palette[index++] = c;
                }
            }

            // put pixels on palette canvas
            x = y = 0;
            for (int i = 0, len = palette.Length; i < len; i++)
            {
                x = i % paletteResolutionX;
                y = (i % len) / paletteResolutionX;
                SetPixel(targetBitmap, x, y, (int)palette[i].ColorBGRA);
            }

            return palette;
        }

        // get pixels from source bitmap
        public static PixelColor[,] GetPixels(BitmapSource source)
        {
            if (source.Format != PixelFormats.Bgra32)
                source = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

            int width = source.PixelWidth;
            int height = source.PixelHeight;
            PixelColor[,] result = new PixelColor[width, height];

            CopyPixels2(source, result, width * 4, 0, false);
            return result;
        }

        // set single pixel to target bitmap
        public static void SetPixel(WriteableBitmap bitmap, int x, int y, int color)
        {
            try
            {
                // Reserve the back buffer for updates.
                bitmap.Lock();

                unsafe
                {
                    // Get a pointer to the back buffer.
                    int pBackBuffer = (int)bitmap.BackBuffer;

                    // Find the address of the pixel to draw.
                    pBackBuffer += y * bitmap.BackBufferStride;
                    pBackBuffer += x * 4;

                    // Assign the color data to the pixel.
                    *((int*)pBackBuffer) = color;
                }

                // Specify the area of the bitmap that changed.
                bitmap.AddDirtyRect(new Int32Rect(x, y, 1, 1));
            }
            finally
            {
                // Release the back buffer and make it available for display.
                bitmap.Unlock();
            }
        }

        // clears bitmap by writing empty pixels to it
        public static void ClearImage(WriteableBitmap targetBitmap, Int32Rect emptyRect, byte[] emptyPixels, int emptyStride)
        {
            targetBitmap.WritePixels(emptyRect, emptyPixels, emptyStride, 0);
        }

        public static void SetCurrentColorPreviewBox(Rectangle targetRectangle, PixelColor color)
        {
            var col = Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);
            targetRectangle.Fill = new SolidColorBrush(col);
        }

        public static byte ClampToByte(int n)
        {
            var r = n <= 255 ? n : (byte)Math.Min(n, (byte)255);
            return (byte)r;
        }

        // wrap-repeat value
        public static int Repeat(int val, int max)
        {
            int result = val % max;
            if (result < 0) result += max;
            return result;
        }

        public static void DrawBackgroundGrid(WriteableBitmap targetBitmap, int canvasResolutionX, int canvasResolutionY, PixelColor c1, PixelColor c2)
        {
            Console.WriteLine(123);
            PixelColor c = new PixelColor();
            for (int x = 0; x < canvasResolutionX; x++)
            {
                for (int y = 0; y < canvasResolutionY; y++)
                {
                    //                    c.Alpha = gridAlpha;
                    //                    byte v = (byte)(((x % 2) == (y % 2)) ? 255 : 0);
                    var v = ((x % 2) == (y % 2)) ? c1 : c2;
                    //                    c.Red = v;
                    //                    c.Green = v;
                    //                    c.Blue = v;
                    //v.Alpha = 255;
                    SetPixel(targetBitmap, x, y, (int)v.ColorBGRA);
                }
            }
        }
        /*
        public static void ColorToHSV(PixelColor c)
        {
            double h, l, s;
            RgbToHls(c.Red, c.Green, c.Blue, out h, out l, out s);
            //Console.WriteLine(h + "," + s + "," + l);
        }*/

        // http://www.java2s.com/Code/CSharp/2D-Graphics/HsvToRgb.htm
        public static Color HsvToRgb(double h, double s, double v)
        {
            int hi = (int)Math.Floor(h / 60.0) % 6;
            double f = (h / 60.0) - Math.Floor(h / 60.0);

            double p = v * (1.0 - s);
            double q = v * (1.0 - (f * s));
            double t = v * (1.0 - ((1.0 - f) * s));

            Color ret;

            switch (hi)
            {
                case 0:
                    ret = GetRgb(v, t, p);
                    break;
                case 1:
                    ret = GetRgb(q, v, p);
                    break;
                case 2:
                    ret = GetRgb(p, v, t);
                    break;
                case 3:
                    ret = GetRgb(p, q, v);
                    break;
                case 4:
                    ret = GetRgb(t, p, v);
                    break;
                case 5:
                    ret = GetRgb(v, p, q);
                    break;
                default:
                    ret = Color.FromArgb(0xFF, 0x00, 0x00, 0x00);
                    break;
            }
            return ret;
        }
        public static Color GetRgb(double r, double g, double b)
        {
            return Color.FromArgb(255, (byte)(r * 255.0), (byte)(g * 255.0), (byte)(b * 255.0));
        }

        // FIXME not working properly yet
        public static PixelColor AdjustColorLightness(PixelColor c, int dir)
        {
            // convert to hls
            //double h, l, s;
            double h, s, v;
            //RgbToHls(c.Red, c.Green, c.Blue, out h, out l, out s);
            RGB2HSV(c.Red, c.Green, c.Blue, out h, out s, out v);

            // adjust lightness
            v *= dir < 0 ? 0.9 : 1.1;

            v = Clamp(v, 0, 1);

            Console.WriteLine(v);

            // convert back to pixelcolor
            int r, g, b;
            //HlsToRgb(h, s, v, out r, out g, out b);
            HSV2RGB(h, s, v, out r, out g, out b);



            c.Red = (byte)r;
            c.Green = (byte)g;
            c.Blue = (byte)b;

            return c;
        }



        // http://lolengine.net/blog/2013/07/27/rgb-to-hsv-in-glsl
        //vec3 rgb2hsv(vec3 c)
        public static void RGB2HSV(int r, int g, int b, out double h, out double s, out double v)
        {
            //vec4 K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
            float Kx = 0;
            float Ky = -1.0f / 3.0f;
            float Kz = 2.0f / 3.0f;
            float Kw = -1;

            float cr = r / (float)255;
            float cg = g / (float)255;
            float cb = b / (float)255;

            //vec4 p = c.g < c.b ? vec4(c.bg, K.wz) : vec4(c.gb, K.xy);
            //vec4 q = c.r < p.x ? vec4(p.xyw, c.r) : vec4(c.r, p.yzx);

            float px = cb;
            float py = cg;
            float pz = Kw;
            float pw = Kz;
            if (cg < cb)
            {
            }
            else
            {
                px = cg;
                py = cb;
                pz = Kx;
                pw = Ky;
            }

            float qx = px;
            float qy = py;
            float qz = pw;
            float qw = cr;
            if (cg < cb)
            {
            }
            else
            {
                qx = cr;
                qy = py;
                qz = pz;
                qw = px;
            }

            //float d = q.x - min(q.w, q.y);
            float d = qx - Math.Min(qw, qy);

            // float e = 1.0e-10;
            float e = -float.Epsilon;

            //return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            h = Math.Abs(qz + (qw - qy) / (6.0 * d + e));
            s = d / (qx + e);
            v = qx;
        }

        // https://stackoverflow.com/a/51509540/5452781
        public static float Lerp(float firstFloat, float secondFloat, float by)
        {
            return firstFloat + (secondFloat - firstFloat) * by;
        }

        public static double Lerp(double firstFloat, double secondFloat, double by)
        {
            return firstFloat + (secondFloat - firstFloat) * by;
        }

        // Returns the fractional portion of a scalar or each vector component.
        // http://developer.download.nvidia.com/cg/frac.html
        public static float Frac(float v)
        {
            return v - (float)Math.Floor(v);
        }

        public static double Frac(double v)
        {
            return v - Math.Floor(v);
        }

        // http://developer.download.nvidia.com/cg/clamp.html
        public static float Clamp(float x, float a, float b)
        {
            return Math.Max(a, Math.Min(b, x));
        }

        public static double Clamp(double x, double a, double b)
        {
            return Math.Max(a, Math.Min(b, x));
        }

        public static void HSV2RGB(double h, double s, double v, out int r, out int g, out int b)
        {
            //vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
            double Kx = 0;
            double Ky = -1.0f / 3.0f;
            double Kz = 2.0f / 3.0f;
            double Kw = -1;

            double cx = h;// r / (float)255;
            double cy = s;// g / (float)255;
            double cz = v;// b / (float)255;

            //vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);

            double fx = Frac(cx + Kx) * 6.0f - Kw;
            double fy = Frac(cx + Ky) * 6.0f - Kw;
            double fz = Frac(cx + Kz) * 6.0f - Kw;

            double px = Math.Abs(fx);
            double py = Math.Abs(fy);
            double pz = Math.Abs(fz);

            //            return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
            double rx = cz * Lerp(Kx, Clamp(px - Kx, 0.0f, 1.0f), cy);
            double ry = cz * Lerp(Kx, Clamp(py - Kx, 0.0f, 1.0f), cy);
            double rz = cz * Lerp(Kx, Clamp(pz - Kx, 0.0f, 1.0f), cy);

            r = (int)(rx * 255);
            g = (int)(ry * 255);
            b = (int)(rz * 255);
        }

        // http://csharphelper.com/blog/2016/08/convert-between-rgb-and-hls-color-models-in-c/
        // Hue determines the color with a 0 to 360 degree direction on a color wheel.
        // Lightness indicates how much light is in the color. When lightness = 0, the color is black.When lightness = 1, the color is white.When lightness = 0.5, the color is as "pure" as possible.
        // Saturation indicates the amount of color added.You can think of this as the opposite of “grayness.” When saturation = 0, the color is pure gray. In this case, if lightness = 0.5 you get a neutral color.When saturation is 1, the color is "pure"
        // Convert an RGB value into an HLS value.
        public static void RgbToHls(int r, int g, int b, out double h, out double l, out double s)
        {
            // Convert RGB to a 0.0 to 1.0 range.
            double double_r = r / 255.0;
            double double_g = g / 255.0;
            double double_b = b / 255.0;

            // Get the maximum and minimum RGB components.
            double max = double_r;
            if (max < double_g) max = double_g;
            if (max < double_b) max = double_b;

            double min = double_r;
            if (min > double_g) min = double_g;
            if (min > double_b) min = double_b;

            double diff = max - min;
            l = (max + min) / 2;
            if (Math.Abs(diff) < 0.00001)
            {
                s = 0;
                h = 0;  // H is really undefined.
            }
            else
            {
                if (l <= 0.5) s = diff / (max + min);
                else s = diff / (2 - max - min);

                double r_dist = (max - double_r) / diff;
                double g_dist = (max - double_g) / diff;
                double b_dist = (max - double_b) / diff;

                if (double_r == max) h = b_dist - g_dist;
                else if (double_g == max) h = 2 + r_dist - b_dist;
                else h = 4 + g_dist - r_dist;

                h = h * 60;
                if (h < 0) h += 360;
            }
        }

        // Convert an HLS value into an RGB value.
        public static void HlsToRgb(double h, double l, double s, out int r, out int g, out int b)
        {
            double p2;
            if (l <= 0.5) p2 = l * (1 + s);
            else p2 = l + s - l * s;

            double p1 = 2 * l - p2;
            double double_r, double_g, double_b;
            if (s == 0)
            {
                double_r = l;
                double_g = l;
                double_b = l;
            }
            else
            {
                double_r = QqhToRgb(p1, p2, h + 120);
                double_g = QqhToRgb(p1, p2, h);
                double_b = QqhToRgb(p1, p2, h - 120);
            }

            // Convert RGB to the 0 to 255 range.
            r = (int)(double_r * 255.0);
            g = (int)(double_g * 255.0);
            b = (int)(double_b * 255.0);
        }

        private static double QqhToRgb(double q1, double q2, double hue)
        {
            if (hue > 360) hue -= 360;
            else if (hue < 0) hue += 360;

            if (hue < 60) return q1 + (q2 - q1) * hue / 60;
            if (hue < 180) return q2;
            if (hue < 240) return q1 + (q2 - q1) * (240 - hue) / 60;
            return q1;
        }


        // https://stackoverflow.com/a/14165162/5452781
        public static BitmapImage ConvertWriteableBitmapToBitmapImage(WriteableBitmap wbm)
        {
            BitmapImage bmImage = new BitmapImage();
            using (MemoryStream stream = new MemoryStream())
            {
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(wbm));
                encoder.Save(stream);
                bmImage.BeginInit();
                bmImage.CacheOption = BitmapCacheOption.OnLoad;
                bmImage.StreamSource = stream;
                bmImage.EndInit();
                bmImage.Freeze();
            }
            return bmImage;
        }

        public static SolidColorBrush ConvertSystemDrawingColorToSolidColorBrush(System.Drawing.Color c)
        {
            return new SolidColorBrush(Color.FromArgb(c.A, c.R, c.G, c.B));
        }

        public static System.Drawing.Color ConvertBrushToSystemDrawingColor(Brush c)
        {
            var bc = ((SolidColorBrush)c);
            var newc = System.Drawing.Color.FromArgb(bc.Color.A, bc.Color.R, bc.Color.G, bc.Color.B);
            return newc;
        }

    } // class
} // namespace
