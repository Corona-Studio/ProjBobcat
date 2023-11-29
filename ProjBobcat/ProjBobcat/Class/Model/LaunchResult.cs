using System;
using System.Diagnostics;
using ProjBobcat.Class.Model.YggdrasilAuth;

namespace ProjBobcat.Class.Model;

public class LaunchResult
{
    public LaunchErrorType ErrorType { get; init; }
    public LaunchSettings? LaunchSettings { get; init; }
    public ErrorModel? Error { get; init; }
    public TimeSpan RunTime { get; init; }
    public Process? GameProcess { get; init; }
}