using System.Collections.Generic;
using ProjBobcat.DefaultComponent.LogAnalysis;

namespace ProjBobcat.Interface;

public interface IAnalysisReport
{
    CrashCauses Cause { get; }
    IReadOnlyCollection<string>? Details { get; }
    string? From { get; set; }
    bool HasDetails { get; }
}