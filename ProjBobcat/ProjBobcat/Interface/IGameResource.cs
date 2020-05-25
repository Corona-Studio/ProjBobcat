namespace ProjBobcat.Interface
{
    public interface IGameResource
    {
        /// <summary>
        ///     下载目录
        /// </summary>
        string Path { get; set; }

        /// <summary>
        ///     标题
        /// </summary>
        string Title { get; set; }

        /// <summary>
        ///     文件类型
        /// </summary>
        string Type { get; set; }

        /// <summary>
        ///     Uri
        /// </summary>
        string Uri { get; set; }

        long FileSize { get; set; }
    }
}