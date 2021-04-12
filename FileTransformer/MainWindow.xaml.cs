using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using System.IO;
using System.Threading;
using System.Security.Cryptography;
using System.Reflection;

namespace FileTransformerNS
{
    public partial class MainWindow : Window
    {
        #region Configuration
        const int WINDOW_HEIGHT = 345, WINDOW_HEIGHT_FULL = 430;
        const string DEFAULT_ENCRYPTION_KEY = "FileTransformer";
        #endregion

        private string[] filesPaths, filesNames;
        private string savePath, encryptionKey, extension;
        private byte[] encryptionKeyBytes;
        private int TransformedFilesListIndex;
        private bool isCurrentlyRunning = false;
        private FileTransformer.Mode conversionMode = FileTransformer.Mode.Transform;
        private List<(string fileName, string fileSize)> TransformedFilesList;
        private CancellationTokenSource cts = new CancellationTokenSource();

        public MainWindow()
        {
            InitializeComponent();
            this.Height = WINDOW_HEIGHT;
            extension_textBox.Visibility = Visibility.Hidden;
            right_arrow_button.Visibility = Visibility.Hidden;
            left_arrow_button.Visibility = Visibility.Hidden;
            ResetWindow("Transform");
        }

        private void ResetWindow(string actionButtonText)
        {
            progressBar.Value = 0;
            isCurrentlyRunning = false;
            action_button.Content = actionButtonText;
            tabControl.IsEnabled = true;
            cts = new CancellationTokenSource();
            TransformedFilesListIndex = 0;
            left_arrow_button.Visibility = Visibility.Hidden;
            right_arrow_button.Visibility = Visibility.Hidden;
        }

        private void tabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TabItem selected = (tabControl.SelectedItem as TabItem);

            selected.Foreground = new SolidColorBrush(Colors.Black);

            if (selected.Name == "restore_tabItem")
            {
                transform_tabItem.Foreground = new SolidColorBrush(Colors.White);
                extension_checkBox.Visibility = Visibility.Hidden;
                extension_textBox.Visibility = Visibility.Hidden;
                action_button.Content = "Restore";
                conversionMode = FileTransformer.Mode.Restore;
            }

            else if (selected.Name == "transform_tabItem")
            {
                restore_tabItem.Foreground = new SolidColorBrush(Colors.White);
                extension_checkBox.Visibility = Visibility.Visible;

                if (extension_checkBox.IsChecked ?? true)
                    extension_textBox.Visibility = Visibility.Visible;

                action_button.Content = "Transform";
                conversionMode = FileTransformer.Mode.Transform;
            }
        }

        private void chooseFile_button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Multiselect = true;
            Nullable<bool> result = dlg.ShowDialog();

            if (result == true)
            {
                filesPaths = dlg.FileNames;

                filesNames = new string[filesPaths.Length];

                for (int i = 0; i < filesPaths.Length; i++)
                    filesNames[i] = Path.GetFileName(filesPaths[i]);

                files_comboBox.ItemsSource = filesNames;

                if (filesNames.Length > 1)
                {
                    files_comboBox.IsHitTestVisible = true;
                    files_comboBox.IsEditable = false;
                }

                else
                {
                    files_comboBox.IsHitTestVisible = false;
                    files_comboBox.IsEditable = true;
                    files_comboBox.Text = filesNames[0];
                }

                #region Set save path to last chosen file's directory
                savePath = filesPaths[filesPaths.Length - 1].Replace(filesNames[filesPaths.Length - 1], "");
                savePath_textBox.Text = savePath;
                #endregion
            }
        }

        private void savePath_button_Click(object sender, RoutedEventArgs e)
        {
            using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
            {
                System.Windows.Forms.DialogResult dialogResult = fbd.ShowDialog();

                if (dialogResult == System.Windows.Forms.DialogResult.OK)
                {
                    savePath = fbd.SelectedPath;
                    savePath_textBox.Text = savePath;
                }
            }
        }

        private void extension_checkBox_Changed(object sender, RoutedEventArgs e)
        {
            if (extension_checkBox.IsChecked ?? true)
            {
                extension_textBox.Text = string.Empty;
                extension_textBox.Visibility = Visibility.Visible;
            }

            else
                extension_textBox.Visibility = Visibility.Hidden;
        }

        private async void action_button_Click(object sender, RoutedEventArgs e)
        {
            if (isCurrentlyRunning)
            {
                cts.Cancel();
                return;
            }

            this.Height = WINDOW_HEIGHT;

            #region Check if inputs are valid
            for (int i = 0; i < filesPaths.Length; i++)
            {
                if (!File.Exists(filesPaths[i]))
                {
                    MessageBox.Show($"\"{filesNames[i]}\" not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }    
            }

            if (!Directory.Exists(savePath))
            {
                MessageBox.Show($"Save directory not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            else if (extension_textBox.Text.IndexOfAny(Path.GetInvalidFileNameChars()) != -1)
            {
                MessageBox.Show($"Extension string includes invalid filename characters.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            #endregion

            if (extension_textBox.Text != null && extension_textBox.Text != "")
                extension = extension_textBox.Text;

            #region Set encryption key
            if (encryption_passwordBox.Password == string.Empty)
                encryptionKey = DEFAULT_ENCRYPTION_KEY;

            else
                encryptionKey = encryption_passwordBox.Password;

            using (SHA256Managed SHA256 = new SHA256Managed())
                encryptionKeyBytes = SHA256.ComputeHash(Encoding.UTF8.GetBytes(encryptionKey));
            #endregion

            progressBar.Visibility = Visibility.Visible;
            isCurrentlyRunning = true;
            string textboxOldText = (string)action_button.Content;
            action_button.Content = "Cancel";
            tabControl.IsEnabled = false;

            TransformedFilesList = new List<(string fileName, string fileSize)>();

            #region Loop over selected files and convert them
            for (int i = 0; i < filesPaths.Length; i++)
            {
                if (!File.Exists(filesPaths[i]))
                {
                    MessageBox.Show($"File \"{filesNames[i]}\" not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    continue;
                }

                if (conversionMode == FileTransformer.Mode.Transform)
                    status_label.Content = $"Transforming \"{filesNames[i]}\"...";

                else
                    status_label.Content = $"Restoring \"{filesNames[i]}\"...";

                CancellationToken ct = cts.Token;
                Progress<int> progressReporter = new Progress<int>(value => { progressBar.Value = value; });
                FileTransformer ft = new FileTransformer(encryptionKeyBytes, progressReporter);
                Task<string> task;
                string newFilePath = null;

                try
                {
                    if (conversionMode == FileTransformer.Mode.Transform)
                        task = Task.Run(() => ft.TransformFile(filesPaths[i], savePath, ct, extension), ct);

                    else // (conversionMode == FileTransformer.Mode.Restore)
                        task = Task.Run(() => ft.RestoreFile(filesPaths[i], savePath, ct), ct);

                    newFilePath = await task;

                    TransformedFilesList.Add((Path.GetFileName(newFilePath), FormatFileSize(new FileInfo(newFilePath).Length)));

                    if (delete_file_checkBox.IsChecked ?? false)
                        File.Delete(filesPaths[i]);
                }

                #region Error handling
                catch (OperationCanceledException)
                {
                    status_label.Content = $"Operation canceled.";
                    ResetWindow(textboxOldText);
                    return;
                }

                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                finally
                {
                    progressBar.Value = 0;
                }
                #endregion
            }
            #endregion

            int fileListLength = TransformedFilesList.Count();
            ResetWindow(textboxOldText);

            if (fileListLength > 0)
            {
                TransformedFilesListIndex = 0;

                if (fileListLength == filesNames.Length)
                    status_label.Content = "Conversion completed successfully";

                else
                    status_label.Content = $"{fileListLength}/{filesNames.Length} conversions completed successfully.";

                filename_textBox.Text = TransformedFilesList[0].fileName;
                filesize_textBox.Text = TransformedFilesList[0].fileSize;
                this.Height = WINDOW_HEIGHT_FULL;

                if (fileListLength > 1)
                {
                    right_arrow_button.Visibility = Visibility.Visible;
                    files_count_label.Content = $"- 1/{fileListLength} -";
                }

                else
                    files_count_label.Content = string.Empty;
            }

            else
            {
                status_label.Content = "Conversion failed.";
                this.Height = WINDOW_HEIGHT;
            }
        }

        private void right_arrow_button_Click(object sender, RoutedEventArgs e)
        {
            TransformedFilesListIndex++;
            filename_textBox.Text = TransformedFilesList[TransformedFilesListIndex].fileName;
            filesize_textBox.Text = TransformedFilesList[TransformedFilesListIndex].fileSize;
            files_count_label.Content = $"- {TransformedFilesListIndex+1}/{TransformedFilesList.Count()} -";

            if (TransformedFilesListIndex + 1 >= TransformedFilesList.Count())
                right_arrow_button.Visibility = Visibility.Hidden;

            left_arrow_button.Visibility = Visibility.Visible;
        }

        private void left_arrow_button_Click(object sender, RoutedEventArgs e)
        {
            TransformedFilesListIndex--;
            filename_textBox.Text = TransformedFilesList[TransformedFilesListIndex].fileName;
            filesize_textBox.Text = TransformedFilesList[TransformedFilesListIndex].fileSize;
            files_count_label.Content = $"- {TransformedFilesListIndex + 1}/{TransformedFilesList.Count()} -";

            if (TransformedFilesListIndex - 1 < 0)
                left_arrow_button.Visibility = Visibility.Hidden;

            right_arrow_button.Visibility = Visibility.Visible;
        }

        #region Menu Items
        private void MenuItem_Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void MenuItem_CheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            string newVersion;
            try
            {
                using (var wc = new System.Net.WebClient())
                {
                    System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
                    newVersion = wc.DownloadString("https://raw.githubusercontent.com/MichaelYochpaz/FileTransformer/main/VERSION").Replace("\n", "");
                }
            }

            catch
            {
                MessageBox.Show("Could not connect to server.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Version currentVersion = Assembly.GetExecutingAssembly().GetName().Version;

            if (currentVersion.CompareTo(Version.Parse(newVersion)) < 0)
            {
                UpdateWindow window = new UpdateWindow(currentVersion.ToString(3), newVersion);
                window.Owner = this;
                window.Show();
            }

            else
                MessageBox.Show("You are running the latest version of FileTransformer.", "", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuItem_About_Clicked(object sender, RoutedEventArgs e)
        {
            AboutWindow window = new AboutWindow();
            window.Owner = this;
            window.Show();
        }
        #endregion

        /// <summary>Format file size from bytes length to a readable format (KB, MB, GB, etc).</summary>
        /// <param name="fileSizeBytes">File size in bytes.</param>
        /// <returns>A string with file size in a readable format.</returns>
        private string FormatFileSize(long fileSizeBytes)
        {
            int unit = 1024;
            if (fileSizeBytes < unit)
                return $"{fileSizeBytes} B";

            int exp = (int)(Math.Log(fileSizeBytes) / Math.Log(unit));
            return $"{fileSizeBytes / Math.Pow(unit, exp):F2} {("KMGTPE")[exp - 1]}B";
        }
    }
}