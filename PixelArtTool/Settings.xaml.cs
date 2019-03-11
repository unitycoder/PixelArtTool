using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using static PixelArtTool.Tools;

namespace PixelArtTool
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class Settings : Window
    {
        public Settings()
        {
            InitializeComponent();
            Start();
        }

        void Start()
        {
            // TODO load all current settings
            settingsLightColor.Fill = ConvertSystemDrawingColorToSolidColorBrush(Properties.Settings.Default.gridLightColor);
            settingsDarkColor.Fill = ConvertSystemDrawingColorToSolidColorBrush(Properties.Settings.Default.gridDarkColor);
            sldGridAlpha.Value = Properties.Settings.Default.gridAlpha;
            sliderResolution.Value = Properties.Settings.Default.defaultResolution;
        }

        private void OnOkButtonClick(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void settingsLightColor_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var dlg = new ColorPicker();
            dlg.Owner = this;
            var result = dlg.ShowDialog();
            switch (result)
            {
                case true: // ok                    
                    // get values from color picker
                    settingsLightColor.Fill = dlg.rectCurrentColor.Fill;
                    break;
                case false: // cancelled
                    break;
                default:
                    Console.WriteLine("Unknown error..");
                    break;
            }
        }

        private void settingsDarkColor_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var dlg = new ColorPicker();
            dlg.Owner = this;
            var result = dlg.ShowDialog();
            switch (result)
            {
                case true: // ok
                    settingsDarkColor .Fill = dlg.rectCurrentColor.Fill;
                    break;
                case false: // cancelled
                    break;
                default:
                    Console.WriteLine("Unknown error..");
                    break;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (this.DialogResult==true)
            {
                // TODO save all settings
                Properties.Settings.Default.gridDarkColor = ConvertBrushToSystemDrawingColor(settingsDarkColor.Fill);
                Properties.Settings.Default.gridLightColor = ConvertBrushToSystemDrawingColor(settingsLightColor.Fill);
                Properties.Settings.Default.gridAlpha = (byte)sldGridAlpha.Value;
                Properties.Settings.Default.defaultResolution = (byte)sliderResolution.Value;
                Properties.Settings.Default.Save();
            }
        }

        private void resetButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: reset to default settings
            Console.WriteLine("TODO reset");
        }
    } // class
}
