using System;
using System.Threading.Tasks;
using ProjBobcat.Class.Model;
using ProjBobcat.Event;

namespace ProjBobcat.Interface
{
    public interface IForgeInstaller
    {
        string RootPath { get; set; }
        string ForgeInstallPath { get; set; }
        Task<ForgeInstallResult> InstallForgeTaskAsync();

        event EventHandler<ForgeInstallStageChangedEventArgs> StageChangedEventDelegate;
    }
}