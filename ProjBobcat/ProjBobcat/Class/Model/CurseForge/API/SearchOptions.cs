namespace ProjBobcat.Class.Model.CurseForge.API;
#nullable enable
public class SearchOptions
{
    public int? SectionId { get; init; }
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
            $"&gameVersion={GameVersion ?? string.Empty}" +
            $"&index={Index ?? 0}" +
            $"&pageSize={PageSize ?? 12}" +
            $"&sort={Sort ?? 0}";

        if (SearchFilter != null)
            result += $"&searchFilter={SearchFilter}";
        if (SectionId != null)
            result += $"&sectionId={SectionId}";
        if (CategoryId != null)
            result += $"&categoryId={CategoryId}";

        return result;
    }
}
#nullable restore