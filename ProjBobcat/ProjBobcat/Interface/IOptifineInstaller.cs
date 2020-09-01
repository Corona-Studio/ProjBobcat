using System.Threading.Tasks;
using ProjBobcat.Class.Model.Optifine;

namespace ProjBobcat.Interface
{
    public interface IOptifineInstaller : IInstaller
    {
        string OptifineExecutablePath { get; set; }
        string Install(OptifineDownloadVersionModel versionModel);
        Task<string> InstallTaskAsync(OptifineDownloadVersionModel versionModel);
    }
}