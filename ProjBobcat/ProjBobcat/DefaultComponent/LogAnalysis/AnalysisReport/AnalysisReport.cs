using System.Collections.Generic;
using System.Linq;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.LogAnalysis.AnalysisReport;

public record AnalysisReport(CrashCauses Cause) : IAnalysisReport
{
    public string? From { get; set; }
    public IReadOnlyCollection<string>? Details { get; init; }
    public bool HasDetails => Details?.Any() ?? false;
}