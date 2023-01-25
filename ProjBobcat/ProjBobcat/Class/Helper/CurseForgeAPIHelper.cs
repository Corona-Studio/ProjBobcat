using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ProjBobcat.Class.Model.CurseForge;
using ProjBobcat.Class.Model.CurseForge.API;

namespace ProjBobcat.Class.Helper;

public static class CurseForgeAPIHelper
{
    const string BaseUrl = "https://api.curseforge.com/v1";

    static string ApiKey { get; set; }
    static HttpClient Client => HttpClientHelper.GetNewClient(HttpClientHelper.DefaultClientName);

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

    public static async Task<DataModelWithPagination<List<CurseForgeAddonInfo>>?> SearchAddons(SearchOptions options)
    {
        var reqUrl = $"{BaseUrl}/mods/search{options}";

        using var req = Req(HttpMethod.Get, reqUrl);
        using var res = await Client.SendAsync(req);

        return await res.Content.ReadFromJsonAsync<DataModelWithPagination<List<CurseForgeAddonInfo>>>();
    }

    public static async Task<CurseForgeAddonInfo?> GetAddon(int addonId)
    {
        var reqUrl = $"{BaseUrl}/mods/{addonId}";

        using var req = Req(HttpMethod.Get, reqUrl);
        using var res = await Client.SendAsync(req);

        return (await res.Content.ReadFromJsonAsync<DataModel<CurseForgeAddonInfo>>())?.Data;
    }

    public static async Task<List<CurseForgeAddonInfo>?> GetAddons(IEnumerable<int> addonIds)
    {
        const string reqUrl = $"{BaseUrl}/mods";
        var data = JsonSerializer.Serialize(new
        {
            modIds = addonIds
        });

        using var req = Req(HttpMethod.Post, reqUrl);
        req.Content = new StringContent(data, Encoding.UTF8, "application/json");

        using var res = await Client.SendAsync(req);

        res.EnsureSuccessStatusCode();

        return (await res.Content.ReadFromJsonAsync<DataModel<List<CurseForgeAddonInfo>>>())?.Data;
    }

    public static async Task<List<CurseForgeLatestFileModel>?> GetAddonFiles(int addonId)
    {
        var reqUrl = $"{BaseUrl}/mods/{addonId}/files";

        using var req = Req(HttpMethod.Get, reqUrl);
        using var res = await Client.SendAsync(req);

        return (await res.Content.ReadFromJsonAsync<DataModel<List<CurseForgeLatestFileModel>>>())?.Data;
    }

    public static async Task<List<CurseForgeSearchCategoryModel>?> GetCategories(int gameId = 432)
    {
        var reqUrl = $"{BaseUrl}/categories?gameId={gameId}";

        using var req = Req(HttpMethod.Get, reqUrl);
        using var res = await Client.SendAsync(req);

        return (await res.Content.ReadFromJsonAsync<DataModel<List<CurseForgeSearchCategoryModel>>>())?.Data;
    }

    public static async Task<CurseForgeFeaturedAddonModel?> GetFeaturedAddons(FeaturedQueryOptions options)
    {
        const string reqUrl = $"{BaseUrl}/mods/featured";
        var reqJson = JsonSerializer.Serialize(options);

        using var req = Req(HttpMethod.Post, reqUrl);
        req.Content = new StringContent(reqJson, Encoding.UTF8, "application/json");

        using var res = await Client.SendAsync(req);
        res.EnsureSuccessStatusCode();

        return (await res.Content.ReadFromJsonAsync<DataModel<CurseForgeFeaturedAddonModel>>())?.Data;
    }

    public static async Task<string?> GetAddonDownloadUrl(long addonId, long fileId)
    {
        var reqUrl = $"{BaseUrl}/mods/{addonId}/files/{fileId}/download-url";

        using var req = Req(HttpMethod.Get, reqUrl);
        using var res = await Client.SendAsync(req);
        res.EnsureSuccessStatusCode();

        return (await res.Content.ReadFromJsonAsync<DataModel<string>>())?.Data;
    }

    public static async Task<string?> GetAddonDescriptionHtml(long addonId)
    {
        var reqUrl = $"{BaseUrl}/mods/{addonId}/description";

        using var req = Req(HttpMethod.Get, reqUrl);
        using var res = await Client.SendAsync(req);
        res.EnsureSuccessStatusCode();

        return (await res.Content.ReadFromJsonAsync<DataModel<string>>())?.Data;
    }
}