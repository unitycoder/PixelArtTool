using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;

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
        }

        private void settingsLightColor_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var dlg = new ColorPicker();
            dlg.Owner = this;
            var result = dlg.ShowDialog();
            switch (result)
            {
                case true: // ok
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
                    break;
                case false: // cancelled
                    break;
                default:
                    Console.WriteLine("Unknown error..");
                    break;
            }
        }
    }
}
