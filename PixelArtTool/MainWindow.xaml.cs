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
        string windowTitle = "";

        WriteableBitmap canvasBitmap;
        WriteableBitmap gridBitmap;
        WriteableBitmap outlineBitmap;
        WriteableBitmap paletteBitmap;

        Window w;

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

        // simple undo
        Stack<WriteableBitmap> undoStack = new Stack<WriteableBitmap>();
        Stack<WriteableBitmap> redoStack = new Stack<WriteableBitmap>();
        WriteableBitmap currentUndoItem;

        // colors
        PixelColor currentColor;
        PixelColor[] palette;

        int currentColorIndex = 0;
        byte opacity = 255;

        // mouse
        int prevX;
        int prevY;

        // drawing lines
        bool firstPixel = true;
        int startPixelX = 0;
        int startPixelY = 0;
        bool verticalLine = false;
        bool horizontalLine = false;
        bool diagonalLine = false;
        int lockedX = 0;
        int lockedY = 0;

        // smart fill with double click
        bool wasDoubleClick = false;
        ToolMode previousToolMode = ToolMode.Draw;
        PixelColor previousPixelColor;
        PixelColor previousColor;

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
            get { return _currentTool; }
            set { _currentTool = value; OnPropertyChanged(); }
        }

        // files
        string saveFile = null;
        private bool _isModified;

        public bool IsModified
        {
            get { return _isModified; }
            set
            {
                _isModified = value;
                // add * mark to file if modified
                if (_isModified == true)
                {
                    if (window.Title.IndexOf("*") == -1)
                    {
                        window.Title = window.Title + "*";
                    }
                }
                else // not modified, remove mark
                {
                    if (window.Title.IndexOf("*") > -1)
                    {
                        window.Title = window.Title.Replace("*", "");
                    }
                }
            }
        }


        LinearGradientBrush currentBrightnessBrushGradient;
        double hueIndicatorLocation = 0.5;


        public MainWindow()
        {
            InitializeComponent();
            Start();
        }

        void Start()
        {
            w = (MainWindow)Application.Current.MainWindow;
            windowTitle = w.Title;

            // needed for binding
            DataContext = this;

            // setup background grid
            gridBitmap = new WriteableBitmap(canvasResolutionX, canvasResolutionY, dpiX, dpiY, PixelFormats.Bgra32, null);
            gridImage.Source = gridBitmap;
            DrawBackgroundGrid(gridBitmap, canvasResolutionX, canvasResolutionY, gridAlpha);

            // build drawing area
            canvasBitmap = new WriteableBitmap(canvasResolutionX, canvasResolutionY, dpiX, dpiY, PixelFormats.Bgra32, null);
            drawingImage.Source = canvasBitmap;
            canvasScaleX = (int)drawingImage.Width / canvasResolutionX;

            // setup outline bitmap
            outlineBitmap = new WriteableBitmap(canvasResolutionX, canvasResolutionY, dpiX, dpiY, PixelFormats.Bgra32, null);
            outlineImage.Source = outlineBitmap;

            // init clear buffers
            emptyRect = new Int32Rect(0, 0, canvasBitmap.PixelWidth, canvasBitmap.PixelHeight);
            bytesPerPixel = canvasBitmap.Format.BitsPerPixel / 8;
            emptyPixels = new byte[emptyRect.Width * emptyRect.Height * bytesPerPixel];
            emptyStride = emptyRect.Width * bytesPerPixel;

            // setup preview images
            imgPreview1x.Source = canvasBitmap;
            imgPreview2x.Source = canvasBitmap;
            imgPreview1xb.Source = canvasBitmap;
            imgPreview2xb.Source = canvasBitmap;
            imgPreview1xc.Source = canvasBitmap;
            imgPreview2xc.Source = canvasBitmap;

            // build palette
            paletteScaleX = (int)paletteImage.Width / paletteResolutionX;
            paletteScaleY = (int)paletteImage.Height / paletteResolutionY;
            paletteBitmap = new WriteableBitmap(paletteResolutionX, paletteResolutionY, dpiX, dpiY, PixelFormats.Bgra32, null);
            paletteImage.Source = paletteBitmap;

            // init
            palette = LoadPalette("pack://application:,,,/Resources/Palettes/aap-64-1x.png", paletteBitmap, paletteResolutionX, paletteResolutionY);
            currentColorIndex = 5;
            currentColor = palette[currentColorIndex];
            SetCurrentColorPreviewBox(rectCurrentColor, currentColor);
            ResetCurrentBrightnessPreview(currentColor);

            // set pixel box size based on resolution
            //rectPixelPos.Width = 16 * (16 / canvasResolutionX);
            //rectPixelPos.Height = 16 * (16 / canvasResolutionY);
            // r 8  = 32
            // r 16 = 16
            // r 32 = 8

            // hide some objects (that are visible at start to keep it easy to edit form)
            lineSymmetryXpositionA.Visibility = Visibility.Hidden;
            lineSymmetryXpositionB.Visibility = Visibility.Hidden;
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
                    /*
                    // get old color from undo buffer
                    // TODO add undo back
                    //var oc = GetPixelColor(x, y, undoBufferBitmap.Pop());
                    // mix colors ADDITIVE mode
                    int r = (int)(oc.Red + currentColor.Red * (opacity / (float)255));
                    int g = (int)(oc.Green + currentColor.Green * (opacity / (float)255));
                    int b = (int)(oc.Blue + currentColor.Blue * (opacity / (float)255));
                    draw.Red = ClampToByte(r);
                    draw.Green = ClampToByte(g);
                    draw.Blue = ClampToByte(b);
                    draw.Alpha = opacity;
                    */
                    break;
                default:
                    break;
            }

            // draw
            SetPixel(canvasBitmap, x, y, (int)draw.ColorBGRA);

            prevX = x;
            prevY = y;
        } // drawpixel

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

            currentColorIndex = y * paletteResolutionX + x;
            if (currentColorIndex > palette.Length) currentColorIndex--;
            currentColor = palette[currentColorIndex];
            ResetCurrentBrightnessPreview(currentColor);
        }

        void ResetCurrentBrightnessPreview(PixelColor c)
        {
            hueIndicatorLocation = 0.5;

            currentBrightnessBrushGradient = new LinearGradientBrush();
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

            currentBrightnessBrushGradient.StartPoint = new Point(0, 0);
            currentBrightnessBrushGradient.EndPoint = new Point(1, 0);

            var g1 = new GradientStop(c1, 0.0);
            currentBrightnessBrushGradient.GradientStops.Add(g1);

            var g2 = new GradientStop(c2, 0.5);
            currentBrightnessBrushGradient.GradientStops.Add(g2);

            var g3 = new GradientStop(c3, 1);
            currentBrightnessBrushGradient.GradientStops.Add(g3);

            rectCurrentHue.Fill = currentBrightnessBrushGradient;

            // move hueline
            int offset = (int)(hueIndicatorLocation * 253);
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
            RegisterUndo();

            int x = (int)(e.GetPosition(drawingImage).X / canvasScaleX);
            int y = (int)(e.GetPosition(drawingImage).Y / canvasScaleX);

            ErasePixel(x, y);

            if (chkMirrorX.IsChecked == true)
            {
                ErasePixel(canvasResolutionX - x, y);
            }
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


        // clicked, but not moved
        void DrawingLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            int x = (int)(e.GetPosition(drawingImage).X / canvasScaleX);
            int y = (int)(e.GetPosition(drawingImage).Y / canvasScaleX);

            // take current bitmap as currentimage
            RegisterUndo();

            // check for double click
            if (e.ClickCount == 2)
            {
                previousToolMode = CurrentTool;
                CurrentTool = ToolMode.Fill;
                wasDoubleClick = true;
            }
            else // keep old color
            {
                previousPixelColor = GetPixel(x, y);
            }


            switch (CurrentTool)
            {
                case ToolMode.Draw:
                    DrawPixel(x, y);
                    // mirror
                    if (chkMirrorX.IsChecked == true)
                    {
                        DrawPixel(canvasResolutionX - x, y);
                    }
                    break;
                case ToolMode.Fill:
                    // NOTE: doesnt work with single pixel area.. because nothing to fill
                    if (wasDoubleClick == true)
                    {
                        // remove previous pixel by using old color (could take from undo also..)
                        // keep backup
                        previousColor = currentColor;
                        // set current to previous color
                        currentColor = previousPixelColor;
                        DrawPixel(x, y);
                        // restore color
                        currentColor = previousColor;
                    }

                    FloodFill(x, y, (int)currentColor.ColorBGRA);
                    if (chkMirrorX.IsChecked == true)
                    {
                        FloodFill(canvasResolutionX - x, y, (int)currentColor.ColorBGRA);
                    }
                    break;
                default:
                    break;
            }

            if (wasDoubleClick == true)
            {
                wasDoubleClick = false;
                CurrentTool = previousToolMode;
            }

            if (chkOutline.IsChecked == true)
            {
                UpdateOutline();
            }
        } // DrawingLeftButtonDown

        // save undo state
        void RegisterUndo()
        {
            currentUndoItem = canvasBitmap.Clone();
            undoStack.Push(currentUndoItem);
            redoStack.Clear();
            IsModified = true;
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
                            DrawPixel(canvasResolutionX - x, y);
                        }
                        break;
                    case ToolMode.Fill:
                        FloodFill(x, y, (int)currentColor.ColorBGRA);
                        if (chkMirrorX.IsChecked == true)
                        {
                            FloodFill(canvasResolutionX - x, y, (int)currentColor.ColorBGRA);
                        }
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
                    ErasePixel(canvasResolutionX - x, y);
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

            // snap preview rectangle to grid
            var left = x * canvasScaleX;
            var top = y * canvasScaleX;
            // NOTE: this causes palette pixels to distort/move?
            rectPixelPos.Margin = new Thickness(89 + left, 50 + top, 0, 0);

        } // drawingareamousemoved

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

        void WindowMouseWheel(object sender, MouseWheelEventArgs e)
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

            hueIndicatorLocation += e.Delta < 0 ? -wheelSpeed : wheelSpeed;
            if (hueIndicatorLocation < 0) hueIndicatorLocation = 0;
            if (hueIndicatorLocation > 1) hueIndicatorLocation = 1;

            var c = GetColorByOffset(currentBrightnessBrushGradient.GradientStops, hueIndicatorLocation);
            var cc = new PixelColor();
            cc.Red = c.R;
            cc.Green = c.G;
            cc.Blue = c.B;
            cc.Alpha = 255;
            currentColor = cc;
            SetCurrentColorPreviewBox(rectCurrentColor, currentColor);
            //ResetCurrentBrightnessPreview(currentColor);

            // move hueline
            int offset = (int)(hueIndicatorLocation * 253);
            lineCurrentHueLine.Margin = new Thickness(offset, 0, offset, 0);
        }

        private void OnClearButton(object sender, RoutedEventArgs e)
        {
            RegisterUndo();
            ClearImage(canvasBitmap, emptyRect, emptyPixels, emptyStride);
            UpdateOutline();
            // reset title
            window.Title = windowTitle;
            saveFile = null;
        }

        // if unsaved, this is same as save as.., if already saved, then overwrite current
        private void OnSaveButton(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.FileName = "pixel";
            saveFileDialog.DefaultExt = ".png";
            saveFileDialog.Filter = "PNG|*.png";
            UseDefaultExtensionAsFilterIndex(saveFileDialog);

            // save to current file
            if (saveFile != null)// || doSaveAs==true)
            {
                SaveImageAsPng(saveFile);
                IsModified = false;
            }
            else // save as
            {
                if (saveFileDialog.ShowDialog() == true)
                {
                    SaveImageAsPng(saveFileDialog.FileName);
                    // update window title
                    window.Title = windowTitle + " - " + saveFileDialog.FileName;
                    saveFile = saveFileDialog.FileName;
                    IsModified = false;
                }
            }

        }

        void SaveImageAsPng(string file)
        {
            FileStream stream = new FileStream(file, FileMode.Create);
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Interlace = PngInterlaceOption.On;
            encoder.Frames.Add(BitmapFrame.Create(canvasBitmap));
            encoder.Save(stream);
            stream.Close();
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
            DoUndo();
        }

        private void OnRedoButtonDown(object sender, RoutedEventArgs e)
        {
            DoRedo();
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


        // restore to previous bitmap
        private void DoUndo()
        {
            if (undoStack.Count > 0)
            {
                // TODO: clear redo?
                // save current image in top of redo stack
                redoStack.Push(canvasBitmap.Clone());
                // take latest image from top of undo stack
                currentUndoItem = undoStack.Pop();
                // show latest image
                CopyBitmapPixels(currentUndoItem, canvasBitmap);
            }
        }

        // go to next existing undo buffer, if available
        private void DoRedo()
        {
            if (redoStack.Count > 0)
            {
                // save current image in top of redo stack
                undoStack.Push(canvasBitmap.Clone());
                // take latest image from top of redo stack
                currentUndoItem = redoStack.Pop();
                // show latest redo image
                CopyBitmapPixels(currentUndoItem, canvasBitmap);
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
            DoUndo();
        }

        public void CanExecute_Undo(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        public void Executed_Redo(object sender, ExecutedRoutedEventArgs e)
        {
            DoRedo();
        }

        public void CanExecute_Redo(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        public void Executed_Paste(object sender, ExecutedRoutedEventArgs e)
        {
            OnPasteImageFromClipboard();
        }

        public void CanExecute_Paste(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        public void Executed_Copy(object sender, ExecutedRoutedEventArgs e)
        {
            OnCopyImageToClipboard();
        }

        public void CanExecute_Copy(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        public void Executed_SaveAs(object sender, ExecutedRoutedEventArgs e)
        {
            saveFile = null;
            OnSaveButton(null, null);
        }

        public void CanExecute_SaveAs(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        public void Executed_Save(object sender, ExecutedRoutedEventArgs e)
        {
            OnSaveButton(null, null);
        }

        public void CanExecute_Save(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        public void Executed_New(object sender, ExecutedRoutedEventArgs e)
        {
            OnClearButton(null, null);
        }

        public void CanExecute_New(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        // paste image from clipboard to canvas
        void OnPasteImageFromClipboard()
        {
            if (Clipboard.ContainsImage())
            {
                IDataObject clipboardData = Clipboard.GetDataObject();

                // https://markheath.net/post/save-clipboard-image-to-file
                if (clipboardData != null)
                {
                    BitmapSource source = Clipboard.GetImage();

                    // https://stackoverflow.com/questions/5867657/copying-from-bitmapsource-to-writablebitmap
                    // Calculate stride of source
                    int stride = source.PixelWidth * (source.Format.BitsPerPixel + 7) / 8;
                    Console.WriteLine("stride:" + stride);
                    // Create data array to hold source pixel data
                    byte[] data = new byte[stride * source.PixelHeight];

                    // Copy source image pixels to the data array
                    source.CopyPixels(data, stride, 0);

                    // Create WriteableBitmap to copy the pixel data to.      
                    WriteableBitmap target = new WriteableBitmap(
                      source.PixelWidth,
                      source.PixelHeight,
                      source.DpiX, source.DpiY,
                      source.Format, null);

                    // Write the pixel data to the WriteableBitmap.
                    target.WritePixels(
                      new Int32Rect(0, 0, source.PixelWidth, source.PixelHeight),
                      data, stride, 0);

                    for (int x = 0; x < canvasResolutionX; x++)
                    {
                        for (int y = 0; y < canvasResolutionY; y++)
                        {
                            var cc = GetPixelColor(x, y, target);
                            Console.WriteLine();
                            var ccc = new PixelColor();
                            ccc.Red = cc.Red;
                            ccc.Green = cc.Green;
                            ccc.Blue = cc.Blue;
                            ccc.Alpha = 255;
                            SetPixel(canvasBitmap, x, y, (int)ccc.ColorBGRA);
                        }
                    }
                }
            }
        }

        void OnCopyImageToClipboard()
        {
            // FIXME no transparency
            Clipboard.SetImage(ConvertWriteableBitmapToBitmapImage(canvasBitmap.Clone()));

            /*
            var bitmap = ConvertWriteableBitmapToBitmapImage(canvasBitmap.Clone());
            Stream stream = new MemoryStream();
            BitmapEncoder enc = new BmpBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bitmap));
            enc.Save(stream);
            var data = new DataObject("PNG", stream);
            Clipboard.Clear();
            Clipboard.SetDataObject(data, true);
            */
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

        // color picker for huebar
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

        private void OnGetTransparentColorButton(object sender, MouseButtonEventArgs e)
        {
            var c = new PixelColor();
            c.Red = 255;
            c.Green = 255;
            c.Blue = 255;
            c.Alpha = 0;
            currentColor = c;
            rectCurrentColor.Fill = new SolidColorBrush(Color.FromArgb(c.Alpha, c.Red, c.Green, c.Blue));
            ResetCurrentBrightnessPreview(currentColor);
        }

        private void chkMirrorX_Unchecked(object sender, RoutedEventArgs e)
        {
            lineSymmetryXpositionA.Visibility = Visibility.Hidden;
            lineSymmetryXpositionB.Visibility = Visibility.Hidden;
        }

        private void chkMirrorX_Checked(object sender, RoutedEventArgs e)
        {
            lineSymmetryXpositionA.Visibility = Visibility.Visible;
            lineSymmetryXpositionB.Visibility = Visibility.Visible;
        }

        private void OnLevelSaturationMouseMoved(object sender, MouseEventArgs e)
        {
            if (rectSaturation.IsMouseOver == false) return;
            if (e.LeftButton == MouseButtonState.Pressed) OnLevelSaturationMouseDown(null, null);
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

        private void OnHueRectangleMouseMoved(object sender, MouseEventArgs e)
        {
            if (rectHueBar.IsMouseOver == false) return;
            if (e.LeftButton == MouseButtonState.Pressed) rectHueBar_MouseDown(null, null);
        }
    } // class

} // namespace
