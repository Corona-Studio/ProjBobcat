using System;
using System.IO;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Class.Helper
{
    /// <summary>
    ///     文件操作帮助器。
    /// </summary>
    public static class FileHelper
    {
        private static readonly object locker = new object();

        /// <summary>
        ///     写入文件。
        /// </summary>
        /// <param name="path">路径。</param>
        /// <param name="content">内容。</param>
        public static void Write(string path, string content)
        {
            lock (locker)
            {
                File.WriteAllText(path, content);
            }
        }

        /// <summary>
        ///     将二进制数据写入文件。
        /// </summary>
        /// <param name="path">路径。</param>
        /// <param name="content">内容。</param>
        public static void Write(string path, byte[] content)
        {
            lock (locker)
            {
                File.WriteAllBytes(path, content);
            }
        }

        /// <summary>
        ///     将二进制流写入到文件。
        /// </summary>
        /// <param name="stream">流。</param>
        /// <param name="fileName">路径。</param>
        /// <param name="bufferSize">缓存区大小。</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <returns>表示成功与否。</returns>
        public static bool SaveBinaryFile(Stream stream, string fileName, int bufferSize = 1024)
        {
            try
            {
                lock (locker)
                {
                    using var outStream = File.Create(fileName);
                    stream.CopyTo(outStream, bufferSize);
                }
            }
            catch (NullReferenceException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            return true;
        }

        public static FileType GetFileType(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(fs);

            var b = new byte[2];
            var buffer = reader.ReadByte();

            b[0] = buffer;

            var fileClass = buffer.ToString();

            buffer = reader.ReadByte();
            b[1] = buffer;
            fileClass += buffer.ToString();

            var type = Enum.TryParse(fileClass, out FileType t) ? t : FileType.ValidFile;
            return type;
        }
    }
}