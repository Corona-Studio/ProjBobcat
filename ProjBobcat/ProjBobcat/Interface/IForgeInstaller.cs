using ProjBobcat.Class.Model;
using System.Threading.Tasks;

namespace ProjBobcat.Interface
{
    public interface IForgeInstaller : IInstaller
    {
        string ForgeExecutablePath { get; set; }
        ForgeInstallResult InstallForge();
        Task<ForgeInstallResult> InstallForgeTaskAsync();
    }
}