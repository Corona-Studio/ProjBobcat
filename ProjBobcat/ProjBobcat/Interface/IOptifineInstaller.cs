using ProjBobcat.Class.Model.Optifine;
using System.Threading.Tasks;

namespace ProjBobcat.Interface
{
    public interface IOptifineInstaller : IInstaller
    {
        string RootPath { get; set; }
        string JavaExecutablePath { get; set; }
        string OptifineJarPath { get; set; }
        OptifineDownloadVersionModel OptifineDownloadVersion { get; set; }
        string Install();
        Task<string> InstallTaskAsync();
    }
}