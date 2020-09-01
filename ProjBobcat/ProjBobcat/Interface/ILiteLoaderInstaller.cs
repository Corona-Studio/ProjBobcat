using ProjBobcat.Class.Model.LiteLoader;
using System.Threading.Tasks;

namespace ProjBobcat.Interface
{
    public interface ILiteLoaderInstaller : IInstaller
    {
        string Install(LiteLoaderDownloadVersionModel versionModel);
        Task<string> InstallTaskAsync(LiteLoaderDownloadVersionModel versionModel);
    }
}