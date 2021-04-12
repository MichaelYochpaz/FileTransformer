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

namespace FileTransformerNS
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class UpdateWindow : Window
    {
        public UpdateWindow(string currentVersion, string newVersion)
        {
            InitializeComponent();
            current_version_label.Content = $"Current version: {currentVersion}";
            new_version_label.Content = $"New version: {newVersion}";
        }

        private void download_button_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/MichaelYochpaz/FileTransformer/releases/latest");
        }
    }
}
