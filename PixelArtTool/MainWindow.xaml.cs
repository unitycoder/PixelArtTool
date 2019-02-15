using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static PixelArtTool.Tools;

namespace PixelArtTool
{
    public partial class MainWindow : Window, INotifyPropertyChanged
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

        // drawing lines
        bool firstPixel = true;
        int startPixelX = 0;
        int startPixelY = 0;
        bool verticalLine = false;
        bool horizontalLine = false;
        bool diagonalLine = false;
        int lockedX = 0;
        int lockedY = 0;

        // modes
        BlendMode blendMode;

        // clear buffers
        Int32Rect emptyRect;
        int bytesPerPixel;
        byte[] emptyPixels;
        int emptyStride;

        // settings
        double wheelSpeed = 0.05;

        private ToolMode _currentTool = ToolMode.Draw;
        public ToolMode CurrentTool
        {
            get
            {
                return _currentTool;
            }
            set
            {
                _currentTool = value;
                OnPropertyChanged();
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            Start();
        }

        void Start()
        {
            // needed for binding
            DataContext = this;

            // setup background grid
            gridImage = imgGrid;
            RenderOptions.SetBitmapScalingMode(gridImage, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(gridImage, EdgeMode.Aliased);
            w = (MainWindow)Application.Current.MainWindow;
            gridBitmap = new WriteableBitmap(canvasResolutionX, canvasResolutionY, dpiX, dpiY, PixelFormats.Bgra32, null);
            gridImage.Source = gridBitmap;
            DrawBackgroundGrid(gridBitmap, canvasResolutionX, canvasResolutionY, gridAlpha);

            // setup outline bitmap
            outlineImage = imgOutline;
            RenderOptions.SetBitmapScalingMode(outlineImage, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(outlineImage, EdgeMode.Aliased);
            w = (MainWindow)Application.Current.MainWindow;
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

            // init clear buffers
            emptyRect = new Int32Rect(0, 0, canvasBitmap.PixelWidth, canvasBitmap.PixelHeight);
            bytesPerPixel = canvasBitmap.Format.BitsPerPixel / 8;
            emptyPixels = new byte[emptyRect.Width * emptyRect.Height * bytesPerPixel];
            emptyStride = emptyRect.Width * bytesPerPixel;

            // setup preview area
            RenderOptions.SetBitmapScalingMode(imgPreview1x, BitmapScalingMode.NearestNeighbor);
            imgPreview1x.Source = canvasBitmap;
            RenderOptions.SetBitmapScalingMode(imgPreview2x, BitmapScalingMode.NearestNeighbor);
            imgPreview2x.Source = canvasBitmap;
            RenderOptions.SetBitmapScalingMode(imgPreview1xb, BitmapScalingMode.NearestNeighbor);
            imgPreview1xb.Source = canvasBitmap;
            RenderOptions.SetBitmapScalingMode(imgPreview2xb, BitmapScalingMode.NearestNeighbor);
            imgPreview2xb.Source = canvasBitmap;
            RenderOptions.SetBitmapScalingMode(imgPreview1xc, BitmapScalingMode.NearestNeighbor);
            imgPreview1xc.Source = canvasBitmap;
            RenderOptions.SetBitmapScalingMode(imgPreview2xc, BitmapScalingMode.NearestNeighbor);
            imgPreview2xc.Source = canvasBitmap;

            // drawing events
            drawingImage.MouseMove += new MouseEventHandler(DrawingAreaMouseMoved);
            drawingImage.MouseLeftButtonDown += new MouseButtonEventHandler(DrawingLeftButtonDown);
            drawingImage.MouseRightButtonDown += new MouseButtonEventHandler(DrawingRightButtonDown);
            drawingImage.MouseDown += new MouseButtonEventHandler(DrawingMiddleButtonDown);
            w.MouseWheel += new MouseWheelEventHandler(DrawingMouseWheel);
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
            palette = LoadPalette("pack://application:,,,/Resources/Palettes/aap-64-1x.png", paletteBitmap, paletteResolutionX, paletteResolutionY);
            currentColorIndex = 5;
            currentColor = palette[currentColorIndex];
            SetCurrentColorPreviewBox(rectCurrentColor, currentColor);
            ResetCurrentBrightnessPreview(currentColor);
        }


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
        } // drawpixel

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

            ResetCurrentBrightnessPreview(currentColor);
        }

        LinearGradientBrush myBrush;
        void ResetCurrentBrightnessPreview(PixelColor c)
        {
            hueLocation = 0.5;

            myBrush = new LinearGradientBrush();
            var c1 = new Color();
            c1.R = 0;
            c1.G = 0;
            c1.B = 0;
            c1.A = 255;
            var c2 = new Color();
            c2.R = c.Red;
            c2.G = c.Green;
            c2.B = c.Blue;
            c2.A = 255;
            var c3 = new Color();
            c3.R = 255;
            c3.G = 255;
            c3.B = 255;
            c3.A = 255;

            myBrush.StartPoint = new Point(0, 0);
            myBrush.EndPoint = new Point(1, 0);

            var g1 = new GradientStop(c1, 0.0);
            myBrush.GradientStops.Add(g1);

            var g2 = new GradientStop(c2, 0.5);
            myBrush.GradientStops.Add(g2);

            var g3 = new GradientStop(c3, 1);
            myBrush.GradientStops.Add(g3);

            rectCurrentHue.Fill = myBrush;

            // move hueline
            int offset = (int)(hueLocation * 253);
            lineCurrentHueLine.Margin = new Thickness(offset, 0, offset, 0);
        }

        // https://stackoverflow.com/a/39450207/5452781
        private static Color GetColorByOffset(GradientStopCollection collection, double offset)
        {
            GradientStop[] stops = collection.OrderBy(x => x.Offset).ToArray();
            if (offset <= 0) return stops[0].Color;
            if (offset >= 1) return stops[stops.Length - 1].Color;
            GradientStop left = stops[0], right = null;
            foreach (GradientStop stop in stops)
            {
                if (stop.Offset >= offset)
                {
                    right = stop;
                    break;
                }
                left = stop;
            }
            //Debug.Assert(right != null);
            offset = Math.Round((offset - left.Offset) / (right.Offset - left.Offset), 2);
            byte a = (byte)((right.Color.A - left.Color.A) * offset + left.Color.A);
            byte r = (byte)((right.Color.R - left.Color.R) * offset + left.Color.R);
            byte g = (byte)((right.Color.G - left.Color.G) * offset + left.Color.G);
            byte b = (byte)((right.Color.B - left.Color.B) * offset + left.Color.B);
            return Color.FromArgb(a, r, g, b);
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
            SetCurrentColorPreviewBox(rectCurrentColor, currentColor);
            ResetCurrentBrightnessPreview(currentColor);
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
                SetCurrentColorPreviewBox(rectCurrentColor, currentColor);
                ResetCurrentBrightnessPreview(currentColor);
            }
        }

        void DrawingLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // undo test
            undoBufferBitmap[currentUndoIndex++] = canvasBitmap.Clone();

            // FIXME if undobuffer enabled above, sometimes Exception thrown: 'System.IndexOutOfRangeException' in PixelArtTool.exe
            // An unhandled exception of type 'System.IndexOutOfRangeException' occurred in PixelArtTool.exe
            // Index was outside the bounds of the array.
            // Console.WriteLine(drawingImage);

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
                ResetCurrentBrightnessPreview(currentColor);
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

        double hueLocation = 0.5;
        void DrawingMouseWheel(object sender, MouseWheelEventArgs e)
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

            hueLocation += e.Delta < 0 ? -wheelSpeed : wheelSpeed;
            if (hueLocation < 0) hueLocation = 0;
            if (hueLocation > 1) hueLocation = 1;

            var c = GetColorByOffset(myBrush.GradientStops, hueLocation);
            var cc = new PixelColor();
            cc.Red = c.R;
            cc.Green = c.G;
            cc.Blue = c.B;
            cc.Alpha = 255;
            currentColor = cc;
            SetCurrentColorPreviewBox(rectCurrentColor, currentColor);
            //ResetCurrentBrightnessPreview(currentColor);

            // move hueline
            int offset = (int)(hueLocation * 253);
            lineCurrentHueLine.Margin = new Thickness(offset, 0, offset, 0);
        }

        private void OnClearButton(object sender, RoutedEventArgs e)
        {
            ClearImage(canvasBitmap, emptyRect, emptyPixels, emptyStride);
            UpdateOutline();
        }

        private void OnSaveButton(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();

            saveFileDialog.FileName = "pixel";
            saveFileDialog.DefaultExt = ".png";
            saveFileDialog.Filter = "PNG|*.png";
            UseDefaultExtensionAsFilterIndex(saveFileDialog);

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
                case Key.I: // TEST global color picker
                    CustomPoint cursor;
                    GetCursorPos(out cursor);
                    var c1 = Win32GetScreenPixel((int)cursor.X, (int)cursor.Y);
                    var c2 = new PixelColor();
                    c2.Alpha = c1.A;
                    c2.Red = c1.R;
                    c2.Green = c1.G;
                    c2.Blue = c1.B;
                    currentColor = c2;
                    rectCurrentColor.Fill = new SolidColorBrush(Color.FromArgb(c2.Alpha, c2.Red, c2.Green, c2.Blue));
                    //                    Console.WriteLine(cursor.X + "," + cursor.Y + " = " + c1);
                    break;
                case Key.X: // swap current/secondary colors
                    var tempcolor = rectCurrentColor.Fill;
                    rectCurrentColor.Fill = rectSecondaryColor.Fill;
                    rectSecondaryColor.Fill = tempcolor;
                    // TODO move to converter
                    var c = new PixelColor();
                    var t = ((SolidColorBrush)rectCurrentColor.Fill).Color;
                    c.Red = t.R;
                    c.Green = t.G;
                    c.Blue = t.B;
                    c.Alpha = t.A;
                    currentColor = c;
                    break;
                case Key.B: // brush
                    CurrentTool = ToolMode.Draw;
                    break;
                case Key.F: // floodfill
                    CurrentTool = ToolMode.Fill;
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
                CopyBitmapPixels(undoBufferBitmap[--currentUndoIndex], canvasBitmap);
            }
        }

        void CopyBitmapPixels(WriteableBitmap source, WriteableBitmap target)
        {
            byte[] data = new byte[source.BackBufferStride * source.PixelHeight];
            source.CopyPixels(data, source.BackBufferStride, 0);
            target.WritePixels(new Int32Rect(0, 0, source.PixelWidth, source.PixelHeight), data, source.BackBufferStride, 0);
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

        private void OnToolChanged(object sender, RoutedEventArgs e)
        {
            //string tag = (string)((RadioButton)sender).Tag;
            //Enum.TryParse(tag, out currentTool);
        }

        // https://github.com/crclayton/WPF-DataBinding-Example
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        void BitmapFlip(bool horizontal)
        {
            // clone canvas, FIXME not really needed..could just copy pixels to array or backbuffer directly
            var tempCanvasBitmap = new WriteableBitmap(canvasResolutionX, canvasResolutionY, dpiX, dpiY, PixelFormats.Bgra32, null);
            tempCanvasBitmap = canvasBitmap.Clone();
            for (int x = 0; x < canvasResolutionX; x++)
            {
                for (int y = 0; y < canvasResolutionY; y++)
                {
                    int xx = horizontal ? (canvasResolutionX - x - 1) : x;
                    int yy = !horizontal ? (canvasResolutionY - y - 1) : y;
                    var c = GetPixelColor(xx, yy, tempCanvasBitmap);
                    SetPixel(canvasBitmap, x, y, (int)c.ColorBGRA);
                }
            }
        }

        private void OnFlipXButtonDown(object sender, RoutedEventArgs e)
        {
            BitmapFlip(horizontal: true);
        }

        private void OnFlipYButtonDown(object sender, RoutedEventArgs e)
        {
            BitmapFlip(horizontal: false);
        }

        private void OnLoadPaletteButton(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                palette = LoadPalette(openFileDialog.FileName, paletteBitmap, paletteResolutionX, paletteResolutionY);
            }
        }

        private void chkOutline_Click(object sender, RoutedEventArgs e)
        {
            if (chkOutline.IsChecked == true)
            {
                UpdateOutline();
            }
            else // clear
            {
                ClearImage(outlineBitmap, emptyRect, emptyPixels, emptyStride);
            }
        }

        private void rectHueBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            CustomPoint cursor;
            GetCursorPos(out cursor);
            var c = Win32GetScreenPixel((int)cursor.X, (int)cursor.Y);
            //Console.WriteLine("color:"+c);
            var f = rectSaturation.Fill;

            // build hue gradient
            LinearGradientBrush myBrush = new LinearGradientBrush();
            var c1 = new Color();
            c1.R = 255;
            c1.G = 255;
            c1.B = 255;
            c1.A = 255;
            var c2 = new Color();
            c2.R = c.R;
            c2.G = c.G;
            c2.B = c.B;
            c2.A = 255;
            myBrush.StartPoint = new Point(0, 0);
            myBrush.EndPoint = new Point(1, 0);

            var g1 = new GradientStop(c1, 0.0);
            myBrush.GradientStops.Add(g1);

            var g2 = new GradientStop(c2, 1.0);
            myBrush.GradientStops.Add(g2);
            rectSaturation.Fill = myBrush;

            // set opacity mask
            var opacityBrush = new LinearGradientBrush();
            opacityBrush.StartPoint = new Point(0, 0);
            opacityBrush.EndPoint = new Point(0, 1);
            var g1b = new GradientStop(c1, 0.0);
            opacityBrush.GradientStops.Add(g1b);
            c2.A = 0;
            c2.R = 0;
            c2.G = 0;
            c2.B = 0;
            var g2b = new GradientStop(c2, 1.0);
            opacityBrush.GradientStops.Add(g2b);
            rectSaturation.OpacityMask = opacityBrush;
        }

        private void OnLevelSaturationMouseDown(object sender, MouseButtonEventArgs e)
        {
            CustomPoint cursor;
            GetCursorPos(out cursor);
            var c1 = Win32GetScreenPixel((int)cursor.X, (int)cursor.Y);
            var c2 = new PixelColor();
            c2.Alpha = c1.A;
            c2.Red = c1.R;
            c2.Green = c1.G;
            c2.Blue = c1.B;
            currentColor = c2;
            rectCurrentColor.Fill = new SolidColorBrush(Color.FromArgb(c2.Alpha, c2.Red, c2.Green, c2.Blue));
            ResetCurrentBrightnessPreview(currentColor);
        }
    } // class

} // namespace
