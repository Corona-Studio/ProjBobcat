using ProjBobcat.DefaultComponent.LogAnalysis;
using System.Collections.Generic;

namespace ProjBobcat.Interface;

public interface IAnalysisReport
{
    CrashCauses Cause { get; }
    IReadOnlyCollection<string>? Details { get; }
    string? From { get; set; }
}