using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ProjBobcat.Class.Model.CurseForge;
using ProjBobcat.Class.Model.CurseForge.API;
using ProjBobcat.Exceptions;

namespace ProjBobcat.Class.Helper;

#region Temp Models

record AddonInfoReqModel(IEnumerable<long> modIds);

record FileInfoReqModel(IEnumerable<long> fileIds);

record FuzzyFingerPrintReqModel(IEnumerable<long> fingerprints);

[JsonSerializable(typeof(AddonInfoReqModel))]
[JsonSerializable(typeof(FileInfoReqModel))]
[JsonSerializable(typeof(FuzzyFingerPrintReqModel))]
[JsonSerializable(typeof(DataModelWithPagination<CurseForgeAddonInfo[]>))]
[JsonSerializable(typeof(DataModel<CurseForgeAddonInfo>))]
[JsonSerializable(typeof(DataModel<CurseForgeAddonInfo[]>))]
[JsonSerializable(typeof(DataModel<CurseForgeLatestFileModel[]>))]
[JsonSerializable(typeof(DataModel<CurseForgeSearchCategoryModel[]>))]
[JsonSerializable(typeof(DataModel<CurseForgeFeaturedAddonModel>))]
[JsonSerializable(typeof(DataModel<CurseForgeFuzzySearchResponseModel>))]
[JsonSerializable(typeof(DataModel<string>))]
partial class CurseForgeModelContext : JsonSerializerContext;

#endregion

public static class CurseForgeAPIHelper
{
    const string BaseUrl = "https://api.curseforge.com/v1";

    static string ApiKey { get; set; } = null!;
    static HttpClient Client => HttpClientHelper.DefaultClient;

    static HttpRequestMessage Req(HttpMethod method, string url)
    {
        if (string.IsNullOrEmpty(ApiKey))
            throw new NullReferenceException("未设置 API KEY，请调用 SetApiKey(string apiKey) 来进行设置。");

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

        return await res.Content.ReadFromJsonAsync(CurseForgeModelContext.Default
            .DataModelWithPaginationCurseForgeAddonInfoArray);
    }

    public static async Task<CurseForgeAddonInfo?> GetAddon(long addonId)
    {
        var reqUrl = $"{BaseUrl}/mods/{addonId}";

        using var req = Req(HttpMethod.Get, reqUrl);
        using var res = await Client.SendAsync(req);

        return (await res.Content.ReadFromJsonAsync(CurseForgeModelContext.Default.DataModelCurseForgeAddonInfo))
            ?.Data;
    }

    public static async Task<CurseForgeAddonInfo[]?> GetAddons(IEnumerable<long> addonIds)
    {
        const string reqUrl = $"{BaseUrl}/mods";
        var data = JsonSerializer.Serialize(new AddonInfoReqModel(addonIds),
            CurseForgeModelContext.Default.AddonInfoReqModel);

        using var req = Req(HttpMethod.Post, reqUrl);
        req.Content = new StringContent(data, Encoding.UTF8, "application/json");

        using var res = await Client.SendAsync(req);

        res.EnsureSuccessStatusCode();

        return (await res.Content.ReadFromJsonAsync(CurseForgeModelContext.Default
            .DataModelCurseForgeAddonInfoArray))?.Data;
    }

    public static async Task<CurseForgeLatestFileModel[]?> GetAddonFiles(long addonId)
    {
        var reqUrl = $"{BaseUrl}/mods/{addonId}/files";

        using var req = Req(HttpMethod.Get, reqUrl);
        using var res = await Client.SendAsync(req);

        res.EnsureSuccessStatusCode();

        return (await res.Content.ReadFromJsonAsync(CurseForgeModelContext.Default
            .DataModelCurseForgeLatestFileModelArray))?.Data;
    }

    public static async Task<CurseForgeLatestFileModel[]?> GetFiles(IEnumerable<long> fileIds)
    {
        const string reqUrl = $"{BaseUrl}/mods/files";
        var data = JsonSerializer.Serialize(new FileInfoReqModel(fileIds),
            CurseForgeModelContext.Default.FileInfoReqModel);

        using var req = Req(HttpMethod.Post, reqUrl);
        req.Content = new StringContent(data, Encoding.UTF8, "application/json");

        using var res = await Client.SendAsync(req);

        res.EnsureSuccessStatusCode();

        return (await res.Content.ReadFromJsonAsync(CurseForgeModelContext.Default
            .DataModelCurseForgeLatestFileModelArray))?.Data;
    }

    public static async Task<CurseForgeSearchCategoryModel[]?> GetCategories(int gameId = 432)
    {
        var reqUrl = $"{BaseUrl}/categories?gameId={gameId}";

        using var req = Req(HttpMethod.Get, reqUrl);
        using var res = await Client.SendAsync(req);

        return (await res.Content.ReadFromJsonAsync(CurseForgeModelContext.Default
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

        return (await res.Content.ReadFromJsonAsync(CurseForgeModelContext.Default
            .DataModelCurseForgeFeaturedAddonModel))?.Data;
    }

    public static async Task<string?> GetAddonDownloadUrl(long addonId, long fileId)
    {
        var reqUrl = $"{BaseUrl}/mods/{addonId}/files/{fileId}/download-url";

        using var req = Req(HttpMethod.Get, reqUrl);
        using var res = await Client.SendAsync(req);

        if (!res.IsSuccessStatusCode)
            throw new CurseForgeModResolveException(addonId, fileId);

        return (await res.Content.ReadFromJsonAsync(CurseForgeModelContext.Default.DataModelString))?.Data;
    }

    public static async Task<string?> GetAddonDescriptionHtml(long addonId)
    {
        var reqUrl = $"{BaseUrl}/mods/{addonId}/description";

        using var req = Req(HttpMethod.Get, reqUrl);
        using var res = await Client.SendAsync(req);
        res.EnsureSuccessStatusCode();

        return (await res.Content.ReadFromJsonAsync(CurseForgeModelContext.Default.DataModelString))?.Data;
    }

    public static async Task<CurseForgeFuzzySearchResponseModel?> TryFuzzySearchFile(long[] fingerprint,
        int gameId = 432)
    {
        var reqUrl = $"{BaseUrl}/fingerprints/{gameId}";

        var data = JsonSerializer.Serialize(new FuzzyFingerPrintReqModel(fingerprint),
            CurseForgeModelContext.Default.FuzzyFingerPrintReqModel);

        using var req = Req(HttpMethod.Post, reqUrl);
        req.Content = new StringContent(data, Encoding.UTF8, "application/json");

        using var res = await Client.SendAsync(req);
        res.EnsureSuccessStatusCode();

        return (await res.Content.ReadFromJsonAsync(CurseForgeModelContext.Default
            .DataModelCurseForgeFuzzySearchResponseModel))?.Data;
    }
}