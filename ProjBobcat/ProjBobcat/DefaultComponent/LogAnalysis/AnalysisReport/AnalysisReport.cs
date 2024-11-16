using System.Collections.Generic;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.LogAnalysis.AnalysisReport;

public record AnalysisReport(CrashCauses Cause) : IAnalysisReport
{
    public string? From { get; set; }
    public IReadOnlyCollection<string>? Details { get; set; }
    public bool HasDetails => this.Details is { Count: > 0 };
}