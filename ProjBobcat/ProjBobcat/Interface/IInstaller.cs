using System;
using ProjBobcat.Event;

namespace ProjBobcat.Interface
{
    public interface IInstaller
    {
        string CustomId { get; set; }
        string RootPath { get; set; }
        event EventHandler<InstallerStageChangedEventArgs> StageChangedEventDelegate;
    }
}