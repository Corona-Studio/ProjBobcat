using System.Threading.Tasks;
using ProjBobcat.Class;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Interface;

public interface IForgeInstaller : IInstaller
{
    string DownloadUrlRoot { get; set; }
    string ForgeExecutablePath { get; set; }
    VersionLocatorBase VersionLocator { get; set; }

    ForgeInstallResult InstallForge();
    Task<ForgeInstallResult> InstallForgeTaskAsync();
}