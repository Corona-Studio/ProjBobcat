namespace ProjBobcat.Class.Model
{
    /// <summary>
    ///     下载范围类
    /// </summary>
    public class DownloadRange
    {
        /// <summary>
        ///     开始字节
        /// </summary>
        public long Start { get; set; }

        /// <summary>
        ///     结束字节
        /// </summary>
        public long End { get; set; }

        /// <summary>
        ///     临时文件名称
        /// </summary>
        public string TempFileName { get; set; }
    }
}