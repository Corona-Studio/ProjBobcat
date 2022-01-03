using System.Collections.Generic;

namespace ProjBobcat.Class.Model;

public class ResourceCompleterCheckResult
{
    public bool IsLibDownloadFailed { get; set; }
    public IEnumerable<DownloadFile> FailedFiles { get; set; }
}