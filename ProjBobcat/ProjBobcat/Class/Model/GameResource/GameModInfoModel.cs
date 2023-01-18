using System.Collections.Generic;

namespace ProjBobcat.Class.Model.GameResource;

public class GameModInfoModel
{
    public string? ModId { get; set; }

    public string? Name { get; set; }

    public string? Version { get; set; }

    public string? McVersion { get; set; }

    public string? Description { get; set; }

    public string? Credits { get; set; }

    public string? Url { get; set; }

    public string? UpdateUrl { get; set; }

    public List<string>? AuthorList { get; set; }

    public string? Parent { get; set; }

    public List<string>? Screenshots { get; set; }

    public List<string>? Dependencies { get; set; }
}