namespace ProjBobcat.Class.Model
{
    /// <summary>
    /// 下载范围类
    /// </summary>
    public class DownloadRange
    {
        /// <summary>
        /// 分片索引
        /// </summary>
        public int Index { get; set; }
        /// <summary>
        /// 开始字节
        /// </summary>
        public long Start { get; set; }
        /// <summary>
        /// 结束字节
        /// </summary>
        public long End { get; set; }
    }
}