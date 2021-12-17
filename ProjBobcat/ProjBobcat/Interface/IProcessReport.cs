using System;
using ProjBobcat.Event;

namespace ProjBobcat.Interface;

public interface IProgressReport
{
    event EventHandler<StageChangedEventArgs> StageChangedEventDelegate;
}