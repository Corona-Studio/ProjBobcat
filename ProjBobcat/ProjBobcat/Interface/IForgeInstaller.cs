using System.Threading.Tasks;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Interface
{
    public interface IForgeInstaller : IInstaller
    {
        string ForgeExecutablePath { get; set; }
        ForgeInstallResult InstallForge();
        Task<ForgeInstallResult> InstallForgeTaskAsync();
    }
}