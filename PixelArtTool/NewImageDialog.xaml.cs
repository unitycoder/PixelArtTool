using System.Windows;
using System.Windows.Input;

namespace PixelArtTool
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class NewImageDialog : Window
    {
        public NewImageDialog()
        {
            InitializeComponent();
            sliderResolution.Focus();
        }

        private void OnOkButtonClick(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
