using System.Collections.Generic;
using ProjBobcat.Class.Model.Downloading;

namespace ProjBobcat.Class.Model;

public class ResourceCompleterCheckResult
{
    public bool IsLibDownloadFailed { get; init; }
    public required IReadOnlyCollection<DownloadFile> FailedFiles { get; init; }
}