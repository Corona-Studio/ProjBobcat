using ProjBobcat.Class.Model;
using ProjBobcat.Event;
using ProjBobcat.Interface;
using System;
using System.Threading.Tasks;

namespace ProjBobcat.DefaultComponent.ForgeInstaller
{
    public class LegacyForgeInstaller : IForgeInstaller
    {
        public string RootPath { get; set; }
        public string ForgeInstallPath { get; set; }

        public event EventHandler<ForgeInstallStageChangedEventArgs> StageChangedEventDelegate;

        public Task<ForgeInstallResult> InstallForgeTaskAsync()
        {
            throw new NotImplementedException();
        }
    }
}