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
using System.Windows.Navigation;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.IO;
using System.Security.Cryptography;
using System.ComponentModel;
using System.Threading;

namespace FileTransformer
{
    public enum FileTransformMode
    {
        Transform,
        Restore
    }

    public partial class MainWindow : Window
    {
        const int WINDOW_HEIGHT = 315, WINDOW_HEIGHT_FULL = 390;

        // (CHUNK_SIZE * 8 % 6 == 0) needs to be true to work since a 1 Base64 character = 6 bits
        const int CHUNK_SIZE = 3145728; // = 3MB
        const int HEADER_SIZE = 512, FOOTER_SIZE = 64 ; // in bytes
        const string DEFAULT_ENCRYPTION_KEY = "FileTransformer";
        const string CANCEL_MESSAGE = "Operation cancelled.";

        private string filePath, savePath, encryptionKey;
        private byte[] encryptionKeyBytes;
        private bool encrypted = false;
        private CancellationTokenSource cts = new CancellationTokenSource();


        public MainWindow()
        {
            InitializeComponent();
            this.Height = WINDOW_HEIGHT;
            cancel_button.Visibility = Visibility.Hidden;
        }

        private void GitHub_image_MouseUp(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/MichaelYochpaz/FileTransformer");
        }

        private void chooseFile_button_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            Nullable<bool> result = dlg.ShowDialog();

            if (result == true)
            {
                filePath = dlg.FileName;
                file_textBox.Text = filePath;
            }
        }

        private void savePath_button_Click(object sender, RoutedEventArgs e)
        {
            using (CommonOpenFileDialog cofd = new CommonOpenFileDialog())
            {
                cofd.IsFolderPicker = true;
                CommonFileDialogResult result = cofd.ShowDialog();


                if (result == CommonFileDialogResult.Ok)
                {
                    savePath = cofd.FileName;
                    savePath_textBox.Text = savePath;
                }
            }
        }

        private void encryption_checkBox_Click(object sender, RoutedEventArgs e)
        {
            if (encryption_checkBox.IsChecked == true)
            {
                encryption_checkBox.Content = "Encryption Key (optional):";
                encryption_passwordBox.IsEnabled = true;
            }

            else
            {
                encryption_checkBox.Content = "Use Encryption";
                encryption_passwordBox.Password = String.Empty;
                encryption_passwordBox.IsEnabled = false;
            }
        }

        private void cancel_button_Click(object sender, RoutedEventArgs e)
        {
            cts.Cancel();
        }

        private async void action_button_Click(object sender, RoutedEventArgs e)
        {
            this.Height = WINDOW_HEIGHT;

            if (!File.Exists(filePath))
            {
                MessageBox.Show("File not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            else if (!Directory.Exists(savePath))
            {
                MessageBox.Show("Directory not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            FileTransformMode mode = (sender == transform_button) ? FileTransformMode.Transform : FileTransformMode.Restore;

            if (encryption_checkBox.IsChecked == true)
            {
                if (encryption_passwordBox.Password == String.Empty)
                    encryptionKey = DEFAULT_ENCRYPTION_KEY;

                else
                    encryptionKey = encryption_passwordBox.Password;

                using (SHA256Managed SHA256 = new SHA256Managed())
                    encryptionKey = Encoding.UTF8.GetString(SHA256.ComputeHash(Encoding.UTF8.GetBytes(encryptionKey)));

                encrypted = true;
            }

            if (encrypted)
                encryptionKeyBytes = Encoding.UTF8.GetBytes(encryptionKey);

            if (mode == FileTransformMode.Transform)
                status_label.Content = "Transforming File...";

            else
                status_label.Content = "Restoring File...";

            progressBar.Visibility = Visibility.Visible;
            transform_button.Visibility = Visibility.Hidden;
            restore_button.Visibility = Visibility.Hidden;
            cancel_button.Visibility = Visibility.Visible;

            CancellationToken ct = cts.Token;
            Progress<int> progressReporter = new Progress<int>(value => { progressBar.Value = value; });
            Task<Dictionary<string, string>> task = Task.Run(() => FileConversion(mode, ct, progressReporter), ct);
            Dictionary<string, string> fileInfo = await task;

            string errorMessage;

            if (fileInfo.TryGetValue("error", out errorMessage))
            {
                status_label.Content = errorMessage;
                string filename, filePath;

                if (fileInfo.TryGetValue("filename", out filename))
                {
                    filePath = Path.Combine(savePath, filename);

                    if (File.Exists(filePath))
                        File.Delete(filePath);
                }

                this.Height = WINDOW_HEIGHT;
                progressBar.Value = 0;
                transform_button.Visibility = Visibility.Visible;
                restore_button.Visibility = Visibility.Visible;
                cancel_button.Visibility = Visibility.Hidden;
            }

            else
            {
                status_label.Content = "Completed";
                filename_textbox.Text = fileInfo["filename"];
                filesize_textbox.Text = fileInfo["filesize"];
                this.Height = WINDOW_HEIGHT_FULL;

                if (delete_file_checkBox.IsChecked ?? false)
                    File.Delete(filePath);
            }

            progressBar.Value = 0;
            transform_button.Visibility = Visibility.Visible;
            restore_button.Visibility = Visibility.Visible;
            cancel_button.Visibility = Visibility.Hidden;
            cts = new CancellationTokenSource();
        }

        /// <summary>
        /// Call TransformFile() or RestoreFile() and return a dictionary with generated file's info.
        /// </summary>
        /// <returns>
        /// A dictionary with information about the generated file.
        /// </returns>
        private Dictionary<string, string> FileConversion(FileTransformMode mode, CancellationToken ct, IProgress<int> progressReporter)
        {
            if (mode == FileTransformMode.Transform)
                return TransformFile(filePath, savePath, encryptionKeyBytes, CHUNK_SIZE, HEADER_SIZE, ct, progressReporter);

            else
                return RestoreFile(filePath, savePath, encryptionKeyBytes, CHUNK_SIZE, HEADER_SIZE, FOOTER_SIZE, ct, progressReporter);
        }

        /// <summary>
        /// Encrypt a file and return a dictionary with generated file's info.
        /// </summary>
        /// <returns>
        /// A dictionary with information about the generated file.
        /// </returns>
        private Dictionary<string, string> TransformFile(string filePath, string savePath, byte[] encryptionBytes, int chunkSize, int headerSize, CancellationToken ct, IProgress<int> progressReporter = null)
        {
            Dictionary<string, string> fileInfo = new Dictionary<string, string>();
            string randomFileName = Path.GetRandomFileName();
            string newFilePath = Path.Combine(savePath, randomFileName);
            bool encrypted = (encryptionBytes != null);
            fileInfo.Add("filename", randomFileName);

            using (var fs1 = new FileStream(filePath, FileMode.Open))
            using (var fs2 = new FileStream(newFilePath, FileMode.Create))
            using (SHA256Managed SHA256 = new SHA256Managed())
            {
                long bufferIndex = 0, encryptionIndex = 0;
                byte[] buffer = new byte[chunkSize];
                byte[] data, remainder;

                #region Embed filename in new file
                string[] splitPath = filePath.Split('\\');
                byte[] originalFileNameBytes = Encoding.UTF8.GetBytes(splitPath[splitPath.Length - 1]);
                Array.Resize(ref originalFileNameBytes, HEADER_SIZE);

                if (encrypted)
                    EncryptDecryptBytes(originalFileNameBytes, encryptionBytes, 0);

                byte[] filenameData = Encoding.UTF8.GetBytes(BitConverter.ToString(originalFileNameBytes).Replace("-", string.Empty));
                fs2.Write(filenameData, 0, originalFileNameBytes.Length);
                encryptionIndex += originalFileNameBytes.Length;
                #endregion

                #region File bytes conversion loop (using chunks)
                while (bufferIndex <= fs1.Length - chunkSize)
                {
                    fs1.Read(buffer, 0, chunkSize);
                    SHA256.TransformBlock(buffer, 0, buffer.Length, null, 0);

                    if (encrypted)
                        EncryptDecryptBytes(buffer, encryptionBytes, encryptionIndex);

                    data = Encoding.UTF8.GetBytes(Convert.ToBase64String(buffer));
                    fs2.Write(data, 0, data.Length);

                    bufferIndex += buffer.Length;
                    encryptionIndex += buffer.Length;

                    if (progressReporter != null)
                        progressReporter?.Report((int)Math.Round((double)bufferIndex / fs1.Length * 100));

                    if (ct.IsCancellationRequested)
                    {
                        fileInfo.Add("error", CANCEL_MESSAGE);
                        return fileInfo;
                    }
                }
                #endregion

                #region Remaining bytes ( < chunkSize) conversion
                int remainderLength = (int)(fs1.Length - bufferIndex);
                remainder = new byte[remainderLength];

                fs1.Read(remainder, 0, remainderLength);
                SHA256.TransformFinalBlock(remainder, 0, remainderLength);
                fileInfo.Add("hash", BitConverter.ToString(SHA256.Hash).Replace("-", string.Empty));

                if (encrypted)
                    EncryptDecryptBytes(remainder, encryptionBytes, encryptionIndex);

                data = Encoding.UTF8.GetBytes(Convert.ToBase64String(remainder));
                fs2.Write(data, 0, data.Length);
                bufferIndex += remainder.Length;
                encryptionIndex += remainder.Length;
                #endregion

                #region Embed SHA256 hash in new file
                byte[] transformedHashBytes = SHA256.Hash;

                if (encrypted)
                    EncryptDecryptBytes(transformedHashBytes, encryptionBytes, encryptionIndex);

                transformedHashBytes = Encoding.UTF8.GetBytes(BitConverter.ToString(transformedHashBytes).Replace("-", string.Empty));

                fs2.Write(transformedHashBytes, 0, transformedHashBytes.Length);
                #endregion

                progressReporter?.Report(100);
                fileInfo.Add("filesize", FormatFileSize(fs2.Length));
                return fileInfo;
            }
        }

        /// <summary>
        /// Decrypt an encrypted file and return a dictionary with generated file's info.
        /// </summary>
        /// <returns>
        /// A dictionary with information about the generated file.
        /// </returns>
        private Dictionary<string, string> RestoreFile(string filePath, string savePath, byte[] encryptionBytes, int chunkSize, int headerSize, int footerSize, CancellationToken ct, IProgress<int> progressReporter)
        {
            Dictionary<string, string> fileInfo = new Dictionary<string, string>();
            string restoredFileName, newFileName, newFilePath;
            bool encrypted = (encryptionBytes != null);

            using (var fs1 = new FileStream(filePath, FileMode.Open))
            {
                #region Exctract filename from file
                byte[] restoredFilenameBytes = new byte[headerSize];

                fs1.Read(restoredFilenameBytes, 0, restoredFilenameBytes.Length);

                try
                {
                    byte[] filenameData = hexToBytes(Encoding.UTF8.GetString(restoredFilenameBytes));

                    if (encrypted)
                        EncryptDecryptBytes(filenameData, encryptionBytes, 0);

                    restoredFileName = Encoding.UTF8.GetString(filenameData).TrimEnd('\0'); // Get rid of null bytes
                }

                catch
                {
                    MessageBox.Show("Could not restore original file name.\nPleas make sure you've entered the correct encryption key.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    fileInfo.Add("error", "Error: Could not restore original file name.");
                    return fileInfo;
                }

                restoredFilenameBytes = Encoding.UTF8.GetBytes(restoredFileName);
                fileInfo.Add("filename", restoredFileName);

                newFileName = Encoding.UTF8.GetString(restoredFilenameBytes);
                newFilePath = Path.Combine(savePath, newFileName);
                #endregion


                using (var fs2 = new FileStream(newFilePath, FileMode.Create))
                using (SHA256Managed SHA256 = new SHA256Managed())
                {
                    long bufferIndex = headerSize, decryptionIndex = bufferIndex;
                    byte[] buffer = new byte[chunkSize];
                    byte[] data, remainder;

                    #region File bytes conversion loop (using chunks)
                    while (bufferIndex <= fs1.Length - chunkSize - FOOTER_SIZE)
                    {
                        fs1.Read(buffer, 0, chunkSize);
                        data = Convert.FromBase64String(Encoding.UTF8.GetString(buffer));

                        if (encrypted)
                            EncryptDecryptBytes(data, encryptionBytes, decryptionIndex);

                        fs2.Write(data, 0, data.Length);
                        SHA256.TransformBlock(data, 0, data.Length, null, 0);
                        bufferIndex += buffer.Length;
                        decryptionIndex += data.Length;

                        if (progressReporter != null)
                            progressReporter?.Report((int)Math.Round((double)bufferIndex / fs1.Length * 100));

                        if (ct.IsCancellationRequested)
                        {
                            fileInfo.Add("error", CANCEL_MESSAGE);
                            return fileInfo;
                        }
                    }
                    #endregion

                    #region Remaining bytes ( < chunkSize) conversion
                    int remainderLength = (int)(fs1.Length - bufferIndex - FOOTER_SIZE);
                    remainder = new byte[remainderLength];

                    fs1.Read(remainder, 0, remainderLength);
                    data = Convert.FromBase64String(Encoding.UTF8.GetString(remainder));

                    if (encrypted)
                        EncryptDecryptBytes(data, encryptionBytes, decryptionIndex);

                    fs2.Write(data, 0, data.Length);
                    SHA256.TransformFinalBlock(data, 0, data.Length);
                    decryptionIndex += data.Length;
                    fileInfo.Add("filesize", FormatFileSize(fs2.Length));
                    string hashString = BitConverter.ToString(SHA256.Hash).Replace("-", string.Empty);
                    #endregion

                    #region Exctract original file hash from file
                    byte[] recoveredHashBytes = new byte[FOOTER_SIZE];
                    fs1.Read(recoveredHashBytes, 0, FOOTER_SIZE);

                    recoveredHashBytes = hexToBytes(Encoding.UTF8.GetString(recoveredHashBytes));

                    if (encrypted)
                        EncryptDecryptBytes(recoveredHashBytes, encryptionBytes, decryptionIndex);

                    string recoveredHash = BitConverter.ToString(recoveredHashBytes).Replace("-", string.Empty);
                    #endregion

                    #region Test if original file and generated file hashes' match
                    if (hashString != recoveredHash)
                    {
                        MessageBox.Show("Restored file hash doesn't match original file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        fileInfo.Add("error", "Error: Restored file's hash doesn't match original file.");
                        return fileInfo;
                    }
                    #endregion

                    return fileInfo;
                }
            }
        }

        /// <summary>
        /// Encrypt or decrypt a byte array with another byte array using XOR.
        /// </summary>
        /// <returns>
        /// A refrence to the changed byte array.
        /// </returns>
        private byte[] EncryptDecryptBytes(byte[] bytes, byte[] encryptionKey, long index)
        {
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] ^= encryptionKey[(i+index) % encryptionKey.Length];

            return bytes;
        }

        /// <summary>
        /// Converts a hex string to a byte array.
        /// </summary>
        /// <returns>
        /// A byte array representation of a hex string.
        /// </returns>
        private byte[] hexToBytes(string hexString)
        {
            byte[] bytes = new byte[hexString.Length / 2];

            for (int i = 0; i < hexString.Length; i += 2)
                bytes[i / 2] = Convert.ToByte(hexString.Substring(i, 2), 16);

            return bytes;
        }

        /// <summary>
        /// Format file size from bytes to a readable format (KB, MB, GB, etc).
        /// </summary>
        /// <returns>
        /// A string with a readable file size.
        /// </returns>
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