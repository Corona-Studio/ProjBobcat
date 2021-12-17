using System.Threading.Tasks;
using ProjBobcat.Class.Model.Optifine;

namespace ProjBobcat.Interface;

public interface IOptifineInstaller : IInstaller
{
    string JavaExecutablePath { get; set; }
    string OptifineJarPath { get; set; }
    OptifineDownloadVersionModel OptifineDownloadVersion { get; set; }
    string Install();
    Task<string> InstallTaskAsync();
}