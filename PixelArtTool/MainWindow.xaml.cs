using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PixelArtTool
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        WriteableBitmap writeableBitmap;
        Window w;
        Image i;
        int resolutionX = 16;
        int resolutionY = 16;

        int prevX;
        int prevY;

        int scale = 1;

        public MainWindow()
        {
            InitializeComponent();
            Start();
        }

        void Start()
        {
            i = canvas;
            RenderOptions.SetBitmapScalingMode(i, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(i, EdgeMode.Aliased);

            w = (MainWindow)Application.Current.MainWindow;

            var dpiX = 96;
            var dpiY = 96;

            scale = (int)i.Width / resolutionX;

            writeableBitmap = new WriteableBitmap(resolutionX, resolutionY, dpiX, dpiY, PixelFormats.Bgr32, null);

            i.Source = writeableBitmap;

            i.MouseMove += new MouseEventHandler(i_MouseMove);
            i.MouseLeftButtonDown += new MouseButtonEventHandler(i_MouseLeftButtonDown);
            i.MouseRightButtonDown += new MouseButtonEventHandler(i_MouseRightButtonDown);
            w.MouseWheel += new MouseWheelEventHandler(w_MouseWheel);

            LoadPalette();
        }

        PixelColor[] palette;

        void LoadPalette()
        {
            Uri uri = new Uri("pack://application:,,,/Resources/Palettes/aap-64-1x.png");
            var img = new BitmapImage(uri);

            // get colors
            var pixels = GetPixels(img);

            var width = pixels.GetLength(0);
            var height = pixels.GetLength(1);

            palette = new PixelColor[width * height];

            int index = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var c = pixels[x, y];
                    //                    Console.WriteLine(c.Red + "," + c.Green + "," + c.Blue);
                    palette[index++] = c;
                }
            }

        }

        // https://stackoverflow.com/a/1740553/5452781
        public unsafe static void CopyPixels2(BitmapSource source, PixelColor[,] pixels, int stride, int offset, bool dummy)
        //        public unsafe static void CopyPixels(BitmapSource source, PixelColor[,] pixels, int stride, int offset)
        {
            fixed (PixelColor* buffer = &pixels[0, 0])
                source.CopyPixels(
                  new Int32Rect(0, 0, source.PixelWidth, source.PixelHeight),
                  (IntPtr)(buffer + offset),
                  pixels.GetLength(0) * pixels.GetLength(1) * sizeof(PixelColor),
                  stride);
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct PixelColor
        {
            // 32 bit BGRA 
            [FieldOffset(0)] public UInt32 ColorBGRA;
            // 8 bit components
            [FieldOffset(0)] public byte Blue;
            [FieldOffset(1)] public byte Green;
            [FieldOffset(2)] public byte Red;
            [FieldOffset(3)] public byte Alpha;
        }

        public PixelColor[,] GetPixels(BitmapSource source)
        {
            if (source.Format != PixelFormats.Bgra32)
                source = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

            int width = source.PixelWidth;
            int height = source.PixelHeight;
            PixelColor[,] result = new PixelColor[width, height];

            CopyPixels2(source, result, width * 4, 0, false);
            //source.CopyPixels(result, width * 4, 0, false);
            return result;
        }

        // from palette
        int currentColorIndex = 0;

        // https://docs.microsoft.com/en-us/dotnet/api/system.windows.media.imaging.writeablebitmap?redirectedfrom=MSDN&view=netframework-4.7.2
        // The DrawPixel method updates the WriteableBitmap by using
        // unsafe code to write a pixel into the back buffer.
        void DrawPixel(MouseEventArgs e)
        {
            int x = (int)(e.GetPosition(i).X / scale);
            int y = (int)(e.GetPosition(i).Y / scale);

            if (x < 0 || x > resolutionX - 1) return;
            if (y < 0 || y > resolutionY - 1) return;

            try
            {
                // Reserve the back buffer for updates.
                writeableBitmap.Lock();

                unsafe
                {
                    // Get a pointer to the back buffer.
                    int pBackBuffer = (int)writeableBitmap.BackBuffer;

                    // Find the address of the pixel to draw.
                    pBackBuffer += y * writeableBitmap.BackBufferStride;
                    pBackBuffer += x * 4;

                    // test
                    currentColorIndex = ++currentColorIndex % palette.Length;

                    int color_data = (int)palette[currentColorIndex].ColorBGRA;

                    // Assign the color data to the pixel.
                    *((int*)pBackBuffer) = color_data;
                }

                // Specify the area of the bitmap that changed.
                writeableBitmap.AddDirtyRect(new Int32Rect(x, y, 1, 1));
            }
            finally
            {
                // Release the back buffer and make it available for display.
                writeableBitmap.Unlock();
            }

            prevX = x;
            prevY = y;

        }

        void ErasePixel(MouseEventArgs e)
        {
            byte[] ColorData = { 0, 0, 0, 0 }; // B G R

            int x = (int)(e.GetPosition(i).X / scale);
            int y = (int)(e.GetPosition(i).Y / scale);
            if (x < 0 || x > resolutionX - 1) return;
            if (y < 0 || y > resolutionY - 1) return;

            Int32Rect rect = new Int32Rect(x, y, 1, 1);
            writeableBitmap.WritePixels(rect, ColorData, 4, 0);
        }

        void i_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            ErasePixel(e);
        }

        void i_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DrawPixel(e);
        }

        void i_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DrawPixel(e);
            }
            else if (e.RightButton == MouseButtonState.Pressed)
            {
                ErasePixel(e);
            }
            // update mousepos
            int x = (int)(e.GetPosition(i).X / scale);
            int y = (int)(e.GetPosition(i).Y / scale);
            lblMousePos.Content = x + "," + y;
        }

        void w_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            /*
            System.Windows.Media.Matrix m = i.RenderTransform.Value;

            if (e.Delta > 0)
            {
                m.ScaleAt(
                    1.5,
                    1.5,
                    e.GetPosition(w).X,
                    e.GetPosition(w).Y);
            }
            else
            {
                m.ScaleAt(
                    1.0 / 1.5,
                    1.0 / 1.5,
                    e.GetPosition(w).X,
                    e.GetPosition(w).Y);
            }

            i.RenderTransform = new MatrixTransform(m);
            */
        }

    }


}
