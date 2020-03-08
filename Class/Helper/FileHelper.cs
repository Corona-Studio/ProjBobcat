using System;
using System.IO;
using System.Threading;

namespace ProjBobcat.Class.Helper
{
    public static class FileHelper
    {
        private static readonly ReaderWriterLock Locker = new ReaderWriterLock();

        /// <summary>
        ///     写入文件
        /// </summary>
        /// <param name="path">路径</param>
        /// <param name="content">内容</param>
        public static void Write(string path, string content)
        {
            using var fs = new FileStream(path, FileMode.Create);
            var sw = new StreamWriter(fs);
            sw.Write(content);
            sw.Close();
        }

        /// <summary>
        ///     将二进制文件保存到磁盘
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="fileName"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool SaveBinaryFile(Stream stream, string fileName)
        {
            if (stream == null) throw new ArgumentNullException();

            var value = true;
            var buffer = new byte[1024];

            try
            {
                if (File.Exists(fileName))
                    File.Delete(fileName);

                lock (Locker)
                {
                    using var outStream = File.Create(fileName);
                    int l;
                    do
                    {
                        l = stream.Read(buffer, 0, buffer.Length);
                        if (l > 0)
                            outStream.Write(buffer, 0, l);
                    } while (l > 0);

                    outStream.Close();
                }

                stream.Close();
            }
            catch (ArgumentException)
            {
                value = false;
            }
            catch (IOException)
            {
                value = false;
            }
            catch (UnauthorizedAccessException)
            {
                value = false;
            }

            return value;
        }
    }
}