using System.Collections.Generic;

namespace ProjBobcat.Class.Model;

public class ResourceCompleterCheckResult
{
    public bool IsLibDownloadFailed { get; init; }
    public required IReadOnlyCollection<DownloadFile> FailedFiles { get; init; }
}