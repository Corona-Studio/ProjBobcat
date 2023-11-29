using System.Threading.Tasks;
using ProjBobcat.Class.Model.Optifine;

namespace ProjBobcat.Interface;

public interface IOptifineInstaller : IInstaller
{
    string JavaExecutablePath { get; init; }
    string OptifineJarPath { get; init; }
    OptifineDownloadVersionModel OptifineDownloadVersion { get; init; }
    string Install();
    Task<string> InstallTaskAsync();
}