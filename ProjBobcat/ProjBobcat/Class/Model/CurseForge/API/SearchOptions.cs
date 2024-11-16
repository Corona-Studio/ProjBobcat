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
            $"gameId={this.GameId ?? 432}";

        if (this.Index != null)
            result += $"&index={this.Index ?? 0}";
        if (this.Sort != null)
            result += $"&sortOrder={this.Sort ?? 1}";
        if (this.PageSize != null)
            result += $"&pageSize={this.PageSize}";
        if (!string.IsNullOrEmpty(this.GameVersion))
            result += $"&gameVersion={this.GameVersion}";
        if (!string.IsNullOrEmpty(this.SearchFilter))
            result += $"&searchFilter={this.SearchFilter}";
        if (this.ClassId != null && this.ParentCategoryId is null)
            result += $"&classId={this.ClassId}";
        if (this.CategoryId != null)
            result += $"&categoryId={this.CategoryId}";

        return result;
    }
}