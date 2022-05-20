namespace ProjBobcat.Class.Model.CurseForge.API;
#nullable enable
public class SearchOptions
{
    public int? CategoryId { get; init; }
    public int? GameId { get; init; }
    public string? GameVersion { get; init; }
    public int? Index { get; init; }
    public int? PageSize { get; init; }
    public string? SearchFilter { get; init; }
    public int? Sort { get; init; }


    public override string ToString()
    {
        var result =
            "?" +
            $"gameId={GameId ?? 432}" +
            $"&index={Index ?? 0}" +
            $"&pageSize={PageSize ?? 12}" +
            $"&sortOrder={Sort ?? 1}";

        if (!string.IsNullOrEmpty(GameVersion))
            result += $"&gameVersion={GameVersion}";
        if (!string.IsNullOrEmpty(SearchFilter))
            result += $"&searchFilter={SearchFilter}";
        if (CategoryId is { })
            result += $"&classId={CategoryId}";

        return result;
    }
}
#nullable restore