using System;
using System.Windows;
using System.Windows.Input;
using System.Reflection;
using System.Diagnostics;

namespace FileTransformerNS
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        Process process;
        public AboutWindow()
        {
            InitializeComponent();
            version_label.Content += Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
            process = new Process();
            process.StartInfo.UseShellExecute = true;
        }

        private void GitHub_grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            process.StartInfo.FileName = "https://github.com/MichaelYochpaz/FileTransformer";
            process.Start();
        }

        private void icons8_link_label_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            process.StartInfo.FileName = "https://icons8.com";
            process.Start();
        }
    }
}
