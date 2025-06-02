using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjBobcat.Class.Model.Modrinth;

public class ModrinthSearchOptions
{
    public string? Name { get; init; }
    public string[]? Categories { get; init; }
    public string? GameVersion { get; init; }
    public string Index { get; init; } = "relevance";
    public string? ProjectType { get; init; }
    public int? Offset { get; init; }
    public int? Limit { get; set; }

    public override string ToString()
    {
        var query = string.IsNullOrEmpty(this.Name)
            ? string.Empty
            : $"query={this.Name}";

        string[] initQueries =
        [
            query,
            $"index={this.Index}"
        ];

        var sb = new StringBuilder($"?{string.Join('&', initQueries.Where(q => !string.IsNullOrEmpty(q)))}");
        var facets = new List<string>();

        if (!string.IsNullOrEmpty(this.GameVersion))
            facets.Add($"[\"versions:{this.GameVersion}\"]");

        var filteredCategories = Categories?
            .Distinct()
            .Where(c => !string.IsNullOrEmpty(c))
            .Select(c => $"\"categories:{c}\"")
            .ToArray();

        if (filteredCategories is { Length: > 0 })
            facets.Add($"[{string.Join(',', filteredCategories)}]");
        if (!string.IsNullOrEmpty(this.ProjectType))
            facets.Add($"[\"project_type:{this.ProjectType}\"]");

        if (facets.Count > 0)
            sb.Append("&facets=[")
                .AppendJoin(',', facets)
                .Append(']');

        if (this.Offset != null) sb.Append($"&offset={this.Offset}");
        if (this.Limit != null) sb.Append($"&limit={this.Limit}");

        return sb.ToString();
    }
}