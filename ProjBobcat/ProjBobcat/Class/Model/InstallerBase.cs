using ProjBobcat.Interface;

namespace ProjBobcat.Class.Model;

public abstract class InstallerBase : ProgressReportBase, IInstaller
{
    public abstract string RootPath { get; init; }
    public string? CustomId { get; init; }
    public string? InheritsFrom { get; init; }
}