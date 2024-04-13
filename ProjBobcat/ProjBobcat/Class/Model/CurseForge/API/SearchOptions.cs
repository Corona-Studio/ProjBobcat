namespace ProjBobcat.Class.Model.CurseForge.API;

public class SearchOptions
{
    public int? CategoryId { get; init; }
    public int? ClassId { get; init; }
    public int? ParentCategoryId { get; init; }
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
            $"gameId={GameId ?? 432}";

        if (Index != null)
            result += $"&index={Index ?? 0}";
        if (Sort != null)
            result += $"&sortOrder={Sort ?? 1}";
        if (PageSize != null)
            result += $"&pageSize={PageSize}";
        if (!string.IsNullOrEmpty(GameVersion))
            result += $"&gameVersion={GameVersion}";
        if (!string.IsNullOrEmpty(SearchFilter))
            result += $"&searchFilter={SearchFilter}";
        if (ClassId != null && ParentCategoryId is null)
            result += $"&classId={ClassId}";
        if (CategoryId != null)
            result += $"&categoryId={CategoryId}";

        return result;
    }
}