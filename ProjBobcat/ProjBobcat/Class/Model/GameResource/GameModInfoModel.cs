using System.Collections.Generic;
using System.Text.Json.Serialization;

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

    public string[]? AuthorList { get; set; }

    public string? Parent { get; set; }

    public string[]? Screenshots { get; set; }

    public string[]? Dependencies { get; set; }
}

[JsonSerializable(typeof(GameModInfoModel))]
[JsonSerializable(typeof(List<GameModInfoModel>))]
partial class GameModInfoModelContext : JsonSerializerContext;