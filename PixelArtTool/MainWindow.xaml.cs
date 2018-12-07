using Microsoft.Win32;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PixelArtTool
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        WriteableBitmap canvasBitmap;
        WriteableBitmap paletteBitmap;
        Window w;

        Image drawingImage;
        Image paletteImage;

        // bitmap settings
        int canvasResolutionX = 16;
        int canvasResolutionY = 16;
        int paletteResolutionX = 4;
        int paletteResolutionY = 16;
        int canvasScaleX = 1;
        int paletteScaleX = 1;
        int paletteScaleY = 1;
        int dpiX = 96;
        int dpiY = 96;

        // colors
        PixelColor currentColor;
        PixelColor[] palette;
        int currentColorIndex = 0;
        byte opacity = 255;

        // mouse
        int prevX;
        int prevY;

        // undo
        const int maxUndoCount = 100;
        int currentUndoIndex = 0;
        WriteableBitmap[] undoBufferBitmap = new WriteableBitmap[maxUndoCount];

        public MainWindow()
        {
            InitializeComponent();
            Start();
        }

        void Start()
        {
            // build drawing area
            drawingImage = imgCanvas;
            RenderOptions.SetBitmapScalingMode(drawingImage, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(drawingImage, EdgeMode.Aliased);
            w = (MainWindow)Application.Current.MainWindow;
            canvasScaleX = (int)drawingImage.Width / canvasResolutionX;
            canvasBitmap = new WriteableBitmap(canvasResolutionX, canvasResolutionY, dpiX, dpiY, PixelFormats.Bgra32, null);
            drawingImage.Source = canvasBitmap;

            // drawing events
            drawingImage.MouseMove += new MouseEventHandler(DrawingAreaMouseMoved);
            drawingImage.MouseLeftButtonDown += new MouseButtonEventHandler(DrawingLeftButtonDown);
            drawingImage.MouseRightButtonDown += new MouseButtonEventHandler(DrawingRightButtonDown);
            drawingImage.MouseDown += new MouseButtonEventHandler(DrawingMiddleButtonDown);
            w.MouseWheel += new MouseWheelEventHandler(drawingMouseWheel);
            drawingImage.MouseUp += new MouseButtonEventHandler(DrawingMouseUp);

            // build palette
            paletteImage = imgPalette;
            RenderOptions.SetBitmapScalingMode(paletteImage, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(paletteImage, EdgeMode.Aliased);
            w = (MainWindow)Application.Current.MainWindow;
            dpiX = 96;
            dpiY = 96;
            paletteScaleX = (int)paletteImage.Width / paletteResolutionX;
            paletteScaleY = (int)paletteImage.Height / paletteResolutionY;
            paletteBitmap = new WriteableBitmap(paletteResolutionX, paletteResolutionY, dpiX, dpiY, PixelFormats.Bgra32, null);
            paletteImage.Source = paletteBitmap;

            // palette events
            paletteImage.MouseLeftButtonDown += new MouseButtonEventHandler(PaletteLeftButtonDown);
            //paletteImage.MouseRightButtonDown += new MouseButtonEventHandler(PaletteRightButtonDown);

            // init
            LoadPalette();
            currentColorIndex = 5;
            currentColor = palette[currentColorIndex];
            UpdateCurrentColor();
        }


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
            int x = 0;
            int y = 0;
            for (y = 0; y < height; y++)
            {
                for (x = 0; x < width; x++)
                {
                    //Console.WriteLine(x + "," + y);
                    var c = pixels[x, y];
                    //                    Console.WriteLine(c.Red + "," + c.Green + "," + c.Blue);
                    palette[index++] = c;
                }
            }

            // put pixels on palette canvas

            x = y = 0;
            for (int i = 0, len = palette.Length; i < len; i++)
            {
                SetPixel(paletteBitmap, x, y, (int)palette[i].ColorBGRA);
                x = i % paletteResolutionX;
                y = (i % len) / paletteResolutionX;
            }
        }

        void SetPixel(WriteableBitmap bitmap, int x, int y, int color)
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


        // https://docs.microsoft.com/en-us/dotnet/api/system.windows.media.imaging.writeablebitmap?redirectedfrom=MSDN&view=netframework-4.7.2
        // The DrawPixel method updates the WriteableBitmap by using
        // unsafe code to write a pixel into the back buffer.
        void DrawPixel(int x, int y)
        {
            /*
            int x = (int)(e.GetPosition(drawingImage).X / canvasScaleX);
            int y = (int)(e.GetPosition(drawingImage).Y / canvasScaleX);
            if (x < 0 || x > canvasResolutionX - 1) return;
            if (y < 0 || y > canvasResolutionY - 1) return;*/

            if (x < 0 || x > canvasResolutionX - 1) return;
            if (y < 0 || y > canvasResolutionY - 1) return;


            //currentColorIndex = ++currentColorIndex % palette.Length;

            SetPixel(canvasBitmap, x, y, (int)currentColor.ColorBGRA);

            prevX = x;
            prevY = y;
        }

        void ErasePixel(MouseEventArgs e)
        {
            byte[] ColorData = { 0, 0, 0, 0 }; // B G R

            int x = (int)(e.GetPosition(drawingImage).X / canvasScaleX);
            int y = (int)(e.GetPosition(drawingImage).Y / canvasScaleX);
            if (x < 0 || x > canvasResolutionX - 1) return;
            if (y < 0 || y > canvasResolutionY - 1) return;

            Int32Rect rect = new Int32Rect(x, y, 1, 1);
            canvasBitmap.WritePixels(rect, ColorData, 4, 0);
        }

        void PickPalette(MouseEventArgs e)
        {
            byte[] ColorData = { 0, 0, 0, 0 }; // B G R !
            int x = (int)(e.GetPosition(paletteImage).X / paletteScaleX);
            int y = (int)(e.GetPosition(paletteImage).Y / paletteScaleY);
            if (x < 0 || x > paletteResolutionX - 1) return;
            if (y < 0 || y > paletteResolutionY - 1) return;
            currentColorIndex = y * paletteResolutionX + x + 1; // +1 for fix index magic number..
            currentColor = palette[currentColorIndex];
        }


        // return canvas pixel color from x,y
        unsafe PixelColor GetPixelColor(int x, int y)
        {
            var pix = new PixelColor();
            byte[] ColorData = { 0, 0, 0, 0 }; // B G R !
            IntPtr pBackBuffer = canvasBitmap.BackBuffer;
            byte* pBuff = (byte*)pBackBuffer.ToPointer();
            var b = pBuff[4 * x + (y * canvasBitmap.BackBufferStride)];
            var g = pBuff[4 * x + (y * canvasBitmap.BackBufferStride) + 1];
            var r = pBuff[4 * x + (y * canvasBitmap.BackBufferStride) + 2];
            var a = pBuff[4 * x + (y * canvasBitmap.BackBufferStride) + 3];
            pix.Red = r;
            pix.Green = g;
            pix.Blue = b;
            pix.Alpha = a;
            return pix;
        }

        void PaletteLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PickPalette(e);
            UpdateCurrentColor();
        }

        void UpdateCurrentColor()
        {
            var col = Color.FromArgb(currentColor.Alpha, currentColor.Red, currentColor.Green, currentColor.Blue);
            rectCurrentColor.Fill = new SolidColorBrush(col);
        }


        void DrawingRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            ErasePixel(e);
        }

        void DrawingMiddleButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                int x = (int)(e.GetPosition(drawingImage).X / canvasScaleX);
                int y = (int)(e.GetPosition(drawingImage).Y / canvasScaleX);

                currentColor = GetPixelColor(x, y);
                UpdateCurrentColor();
            }
        }



        void DrawingLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            int x = (int)(e.GetPosition(drawingImage).X / canvasScaleX);
            int y = (int)(e.GetPosition(drawingImage).Y / canvasScaleX);
            DrawPixel(x, y);
        }

        void DrawingMouseUp(object sender, MouseButtonEventArgs e)
        {
            // undo test
            undoBufferBitmap[++currentUndoIndex] = canvasBitmap.Clone();
            Console.WriteLine("save undo " + currentUndoIndex);
        }


        void DrawingAreaMouseMoved(object sender, MouseEventArgs e)
        {
            // update mousepos info
            int x = (int)(e.GetPosition(drawingImage).X / canvasScaleX);
            int y = (int)(e.GetPosition(drawingImage).Y / canvasScaleX);

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DrawPixel(x, y);
            }
            else if (e.RightButton == MouseButtonState.Pressed)
            {
                ErasePixel(e);
            }
            else if (e.MiddleButton == MouseButtonState.Pressed)
            {
                currentColor = GetPixelColor(x, y);
            }

            ShowMousePos(x, y);
            ShowMousePixelColor(x, y);
        }

        void ShowMousePos(int x, int y)
        {
            lblMousePos.Content = x + "," + y;
        }

        void ShowMousePixelColor(int x, int y)
        {
            var col = GetPixelColor(x, y);
            //lblPixelColor.Content = palette[currentColorIndex].Red + "," + palette[currentColorIndex].Green + "," + palette[currentColorIndex].Blue + "," + palette[currentColorIndex].Alpha;
            lblPixelColor.Content = col.Red + "," + col.Green + "," + col.Blue + "," + col.Alpha;
        }

        void drawingMouseWheel(object sender, MouseWheelEventArgs e)
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

        private void OnClearButton(object sender, RoutedEventArgs e)
        {
            ClearImage(canvasBitmap);
        }

        // clears bitmap by re-creating it
        void ClearImage(WriteableBitmap target)
        {
            canvasBitmap = new WriteableBitmap(canvasResolutionX, canvasResolutionY, dpiX, dpiY, PixelFormats.Bgra32, null);
            drawingImage.Source = canvasBitmap;
        }

        private void OnSaveButton(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();

            saveFileDialog.FileName = "pixel";
            saveFileDialog.DefaultExt = ".png";
            saveFileDialog.Filter = "PNG|*.png";
            UseDefaultExtAsFilterIndex(saveFileDialog);

            if (saveFileDialog.ShowDialog() == true)
            {
                FileStream stream = new FileStream(saveFileDialog.FileName, FileMode.Create);
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Interlace = PngInterlaceOption.On;
                encoder.Frames.Add(BitmapFrame.Create(canvasBitmap));
                encoder.Save(stream);
                stream.Close();
            }
        }

        // https://stackoverflow.com/a/6104319/5452781
        public static void UseDefaultExtAsFilterIndex(FileDialog dialog)
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

        private void OpacitySliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var slider = sender as Slider;
            opacity = (byte)slider.Value;
            currentColor.Alpha = opacity;
            // ... Set Window Title.
            //this.Title = "Value: " + value.ToString("0.0") + "/" + slider.Maximum;
        }

        private void OnUndoButtonDown(object sender, RoutedEventArgs e)
        {
            if (currentUndoIndex > 0)
            {
                canvasBitmap = undoBufferBitmap[--currentUndoIndex];
                Console.WriteLine("restore undo " + currentUndoIndex);
                imgCanvas.Source = canvasBitmap;
            }
        }
    } // class
} // namespace
