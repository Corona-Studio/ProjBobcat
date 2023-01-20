using ProjBobcat.Interface;

namespace ProjBobcat.Class.Model;

public abstract class InstallerBase : ProgressReportBase, IInstaller
{
    public string? RootPath { get; set; }
    public string? CustomId { get; set; }
    public string? InheritsFrom { get; set; }
}