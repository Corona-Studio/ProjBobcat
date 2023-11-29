using System.Threading.Tasks;
using ProjBobcat.Class;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Interface;

public interface IForgeInstaller : IInstaller
{
    string DownloadUrlRoot { get; init; }
    string ForgeExecutablePath { get; init; }
    VersionLocatorBase VersionLocator { get; init; }

    ForgeInstallResult InstallForge();
    Task<ForgeInstallResult> InstallForgeTaskAsync();
}