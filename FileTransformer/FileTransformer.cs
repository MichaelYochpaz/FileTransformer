using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;
using System.Threading;
using System.Security.Cryptography;

namespace FileTransformerNS
{
    class FileTransformer
    {
        /// <summary>Gets or sets chunk sizes for transformed files.</summary>
        /// <remarks>[ChunkSize / 8 % 6] is required to equal 0 for Base64 conversion to work.</remarks>
        public int ChunksSize
        {
            get { return _chunkSize; }
            set
            {
                if (value * 8 % 6 != 0)
                    throw new ArgumentException("[ChunkSize / 8 % 6] is required to equal 0 for Base64 conversion to work.");

                _chunkSize = value;
            }
        }

        /// <summary>Gets transformed file's header length (in bytes).</summary>
        public int HeaderSize { get { return _headerSize; } }

        private readonly byte[] _fileSignature = new byte[] { 0x70, 0x84, 0x00 };
        private const int _fileNameHeaderLength = 260; // Max path length in Windows is 260
        private const int _aesTagLength = 12;
        private int _headerSize, _chunkSize;

        byte[] encryptionKey;
        IProgress<int> progressReporter;

        /// <summary>Specifies a conversion mode (Transform / Restore).</summary>
        public enum Mode
        {
            Transform,
            Restore
        }

        /// <summary>Initialize a new instance of the FileTransformer class with the encryption key, a progress reporter, and chunk size.</summary>
        /// <param name="encryptionKey">Encryption key to use for encrypting / decrypting files.</param>
        /// <param name="progressReporter">An IProgress reporter instance to report progress to while converting.</param>
        /// <param name="chunkSize">Chunk sizes (in bytes) to use when converting files.<br/>[ChunkSize / 8 % 6] is required to equal 0.</param>
        /// <exception cref="ArgumentNullException">encryptionKey is null.</exception>
        /// <exception cref="ArgumentException">[ChunkSize / 8 % 6] does not equal 0.</exception>
        public FileTransformer(byte[] encryptionKey, IProgress<int> progressReporter = null, int chunkSize = 1572864)
        {
            if (encryptionKey == null)
                throw new ArgumentNullException("Encryption key cannot be null.");
            this.encryptionKey = encryptionKey;
            this.progressReporter = progressReporter;
            this.ChunksSize = chunkSize;

            _headerSize = _fileSignature.Length + _fileNameHeaderLength + 4 + _aesTagLength; // 4 bytes for ChunksSize (int)
        }

        /// <summary>Encrypt a file and return path of the new encrypted file.</summary>
        /// <param name="filePath">Path of file to transform.</param>
        /// <param name="savePath">Path to save transformed file to.</param>
        /// <param name="ct">A CancellationToken to use for sending a cancellation signal to method.</param>
        /// <param name="extension">File extension to add to transformed file's filename.</param>
        /// <returns>A string with the path to the newly generated transformed file.</returns>
        /// <exception cref="IOException">Could not read original file / write to new file.</exception>
        /// <exception cref="OperationCanceledException">Operation canceled.</exception>
        public string TransformFile(string filePath, string savePath, CancellationToken ct, string extension = null)
        {
            string originalFileName = Path.GetFileName(filePath);
            string randomFileName = Path.GetRandomFileName().Replace(".", string.Empty);

            if (extension != null)
                randomFileName += $".{extension}";

            string newFilePath = Path.Combine(savePath, randomFileName);

            try
            {
                using (var fs1 = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (var fs2 = new FileStream(newFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var aes = new AesGcm(encryptionKey))
                {
                    int chunkCount = 0;

                    #region Generate and embed file header
                    byte[] filenameBytes = Encoding.UTF8.GetBytes(originalFileName);
                    Array.Resize(ref filenameBytes, _fileNameHeaderLength);

                    byte[] chunksLengthBytes = BitConverter.GetBytes(ChunksSize);
                    byte[] header = MergeByteArrays(new List<byte[]> { _fileSignature, filenameBytes, chunksLengthBytes });
                    byte[] encryptedHeader = new byte[header.Length];
                    byte[] aesTag = new byte[_aesTagLength];
                    byte[] base64AesTag;

                    aes.Encrypt(GenerateAESNonce(chunkCount, _aesTagLength), header, encryptedHeader, aesTag);
                    encryptedHeader = BytesToBase64(encryptedHeader);
                    base64AesTag = BytesToBase64(aesTag);
                    fs2.Write(encryptedHeader, 0, encryptedHeader.Length);
                    fs2.Write(base64AesTag, 0, base64AesTag.Length);
                    chunkCount++;
                    #endregion

                    long bufferIndex = 0;
                    byte[] chunk = new byte[ChunksSize];
                    byte[] encryptedChunk;

                    #region File conversion loop (using chunks)
                    while (bufferIndex <= fs1.Length - chunk.Length)
                    {
                        encryptedChunk = new byte[chunk.Length];
                        aesTag = new byte[_aesTagLength];

                        fs1.Read(chunk, 0, chunk.Length);
                        bufferIndex += chunk.Length;
                        aes.Encrypt(GenerateAESNonce(chunkCount, _aesTagLength), chunk, encryptedChunk, aesTag);
                        encryptedChunk = BytesToBase64(encryptedChunk);
                        base64AesTag = BytesToBase64(aesTag);
                        fs2.Write(encryptedChunk, 0, encryptedChunk.Length);
                        fs2.Write(base64AesTag, 0, base64AesTag.Length);

                        chunkCount++;

                        if (progressReporter != null)
                            progressReporter?.Report((int)Math.Round((double)bufferIndex / fs1.Length * 100));

                        if (ct.IsCancellationRequested)
                            throw new OperationCanceledException();
                    }
                    #endregion

                    #region Remaining bytes conversion
                    int remainderLength = (int)(fs1.Length - bufferIndex);
                    byte[] remainder = new byte[remainderLength];
                    byte[] encryptedRemainder = new byte[remainder.Length];

                    fs1.Read(remainder, 0, remainderLength);
                    aes.Encrypt(GenerateAESNonce(chunkCount, _aesTagLength), remainder, encryptedRemainder, aesTag);
                    encryptedRemainder = BytesToBase64(encryptedRemainder);
                    base64AesTag = BytesToBase64(aesTag);
                    fs2.Write(encryptedRemainder, 0, encryptedRemainder.Length);
                    fs2.Write(base64AesTag, 0, base64AesTag.Length);
                    #endregion
                }

                progressReporter?.Report(100);
            }

            catch
            {
                if (File.Exists(newFilePath))
                    File.Delete(newFilePath);

                throw;
            }

            return newFilePath;
        }

        /// <summary>Decrypt a file and return path of the new decrypted file.</summary>
        /// <param name="filePath">Path of file to decrypt.</param>
        /// <param name="savePath">Path to save decrypted file to.</param>
        /// <param name="ct">A CancellationToken to use for sending a cancellation signal to method.</param>
        /// <returns>A string with the path to the newly generated decrypted file.</returns>
        /// <exception cref="IOException">Could not read original file / write to new file.</exception>
        /// <exception cref="FileFormatException">File is not a valid transformed file / Encryption key is wrong / File data was altered after it transformation.</exception>
        /// <exception cref="OperationCanceledException">Operation canceled.</exception> 
        public string RestoreFile(string filePath, string savePath, CancellationToken ct)
        {
            string newFilePath = null;
            string originalFileName = Path.GetFileName(filePath);

            try
            {
                using (var fs1 = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                using (var aes = new AesGcm(encryptionKey))
                {
                    int chunkCount = 0;

                    #region Extract header and check if file is a valid transformed file.
                    byte[] aesTag = new byte[_aesTagLength];
                    byte[] aesTagBase64 = new byte[aesTag.Length * 8 / 6];
                    byte[] encryptedHeader = new byte[HeaderSize * 8 / 6 - aesTagBase64.Length];
                    byte[] header = new byte[HeaderSize - aesTag.Length];

                    fs1.Read(encryptedHeader, 0, encryptedHeader.Length);
                    encryptedHeader = Base64ToBytes(encryptedHeader);

                    fs1.Read(aesTagBase64, 0, aesTagBase64.Length);
                    aesTag = Base64ToBytes(aesTagBase64);

                    aes.Decrypt(GenerateAESNonce(chunkCount, _aesTagLength), encryptedHeader, aesTag, header);

                    if (!_fileSignature.SequenceEqual(new ArraySegment<byte>(header, 0, _fileSignature.Length).ToArray()))
                        throw new FileFormatException();
                    #endregion

                    #region Extract original filename and chunks size from header
                    byte[] fileNameData = new ArraySegment<byte>(header, _fileSignature.Length, _fileNameHeaderLength).ToArray();
                    string restoredFileName = Encoding.UTF8.GetString(fileNameData).TrimEnd('\0'); // TrimEnd gets rid of header's null bytes

                    int chunksSize = BitConverter.ToInt32(new ArraySegment<byte>(header, _fileSignature.Length + fileNameData.Length, 4).ToArray()); // 4 is int length in bytes.
                    int encryptedChunkSize = chunksSize * 8 / 6;

                    newFilePath = Path.Combine(savePath, restoredFileName);
                    #endregion

                    chunkCount++;

                    using (var fs2 = new FileStream(newFilePath, FileMode.Create, FileAccess.Write))
                    {
                        byte[] base64AesTag = new byte[_aesTagLength * 8 / 6];
                        long bufferIndex = (HeaderSize * 8 / 6);
                        byte[] encryptedChunk = new byte[encryptedChunkSize];
                        byte[] restoredChunk = new byte[chunksSize];

                        #region File bytes conversion loop (using chunks)
                        while (bufferIndex <= fs1.Length - encryptedChunkSize)
                        {
                            fs1.Read(encryptedChunk, 0, encryptedChunk.Length);
                            restoredChunk = Base64ToBytes(encryptedChunk);
                            fs1.Read(base64AesTag, 0, base64AesTag.Length);
                            aesTag = Base64ToBytes(base64AesTag);
                            aes.Decrypt(GenerateAESNonce(chunkCount, _aesTagLength), restoredChunk, aesTag, restoredChunk);
                            fs2.Write(restoredChunk, 0, restoredChunk.Length);
                            bufferIndex += encryptedChunk.Length + base64AesTag.Length;
                            chunkCount++;

                            if (progressReporter != null)
                                progressReporter?.Report((int)Math.Round((double)bufferIndex / fs1.Length * 100));

                            if (ct.IsCancellationRequested)
                                throw new OperationCanceledException();
                        }
                        #endregion

                        #region Remaining bytes conversion
                        byte[] encryptedRemainder = new byte[(int)(fs1.Length - bufferIndex - base64AesTag.Length)];
                        byte[] decryptedRemainder;

                        fs1.Read(encryptedRemainder, 0, encryptedRemainder.Length);
                        decryptedRemainder = Base64ToBytes(encryptedRemainder);
                        fs1.Read(base64AesTag, 0, base64AesTag.Length);
                        aesTag = Base64ToBytes(base64AesTag);
                        aes.Decrypt(GenerateAESNonce(chunkCount, _aesTagLength), decryptedRemainder, aesTag, decryptedRemainder);
                        fs2.Write(decryptedRemainder, 0, decryptedRemainder.Length);
                        #endregion
                    }
                }
            }

            catch (Exception ex)
            {
                if (File.Exists(newFilePath))
                    File.Delete(newFilePath);

                if (ex is FileFormatException || ex is FormatException || ex is CryptographicException)
                    throw new FileFormatException($"Could not restore \"{originalFileName}\".");

                else
                    throw;
            }
            return newFilePath;
        }

        /// <summary>Convert bytes to Base64 string bytes</summary>
        /// <param name="bytes">Bytes to convert to Base64.</param>
        /// <returns>A byte array representation of the input bytes converted to Base64 bytes.</returns>
        /// <exception cref="IOException">Could not read original file / write to new file.</exception>
        /// <exception cref="FileFormatException">File is not a valid transformed file / Encryption key is wrong / File data was altered after it transformation.</exception>
        /// <exception cref="OperationCanceledException">Operation canceled.</exception> 
        private byte[] BytesToBase64(byte[] bytes)
        {
            return Encoding.UTF8.GetBytes(Convert.ToBase64String(bytes));
        }

        /// <summary>Convert Base64 byte representation to original bytes.</summary>
        /// <param name="bytes">Base64 bytes to convert original bytes.</param>
        /// <returns>A byte array of the original bytes.</returns>
        /// <exception cref="FormatException">Input bytes do not represent a valid Base64 string.</exception> 
        private byte[] Base64ToBytes(byte[] bytes)
        {
            return Convert.FromBase64String(Encoding.UTF8.GetString(bytes)); ;
        }

        /// <summary>Merge multiple byte arrays to a single byte array.</summary>
        /// <param name="byteArrayList">A list containing multiple byte arrays to merge.</param>
        /// <returns>A byte array containing bytes from multiple arrays merged into a single byte array.</returns>
        private byte[] MergeByteArrays(List<byte[]> byteArrayList)
        {
            int resultLength = 0;

            if (byteArrayList.Count == 0)
                return null;

            else if (byteArrayList.Count == 1)
                return byteArrayList[0];

            else
            {
                foreach (byte[] byteArray in byteArrayList)
                {
                    resultLength += byteArray.Length;
                }

                byte[] result = new byte[resultLength];

                int overallIndex = 0;

                for (int i = 0; i < byteArrayList.Count; i++)
                {
                    for (int j = 0; j < byteArrayList[i].Length; j++)
                    {
                        result[overallIndex] = byteArrayList[i][j];
                        overallIndex++;
                    }
                }

                return result;
            }
        }

        /// <summary>Generate a random byte array (used for nonce) from an int using MD5.</summary>
        /// <param name="num">An int to use for generating random byte array from.</param>
        /// <param name="length">Length (in bytes) of the result nonce.<br/>Length can be only values between 1-16.</param>
        /// <returns>A byte array representation of a the nonce.</returns>
        /// <exception cref="ArgumentException">Input length is less than 1 or more than 16.</exception> 
        private byte[] GenerateAESNonce(int num, int length)
        {
            if (length < 0 || length > 16)
                throw new ArgumentException("Length can be only values between 1-16.");

            byte[] nonce = MD5.HashData(BitConverter.GetBytes(num));
            Array.Resize(ref nonce, length);

            return nonce;
        }
    }
}