using System.Collections.Generic;

namespace ProjBobcat.Interface;

public interface ILogAnalyzer
{
    string? RootPath { get; }
    string? GameId { get; }
    bool VersionIsolation { get; }
    IReadOnlyList<string>? CustomLogFiles { get; }
    IEnumerable<IAnalysisReport> GenerateReport();
}