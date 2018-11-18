using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        static WriteableBitmap writeableBitmap;
        static Window w;
        static Image i;
        static int resolutionX = 16;
        static int resolutionY = 16;

        static int prevX;
        static int prevY;

        static int scale = 1;

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
        }

        // https://docs.microsoft.com/en-us/dotnet/api/system.windows.media.imaging.writeablebitmap?redirectedfrom=MSDN&view=netframework-4.7.2
        // The DrawPixel method updates the WriteableBitmap by using
        // unsafe code to write a pixel into the back buffer.
        static void DrawPixel(MouseEventArgs e)
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

                    // Compute the pixel's color.
                    int color_data = 255 << 16; // R
                    color_data |= 0 << 8;   // G
                    color_data |= 0 << 0;   // B

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

        static void ErasePixel(MouseEventArgs e)
        {
            byte[] ColorData = { 0, 0, 0, 0 }; // B G R

            Int32Rect rect = new Int32Rect(
                    (int)(e.GetPosition(i).X),
                    (int)(e.GetPosition(i).Y),
                    1,
                    1);

            writeableBitmap.WritePixels(rect, ColorData, 4, 0);
        }

        static void i_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            ErasePixel(e);
        }

        static void i_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DrawPixel(e);
        }

        static void i_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DrawPixel(e);
            }
            else if (e.RightButton == MouseButtonState.Pressed)
            {
                ErasePixel(e);
            }
        }

        static void w_MouseWheel(object sender, MouseWheelEventArgs e)
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
