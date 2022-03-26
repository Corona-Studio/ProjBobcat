using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ProjBobcat.Class.Model.Modrinth;

namespace ProjBobcat.Class.Helper;

public static class ModrinthAPIHelper
{
    const string BaseUrl = "https://api.modrinth.com/v2";

    static async Task<string> Get(string reqUrl)
    {
        using var req = await HttpHelper.Get(reqUrl);
        req.EnsureSuccessStatusCode();

        var resContent = await req.Content.ReadAsStringAsync();

        return resContent;
    }

    public static async Task<List<string>> GetCategories()
    {
        const string reqUrl = $"{BaseUrl}/tag/category";

        var resContent = await Get(reqUrl);
        var resModel = JsonConvert.DeserializeObject<List<ModrinthCategoryInfo>>(resContent);

        return resModel == null ? new List<string>() : resModel.Select(c => c.Name).ToList();
    }

    public static async Task<ModrinthProjectDependencyInfo> GetProjectDependenciesInfo(string projectId)
    {
        var reqUrl = $"{BaseUrl}/project/{projectId}/dependencies";

        var resContent = await Get(reqUrl);
        var resModel = JsonConvert.DeserializeObject<ModrinthProjectDependencyInfo>(resContent);

        return resModel;
    }

    public static async Task<ModrinthProjectInfo> GetProject(string projectId)
    {
        var reqUrl = $"{BaseUrl}/project/{projectId}";

        var resContent = await Get(reqUrl);
        var resModel = JsonConvert.DeserializeObject<ModrinthProjectInfo>(resContent);

        return resModel;
    }

    public static async Task<ModrinthSearchResult> GetFeaturedMods()
    {
        const string reqUrl = $"{BaseUrl}/search";

        var resContent = await Get(reqUrl);
        var resModel = JsonConvert.DeserializeObject<ModrinthSearchResult>(resContent);

        return resModel;
    }

    public static async Task<ModrinthSearchResult> SearchMod(ModrinthSearchOptions searchOptions)
    {
        var reqUrl = $"{BaseUrl}/search{searchOptions}";

        var resContent = await Get(reqUrl);
        var resModel = JsonConvert.DeserializeObject<ModrinthSearchResult>(resContent);

        return resModel;
    }

    public static async Task<List<ModrinthVersionInfo>> GetProjectVersions(string projectId)
    {
        var reqUrl = $"{BaseUrl}/project/{projectId}/version";

        var resContent = await Get(reqUrl);
        var resModel = JsonConvert.DeserializeObject<List<ModrinthVersionInfo>>(resContent);

        return resModel;
    }
}