using System.Threading.Tasks;
using ProjBobcat.Class.Model.LiteLoader;

namespace ProjBobcat.Interface
{
    public interface ILiteLoaderInstaller : IInstaller
    {
        string Install(LiteLoaderDownloadVersionModel versionModel);
        Task<string> InstallTaskAsync(LiteLoaderDownloadVersionModel versionModel);
    }
}