using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ProjBobcat.Class.Model.CurseForge;
using ProjBobcat.Class.Model.CurseForge.API;

namespace ProjBobcat.Class.Helper;

#region Temp Models

record AddonInfoReqModel(IEnumerable<int> modIds);

[JsonSerializable(typeof(AddonInfoReqModel))]
partial class AddonInfoReqModelContext : JsonSerializerContext
{
}

[JsonSerializable(typeof(DataModelWithPagination<CurseForgeAddonInfo[]>))]
partial class SearchAddonsResultModelContext : JsonSerializerContext
{
}

[JsonSerializable(typeof(DataModel<CurseForgeAddonInfo>))]
partial class GetAddonResultModelContext : JsonSerializerContext
{
}

[JsonSerializable(typeof(DataModel<CurseForgeAddonInfo[]>))]
partial class GetAddonsResultModelContext : JsonSerializerContext
{
}

[JsonSerializable(typeof(DataModel<CurseForgeLatestFileModel[]>))]
partial class GetAddonFilesResultModelContext : JsonSerializerContext
{
}

[JsonSerializable(typeof(DataModel<CurseForgeSearchCategoryModel[]>))]
partial class GetCategoriesResultContext : JsonSerializerContext
{
}

[JsonSerializable(typeof(DataModel<CurseForgeFeaturedAddonModel>))]
partial class GetFeaturedAddonsResultContext : JsonSerializerContext
{
}

[JsonSerializable(typeof(DataModel<string>))]
partial class DataModelStringResultContext : JsonSerializerContext
{
}

#endregion

public static class CurseForgeAPIHelper
{
    const string BaseUrl = "https://api.curseforge.com/v1";

    static string ApiKey { get; set; }
    static HttpClient Client => HttpClientHelper.DefaultClient;

    static HttpRequestMessage Req(HttpMethod method, string url)
    {
        var req = new HttpRequestMessage(method, url);

        req.Headers.Add("x-api-key", ApiKey);

        return req;
    }

    public static void SetApiKey(string apiKey)
    {
        ApiKey = apiKey;
    }

    public static async Task<DataModelWithPagination<CurseForgeAddonInfo[]>?> SearchAddons(SearchOptions options)
    {
        var reqUrl = $"{BaseUrl}/mods/search{options}";

        using var req = Req(HttpMethod.Get, reqUrl);
        using var res = await Client.SendAsync(req);

        return await res.Content.ReadFromJsonAsync(SearchAddonsResultModelContext.Default
            .DataModelWithPaginationCurseForgeAddonInfoArray);
    }

    public static async Task<CurseForgeAddonInfo?> GetAddon(int addonId)
    {
        var reqUrl = $"{BaseUrl}/mods/{addonId}";

        using var req = Req(HttpMethod.Get, reqUrl);
        using var res = await Client.SendAsync(req);

        return (await res.Content.ReadFromJsonAsync(GetAddonResultModelContext.Default.DataModelCurseForgeAddonInfo))
            ?.Data;
    }

    public static async Task<CurseForgeAddonInfo[]?> GetAddons(IEnumerable<int> addonIds)
    {
        const string reqUrl = $"{BaseUrl}/mods";
        var data = JsonSerializer.Serialize(new AddonInfoReqModel(addonIds),
            AddonInfoReqModelContext.Default.AddonInfoReqModel);

        using var req = Req(HttpMethod.Post, reqUrl);
        req.Content = new StringContent(data, Encoding.UTF8, "application/json");

        using var res = await Client.SendAsync(req);

        res.EnsureSuccessStatusCode();

        return (await res.Content.ReadFromJsonAsync(GetAddonsResultModelContext.Default
            .DataModelCurseForgeAddonInfoArray))?.Data;
    }

    public static async Task<CurseForgeLatestFileModel[]?> GetAddonFiles(int addonId)
    {
        var reqUrl = $"{BaseUrl}/mods/{addonId}/files";

        using var req = Req(HttpMethod.Get, reqUrl);
        using var res = await Client.SendAsync(req);

        return (await res.Content.ReadFromJsonAsync(GetAddonFilesResultModelContext.Default
            .DataModelCurseForgeLatestFileModelArray))?.Data;
    }

    public static async Task<CurseForgeSearchCategoryModel[]?> GetCategories(int gameId = 432)
    {
        var reqUrl = $"{BaseUrl}/categories?gameId={gameId}";

        using var req = Req(HttpMethod.Get, reqUrl);
        using var res = await Client.SendAsync(req);

        return (await res.Content.ReadFromJsonAsync(GetCategoriesResultContext.Default
            .DataModelCurseForgeSearchCategoryModelArray))?.Data;
    }

    public static async Task<CurseForgeFeaturedAddonModel?> GetFeaturedAddons(FeaturedQueryOptions options)
    {
        const string reqUrl = $"{BaseUrl}/mods/featured";
        var reqJson = JsonSerializer.Serialize(options, FeaturedQueryOptionsContext.Default.FeaturedQueryOptions);

        using var req = Req(HttpMethod.Post, reqUrl);
        req.Content = new StringContent(reqJson, Encoding.UTF8, "application/json");

        using var res = await Client.SendAsync(req);
        res.EnsureSuccessStatusCode();

        return (await res.Content.ReadFromJsonAsync(GetFeaturedAddonsResultContext.Default
            .DataModelCurseForgeFeaturedAddonModel))?.Data;
    }

    public static async Task<string?> GetAddonDownloadUrl(long addonId, long fileId)
    {
        var reqUrl = $"{BaseUrl}/mods/{addonId}/files/{fileId}/download-url";

        using var req = Req(HttpMethod.Get, reqUrl);
        using var res = await Client.SendAsync(req);
        res.EnsureSuccessStatusCode();

        return (await res.Content.ReadFromJsonAsync(DataModelStringResultContext.Default.DataModelString))?.Data;
    }

    public static async Task<string?> GetAddonDescriptionHtml(long addonId)
    {
        var reqUrl = $"{BaseUrl}/mods/{addonId}/description";

        using var req = Req(HttpMethod.Get, reqUrl);
        using var res = await Client.SendAsync(req);
        res.EnsureSuccessStatusCode();

        return (await res.Content.ReadFromJsonAsync(DataModelStringResultContext.Default.DataModelString))?.Data;
    }
}