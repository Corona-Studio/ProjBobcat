using System;
using System.IO;
using System.Threading;

namespace ProjBobcat.Class.Helper
{
    public static class FileHelper
    {
        private static readonly object Locker = new object();

        /// <summary>
        /// 写入文件。
        /// </summary>
        /// <param name="path">路径。</param>
        /// <param name="content">内容。</param>
        public static void Write(string path, string content)
        {
            lock(Locker)
                File.WriteAllText(path, content);
            /*
            using var fs = new FileStream(path, FileMode.Create);
            var sw = new StreamWriter(fs);
            sw.Write(content);
            sw.Close();
            */
        }

        /// <summary>
        /// 将二进制数据写入文件。
        /// </summary>
        /// <param name="path">路径。</param>
        /// <param name="content">内容。</param>
        public static void Write(string path, byte[] content)
        {
            lock (Locker)
                File.WriteAllBytes(path, content);
        }

        /// <summary>
        /// 将二进制流写入到文件。
        /// </summary>
        /// <param name="stream">流。</param>
        /// <param name="fileName">路径。</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <returns>表示成功与否。</returns>
        public static bool SaveBinaryFile(Stream stream, string fileName)
        {
            if (stream == null) throw new ArgumentNullException();

            const int bufferSize = 1024;
            var result = true;

            try
            {
                /*
                if (File.Exists(fileName))
                    File.Delete(fileName);
                */
                // File.Create本身就会覆盖。
                lock (Locker)
                {
                    using var outStream = File.Create(fileName);

                    stream.CopyTo(outStream, bufferSize);
                    /*
                    int l;
                    do
                    {
                        l = stream.Read(buffer, 0, buffer.Length);
                        if (l > 0)
                            outStream.Write(buffer, 0, l);
                    } while (l > 0);

                    outStream.Close();
                    */
                }

                // stream.Close();
            }
            catch (ArgumentException)
            {
                result = false;
            }
            catch (IOException)
            {
                result = false;
            }
            catch (UnauthorizedAccessException)
            {
                result = false;
            }

            return result;
        }
    }
}