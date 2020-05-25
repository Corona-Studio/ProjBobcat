using ProjBobcat.Interface;

namespace ProjBobcat.Class.Model.GameResource
{
    public class AssetDownloadInfo : IGameResource
    {
        public string Path { get; set; }
        public string Title { get; set; }
        public string Type { get; set; }
        public string Uri { get; set; }
        public long FileSize { get; set; }
    }
}