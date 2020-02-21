using System;
using System.Threading.Tasks;
using ProjBobcat.Class.Model;
using ProjBobcat.Event;

namespace ProjBobcat.Interface
{
    public interface IForgeInstaller
    {
        string ForgeExecutablePath { get; set; }
        string ForgeInstallPath { get; set; }
        ForgeInstallResult InstallForge();
        Task<ForgeInstallResult> InstallForgeTaskAsync();

        event EventHandler<ForgeInstallStageChangedEventArgs> StageChangedEventDelegate;
    }
}