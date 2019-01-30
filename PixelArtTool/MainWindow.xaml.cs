using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PixelArtTool
{

    public enum BlendMode : byte
    {
        Default = 0,
        Additive = 1
    }

    public enum ToolMode
    {
        Draw,
        Fill
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        WriteableBitmap canvasBitmap;
        WriteableBitmap gridBitmap;
        WriteableBitmap outlineBitmap;
        WriteableBitmap paletteBitmap;
        Window w;

        Image drawingImage;
        Image gridImage;
        Image outlineImage;
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

        byte gridAlpha = 16;

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

        // modes
        BlendMode blendMode;

        // TEST property binding
        private ToolMode myVar = ToolMode.Draw;
        public ToolMode CurrentTool
        {
            get
            {
                Console.WriteLine("get:"+myVar);
                return myVar; }
            set {
                Console.WriteLine("set:"+value);
                myVar = value; }
        }


        //public MyLovelyEnum VeryLovelyEnum { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            Start();
        }

        void Start()
        {
            DataContext = this;

            // setup background grid
            gridImage = imgGrid;
            RenderOptions.SetBitmapScalingMode(gridImage, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(gridImage, EdgeMode.Aliased);
            w = (MainWindow)Application.Current.MainWindow;
            //var gridScaleX = (int)gridImage.Width / canvasResolutionX;
            gridBitmap = new WriteableBitmap(canvasResolutionX, canvasResolutionY, dpiX, dpiY, PixelFormats.Bgra32, null);
            gridImage.Source = gridBitmap;
            DrawBackgroundGrid();

            // setup outline bitmap
            outlineImage = imgOutline;
            RenderOptions.SetBitmapScalingMode(outlineImage, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(outlineImage, EdgeMode.Aliased);
            w = (MainWindow)Application.Current.MainWindow;
            //var gridScaleX = (int)gridImage.Width / canvasResolutionX;
            outlineBitmap = new WriteableBitmap(canvasResolutionX, canvasResolutionY, dpiX, dpiY, PixelFormats.Bgra32, null);
            outlineImage.Source = outlineBitmap;


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

        // used in drawing
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
            return result;
        }

        bool firstPixel = true;
        int startPixelX = 0;
        int startPixelY = 0;
        bool verticalLine = false;
        bool horizontalLine = false;
        bool diagonalLine = false;
        int lockedX = 0;
        int lockedY = 0;

        // https://docs.microsoft.com/en-us/dotnet/api/system.windows.media.imaging.writeablebitmap?redirectedfrom=MSDN&view=netframework-4.7.2
        // The DrawPixel method updates the WriteableBitmap by using
        // unsafe code to write a pixel into the back buffer.
        void DrawPixel(int x, int y)
        {
            if (x < 0 || x > canvasResolutionX - 1) return;
            if (y < 0 || y > canvasResolutionY - 1) return;

            // is using straight lines
            if (leftShiftDown == true)
            {
                // get first pixel, to measure direction
                if (firstPixel == true)
                {
                    startPixelX = x;
                    startPixelY = y;
                    firstPixel = false;
                }
                else // already drew before
                {
                    // have detected linemode
                    if (horizontalLine == false && verticalLine == false && diagonalLine == false)
                    {
                        // vertical
                        if (x == startPixelX && y != startPixelY)
                        {
                            verticalLine = true;
                            lockedX = x;
                        }
                        // horizontal
                        else if (y == startPixelY && x != startPixelX)
                        {
                            horizontalLine = true;
                            lockedY = y;
                        }
                        // diagonal
                        else if (y != startPixelY && x != startPixelX)
                        {
                            diagonalLine = true;
                        }
                    }

                    // lock coordinates if straight lines
                    if (verticalLine == true)
                    {
                        x = lockedX;
                    }
                    else if (horizontalLine == true)
                    {
                        y = lockedY;
                    }
                    else if (diagonalLine == true)
                    {
                        // force diagonal
                        int xx = x - startPixelX;
                        int yy = y - startPixelY;

                        // stop drawing, if not in diagonal cell
                        if (Math.Abs(xx) - Math.Abs(yy) != 0)
                        {
                            return;
                        }
                    }
                }
            }
            else // left shift not down
            {
                verticalLine = false;
                horizontalLine = false;
                diagonalLine = false;
                firstPixel = true;
            }

            PixelColor draw = new PixelColor();

            switch (blendMode)
            {
                case BlendMode.Default: // replace
                    draw = currentColor;
                    break;
                case BlendMode.Additive:
                    // get old color from undo buffer
                    var oc = GetPixelColor(x, y, undoBufferBitmap[currentUndoIndex]);
                    // mix colors ADDITIVE mode
                    int r = (int)(oc.Red + currentColor.Red * (opacity / (float)255));
                    int g = (int)(oc.Green + currentColor.Green * (opacity / (float)255));
                    int b = (int)(oc.Blue + currentColor.Blue * (opacity / (float)255));
                    draw.Red = ClampToByte(r);
                    draw.Green = ClampToByte(g);
                    draw.Blue = ClampToByte(b);
                    draw.Alpha = opacity;
                    break;
                default:
                    break;
            }

            // draw
            SetPixel(canvasBitmap, x, y, (int)draw.ColorBGRA);

            prevX = x;
            prevY = y;
        }

        byte Clamp(byte n)
        {
            return n <= 255 ? n : (byte)Math.Min(n, (byte)255);
        }

        byte ClampToByte(int n)
        {
            var r = n <= 255 ? n : (byte)Math.Min(n, (byte)255);
            return (byte)r;
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

        void ErasePixel(int x, int y)
        {
            byte[] ColorData = { 0, 0, 0, 0 }; // B G R

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
        unsafe PixelColor GetPixel(int x, int y)
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

        unsafe PixelColor GetPixelColor(int x, int y, WriteableBitmap source)
        {
            var pix = new PixelColor();
            byte[] ColorData = { 0, 0, 0, 0 }; // B G R !
            IntPtr pBackBuffer = source.BackBuffer;
            byte* pBuff = (byte*)pBackBuffer.ToPointer();
            var b = pBuff[4 * x + (y * source.BackBufferStride)];
            var g = pBuff[4 * x + (y * source.BackBufferStride) + 1];
            var r = pBuff[4 * x + (y * source.BackBufferStride) + 2];
            var a = pBuff[4 * x + (y * source.BackBufferStride) + 3];
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

                currentColor = GetPixel(x, y);
                UpdateCurrentColor();
            }
        }



        void DrawingLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // undo test
            undoBufferBitmap[currentUndoIndex++] = canvasBitmap.Clone();
            //Console.WriteLine("save undo " + currentUndoIndex);

            int x = (int)(e.GetPosition(drawingImage).X / canvasScaleX);
            int y = (int)(e.GetPosition(drawingImage).Y / canvasScaleX);


            switch (CurrentTool)
            {
                case ToolMode.Draw:
                    DrawPixel(x, y);
                    // mirror
                    if (chkMirrorX.IsChecked == true)
                    {
                        DrawPixel(canvasResolutionX - x - 1, y);
                    }
                    break;
                case ToolMode.Fill:
                    FloodFill(x, y, (int)currentColor.ColorBGRA);
                    break;
                default:
                    break;
            }

            if (chkOutline.IsChecked == true)
            {
                UpdateOutline();
            }

        }

        void DrawingMouseUp(object sender, MouseButtonEventArgs e)
        {
        }


        void DrawingAreaMouseMoved(object sender, MouseEventArgs e)
        {
            // update mousepos info
            int x = (int)(e.GetPosition(drawingImage).X / canvasScaleX);
            int y = (int)(e.GetPosition(drawingImage).Y / canvasScaleX);

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                switch (CurrentTool)
                {
                    case ToolMode.Draw:
                        DrawPixel(x, y);
                        // mirror
                        if (chkMirrorX.IsChecked == true)
                        {
                            DrawPixel(canvasResolutionX - x - 1, y);
                        }
                        break;
                    case ToolMode.Fill:
                        FloodFill(x, y, (int)currentColor.ColorBGRA);
                        break;
                    default:
                        break;
                }

            }
            else if (e.RightButton == MouseButtonState.Pressed)
            {
                ErasePixel(x, y);
                // mirror
                if (chkMirrorX.IsChecked == true)
                {
                    ErasePixel(canvasResolutionX - x - 1, y);
                }
            }
            else if (e.MiddleButton == MouseButtonState.Pressed)
            {
                currentColor = GetPixel(x, y);
            }

            ShowMousePos(x, y);
            ShowMousePixelColor(x, y);
            // outline
            if (chkOutline.IsChecked == true)
            {
                UpdateOutline();
            }

        }

        void ShowMousePos(int x, int y)
        {
            lblMousePos.Content = x + "," + y;
        }

        void ShowMousePixelColor(int x, int y)
        {
            var col = GetPixel(x, y);
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
            CallUndo();
        }

        private void OnModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var s = sender as ComboBox;
            blendMode = (BlendMode)s.SelectedIndex;
        }

        bool leftShiftDown = false;

        // if key is pressed down globally
        void OnKeyDown(object sender, KeyEventArgs e)
        {
            // TODO: add tool shortcut keys
            switch (e.Key)
            {
                case Key.B: // brush
                    CurrentTool = ToolMode.Draw;
                    Console.WriteLine("drawmode");
                    break;
                case Key.F: // floodfill
                    CurrentTool = ToolMode.Fill;
                    Console.WriteLine("fillmode");
                    break;
                case Key.LeftShift: // left shift
                    lblToolInfo.Content = "Straight Lines";
                    leftShiftDown = true;
                    break;
                default:
                    break;
            }
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.LeftShift:
                    lblToolInfo.Content = "";
                    leftShiftDown = false;
                    verticalLine = false;
                    horizontalLine = false;
                    diagonalLine = false;
                    firstPixel = true;
                    break;
                default:
                    break;
            }
        }

        private void CallUndo()
        {
            if (currentUndoIndex > 0)
            {
                canvasBitmap = undoBufferBitmap[--currentUndoIndex];
                imgCanvas.Source = canvasBitmap;
            }
        }

        public void Executed_Undo(object sender, ExecutedRoutedEventArgs e)
        {
            CallUndo();
        }

        public void CanExecute_Undo(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        void FloodFill(int x, int y, int fillColor)
        {
            // get hit color pixel
            var hitColor = GetPixel(x, y);

            // if same as current color, exit
            if (hitColor.ColorBGRA == fillColor) return;

            SetPixel(canvasBitmap, x, y, (int)hitColor.ColorBGRA);

            List<int> ptsx = new List<int>();
            ptsx.Add(x);
            List<int> ptsy = new List<int>();
            ptsy.Add(y);

            int maxLoop = canvasResolutionX * canvasResolutionY + canvasResolutionX;
            while (ptsx.Count > 0 && maxLoop > 0)
            {
                maxLoop--;

                if (ptsx[0] - 1 >= 0)
                {
                    if (GetPixel(ptsx[0] - 1, ptsy[0]).ColorBGRA == hitColor.ColorBGRA)
                    {
                        ptsx.Add(ptsx[0] - 1); ptsy.Add(ptsy[0]);
                        SetPixel(canvasBitmap, ptsx[0] - 1, ptsy[0], fillColor);
                    }
                }

                if (ptsy[0] - 1 >= 0)
                {
                    if (GetPixel(ptsx[0], ptsy[0] - 1).ColorBGRA == hitColor.ColorBGRA)
                    {
                        ptsx.Add(ptsx[0]); ptsy.Add(ptsy[0] - 1);
                        SetPixel(canvasBitmap, ptsx[0], ptsy[0] - 1, fillColor);
                    }
                }

                if (ptsx[0] + 1 < canvasResolutionX)
                {
                    if (GetPixel(ptsx[0] + 1, ptsy[0]).ColorBGRA == hitColor.ColorBGRA)
                    {
                        ptsx.Add(ptsx[0] + 1); ptsy.Add(ptsy[0]);
                        SetPixel(canvasBitmap, ptsx[0] + 1, ptsy[0], fillColor);
                    }
                }

                if (ptsy[0] + 1 < canvasResolutionY)
                {
                    if (GetPixel(ptsx[0], ptsy[0] + 1).ColorBGRA == hitColor.ColorBGRA)
                    {
                        ptsx.Add(ptsx[0]); ptsy.Add(ptsy[0] + 1);
                        SetPixel(canvasBitmap, ptsx[0], ptsy[0] + 1, fillColor);
                    }
                }
                ptsx.RemoveAt(0);
                ptsy.RemoveAt(0);
            } // while can floodfill

        } // floodfill


        void DrawBackgroundGrid()
        {
            PixelColor c = new PixelColor();
            for (int x = 0; x < canvasResolutionX; x++)
            {
                for (int y = 0; y < canvasResolutionY; y++)
                {
                    c.Alpha = gridAlpha;
                    byte v = (byte)(((x % 2) == (y % 2)) ? 255 : 0);
                    c.Red = v;
                    c.Green = v;
                    c.Blue = v;
                    SetPixel(gridBitmap, x, y, (int)c.ColorBGRA);
                }
            }
        }

        // draw automatic outlines
        void UpdateOutline()
        {
            PixelColor c = new PixelColor();
            for (int x = 0; x < canvasResolutionX; x++)
            {
                for (int y = 0; y < canvasResolutionY; y++)
                {
                    int centerPix = GetPixelColor(x, y, canvasBitmap).Alpha > 0 ? 1 : 0;

                    int yy = (y + 1) > (canvasResolutionY - 1) ? y : y;
                    int upPix = GetPixelColor(x, yy + 1, canvasBitmap).Alpha > 0 ? 1 : 0;
                    int xx = (x + 1) > (canvasResolutionX - 1) ? x : x + 1;
                    int rightPix = GetPixelColor(xx, y, canvasBitmap).Alpha > 0 ? 1 : 0;
                    yy = (y - 1) < 0 ? y : y - 1;
                    int downPix = GetPixelColor(x, yy, canvasBitmap).Alpha > 0 ? 1 : 0;
                    xx = (x - 1) < 0 ? x : x - 1;
                    int leftPix = GetPixelColor(xx, y, canvasBitmap).Alpha > 0 ? 1 : 0;

                    /*
                    // decrease count if black color founded
                    if (!automaticOutlineForBlack)
                    {
                        if (upPix > 0) upPix -= canvas.GetPixel(x, y + 1).grayscale == 0 ? 1 : 0;
                        if (rightPix > 0) rightPix -= canvas.GetPixel(x + 1, y).grayscale == 0 ? 1 : 0;
                        if (downPix > 0) downPix -= canvas.GetPixel(x, y - 1).grayscale == 0 ? 1 : 0;
                        if (leftPix > 0) leftPix -= canvas.GetPixel(x - 1, y).grayscale == 0 ? 1 : 0;
                    }*/

                    c.Red = 0;
                    c.Green = 0;
                    c.Blue = 0;
                    c.Alpha = 0;

                    int neighbourAlphas = upPix + rightPix + downPix + leftPix;
                    if (neighbourAlphas > 0)
                    {
                        if (centerPix == 0)
                        {
                            c.Alpha = 255;
                        }
                        else
                        {
                            c.Alpha = 0;
                        }
                    }
                    else
                    {
                        c.Alpha = 0;
                    }

                    SetPixel(outlineBitmap, x, y, (int)c.ColorBGRA);
                }
            }
        } // UpdateOutline()

        void OnScrollButtonUpClicked(object sender, RoutedEventArgs e)
        {
            ScrollCanvas(0, -1);
        }
        void OnScrollButtonDownClicked(object sender, RoutedEventArgs e)
        {
            ScrollCanvas(0, 1);
        }
        void OnScrollButtonRightClicked(object sender, RoutedEventArgs e)
        {
            ScrollCanvas(1, 0);
        }
        void OnScrollButtonLeftClicked(object sender, RoutedEventArgs e)
        {
            ScrollCanvas(-1, 0);
        }

        void ScrollCanvas(int sx, int sy)
        {
            // clone canvas, FIXME not really needed..could just copy pixels to array or so..
            var tempCanvasBitmap = new WriteableBitmap(canvasResolutionX, canvasResolutionY, dpiX, dpiY, PixelFormats.Bgra32, null);
            tempCanvasBitmap = canvasBitmap.Clone();

            // TODO add wrap or clamp option?

            for (int x = 0; x < canvasResolutionX; x++)
            {
                for (int y = 0; y < canvasResolutionY; y++)
                {
                    var c = GetPixelColor(x, y, tempCanvasBitmap);
                    int xx = Repeat(x + sx, canvasResolutionX);
                    int yy = Repeat(y + sy, canvasResolutionY);
                    SetPixel(canvasBitmap, xx, yy, (int)c.ColorBGRA);
                }
            }
        }

        int Repeat(int val, int max)
        {
            int result = val % max;
            if (result < 0) result += max;
            return result;
        }

        private void OnToolChanged(object sender, RoutedEventArgs e)
        {
            //string tag = (string)((RadioButton)sender).Tag;
            //Enum.TryParse(tag, out currentTool);
        }



    } // class

    public class EnumBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            Console.WriteLine("valueA:"+ value);
            Console.WriteLine("parameter:" + parameter);
            return value?.Equals(parameter);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            Console.WriteLine("valueB:" + value);
            Console.WriteLine("parameter:" + parameter);
            return value?.Equals(true) == true ? parameter : Binding.DoNothing;
        }
    }

} // namespace
