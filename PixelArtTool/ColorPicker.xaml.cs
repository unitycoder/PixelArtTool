using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using static PixelArtTool.Tools;

namespace PixelArtTool
{
    /// <summary>
    /// Interaction logic for ColorPicker.xaml
    /// </summary>
    public partial class ColorPicker : Window
    {
        public ColorPicker()
        {
            InitializeComponent();
        }

        private void OnOkButtonClick(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void OnLevelSaturationMouseMoved(object sender, MouseEventArgs e)
        {
            if (rectSaturation.IsMouseOver == false) return;
            if (e.LeftButton == MouseButtonState.Pressed) OnLevelSaturationMouseDown(null, null);
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
            //currentColor = c2;
            rectCurrentColor.Fill = new SolidColorBrush(Color.FromArgb(c2.Alpha, c2.Red, c2.Green, c2.Blue));
            //ResetCurrentBrightnessPreview(currentColor);
        }

        private void OnHueRectangleMouseMoved(object sender, MouseEventArgs e)
        {
            if (rectHueBar.IsMouseOver == false) return;
            if (e.LeftButton == MouseButtonState.Pressed) rectHueBar_MouseDown(null, null);
        }
    }
}
