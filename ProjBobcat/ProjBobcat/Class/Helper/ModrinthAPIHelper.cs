using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Modrinth;

namespace ProjBobcat.Class.Helper;

public static class ModrinthAPIHelper
{
    const string BaseUrl = "https://api.modrinth.com/v2";

    static async Task<HttpResponseMessage> Get(string reqUrl, CancellationToken ct = default)
    {
        var req = await HttpHelper.Get(reqUrl, ct: ct);
        req.EnsureSuccessStatusCode();

        return req;
    }

    public static async Task<ModrinthCategoryInfo[]?> GetCategories()
    {
        const string reqUrl = $"{BaseUrl}/tag/category";

        using var res = await Get(reqUrl);
        var resModel =
            await res.Content.ReadFromJsonAsync(ModrinthCategoryInfoContext.Default.ModrinthCategoryInfoArray);

        return resModel;
    }

    public static async Task<ModrinthProjectDependencyInfo?> GetProjectDependenciesInfo(string projectId)
    {
        var reqUrl = $"{BaseUrl}/project/{projectId}/dependencies";

        using var res = await Get(reqUrl);
        var resModel =
            await res.Content.ReadFromJsonAsync(ModrinthProjectDependencyInfoContext.Default
                .ModrinthProjectDependencyInfo);

        return resModel;
    }

    public static async Task<ModrinthProjectInfo?> GetProject(string projectId, CancellationToken ct)
    {
        var reqUrl = $"{BaseUrl}/project/{projectId}";

        using var res = await Get(reqUrl, ct);
        var resModel = await res.Content.ReadFromJsonAsync(ModrinthProjectInfoContext.Default.ModrinthProjectInfo, ct);

        return resModel;
    }

    public static async Task<ModrinthSearchResult?> GetFeaturedMods()
    {
        const string reqUrl = $"{BaseUrl}/search";

        using var res = await Get(reqUrl);
        var resModel = await res.Content.ReadFromJsonAsync(ModrinthSearchResultContext.Default.ModrinthSearchResult);

        return resModel;
    }

    public static async Task<ModrinthSearchResult?> SearchMod(ModrinthSearchOptions searchOptions)
    {
        var reqUrl = $"{BaseUrl}/search{searchOptions}";

        using var res = await Get(reqUrl);
        var resModel = await res.Content.ReadFromJsonAsync(ModrinthSearchResultContext.Default.ModrinthSearchResult);

        return resModel;
    }

    public static async Task<ModrinthVersionInfo[]?> GetProjectVersions(string projectId)
    {
        var reqUrl = $"{BaseUrl}/project/{projectId}/version";

        using var res = await Get(reqUrl);
        var resModel = await res.Content.ReadFromJsonAsync(ModrinthVersionInfoContext.Default.ModrinthVersionInfoArray);

        return resModel;
    }

    public static async Task<ModrinthVersionInfo?> TryMatchVersionFileByHash(
        string hash,
        HashType hashType)
    {
        var para = hashType switch
        {
            HashType.SHA1 => "?algorithm=sha1",
            HashType.SHA512 => "?algorithm=sha512",
            _ => throw new ArgumentOutOfRangeException(nameof(hashType), hashType, null)
        };
        var reqUrl = $"{BaseUrl}/version_file/{hash}{para}";

        using var res = await Get(reqUrl);
        var resModel = await res.Content.ReadFromJsonAsync(ModrinthVersionInfoContext.Default.ModrinthVersionInfo);

        return resModel;
    }

    public static async Task<ModrinthVersionInfo?> GetVersionInfo(string projectId, string versionId)
    {
        var reqUrl = $"{BaseUrl}/project/{projectId}/version/{versionId}";

        using var res = await Get(reqUrl);
        var resModel = await res.Content.ReadFromJsonAsync(ModrinthVersionInfoContext.Default.ModrinthVersionInfo);

        return resModel;
    }
}