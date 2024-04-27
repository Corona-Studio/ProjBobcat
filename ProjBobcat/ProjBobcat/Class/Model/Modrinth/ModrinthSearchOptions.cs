using System.Collections.Generic;
using System.Text;

namespace ProjBobcat.Class.Model.Modrinth;

public class ModrinthSearchOptions
{
    public string? Name { get; init; }
    public string? Category { get; init; }
    public string Index { get; init; } = "relevance";
    public string? ProjectType { get; init; }
    public int? Offset { get; init; }
    public int? Limit { get; set; }

    public override string ToString()
    {
        var sb = new StringBuilder($"?query={Name ?? "any"}&index={Index}");

        var facets = new List<string>();

        if (!string.IsNullOrEmpty(Category))
            facets.Add($"[\"categories:{Category}\"]");
        if (!string.IsNullOrEmpty(ProjectType))
            facets.Add($"[\"project_type:{ProjectType}\"]");

        if (facets.Count > 0)
            sb.Append("&facets=[")
                .AppendJoin(',', facets)
                .Append(']');

        if (Offset != null) sb.Append($"&offset={Offset}");
        if (Limit != null) sb.Append($"&limit={Limit}");

        return sb.ToString();
    }
}