using System.Text;

namespace ProjBobcat.Class.Model.Modrinth;

public class ModrinthSearchOptions
{
    public string Name { get; init; }
    public string? Category { get; init; }
    public string Index { get; init; } = "relevance";
    public string? ProjectType { get; init; } = "mod";
    public int? Offset { get; init; }
    public int? Limit { get; set; }

    public override string ToString()
    {
        var sb = new StringBuilder($"?query={Name ?? "any"}&index={Index}&facets=[");
        var projType = $"[\"project_type:{ProjectType}\"]";

        if (!string.IsNullOrEmpty(Category)) sb.Append($"[\"categories:{Category}\"],");

        sb.Append(projType);
        sb.Append(']');

        if (Offset != null) sb.Append($"&offset={Offset}");
        if (Limit != null) sb.Append($"&limit={Limit}");

        return sb.ToString();
    }
}