using System;
using ProjBobcat.Event;

namespace ProjBobcat.Interface
{
    public interface IInstaller
    {
        string RootPath { get; set; }
        event EventHandler<InstallerStageChangedEventArgs> StageChangedEventDelegate;
    }
}