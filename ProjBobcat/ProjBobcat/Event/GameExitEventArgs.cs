using System;

namespace ProjBobcat.Event;

public class GameExitEventArgs : EventArgs
{
    public Exception? Exception { get; init; }

    public int ExitCode { get; init; }
}