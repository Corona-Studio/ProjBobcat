using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ProjBobcat.Class.Model.CurseForge;
using ProjBobcat.Class.Model.CurseForge.API;
using ProjBobcat.Exceptions;
using ProjBobcat.Interface;
using ProjBobcat.Interface.Services;

namespace ProjBobcat.Services;

#region Temp Models

record AddonInfoReqModel([property: JsonPropertyName("modIds")] IEnumerable<long> ModIds);

record FileInfoReqModel([property: JsonPropertyName("fileIds")] IEnumerable<long> FileIds);

record FuzzyFingerPrintReqModel([property: JsonPropertyName("fingerprints")] IEnumerable<long> Fingerprints);

[JsonSerializable(typeof(AddonInfoReqModel))]
[JsonSerializable(typeof(FileInfoReqModel))]
[JsonSerializable(typeof(FuzzyFingerPrintReqModel))]
[JsonSerializable(typeof(DataModelWithPagination<CurseForgeAddonInfo[]>))]
[JsonSerializable(typeof(DataModel<CurseForgeAddonInfo>))]
[JsonSerializable(typeof(DataModel<CurseForgeAddonInfo[]>))]
[JsonSerializable(typeof(DataModel<CurseForgeLatestFileModel[]>))]
[JsonSerializable(typeof(DataModelWithPagination<CurseForgeLatestFileModel[]>))]
[JsonSerializable(typeof(DataModel<CurseForgeSearchCategoryModel[]>))]
[JsonSerializable(typeof(DataModel<CurseForgeFeaturedAddonModel>))]
[JsonSerializable(typeof(DataModel<CurseForgeFuzzySearchResponseModel>))]
[JsonSerializable(typeof(DataModel<string>))]
partial class CurseForgeModelContext : JsonSerializerContext;

#endregion

public class CurseForgeApiService(
    HttpClient httpClient,
    ILauncherCoreSettingsProvider settingsProvider) : ICurseForgeApiService
{
    private const string DefaultApiUrl = "https://api.curseforge.com/v1";

    private string GetApiRoot()
    {
        var customRoot = settingsProvider.CurseForgeApiBaseUrl();

        if (!string.IsNullOrEmpty(customRoot))
            return customRoot;

        return DefaultApiUrl;
    }

    public async Task<DataModelWithPagination<CurseForgeAddonInfo[]>?> SearchAddons(SearchOptions options, CancellationToken ct)
    {
        var reqUrl = $"{GetApiRoot()}/mods/search{options}";

        using var res = await httpClient.GetAsync(reqUrl, ct);

        if (!res.IsSuccessStatusCode)
            return null;

        return await res.Content.ReadFromJsonAsync(CurseForgeModelContext.Default
            .DataModelWithPaginationCurseForgeAddonInfoArray, ct);
    }

    public async Task<CurseForgeAddonInfo?> GetAddon(long addonId)
    {
        var reqUrl = $"{GetApiRoot()}/mods/{addonId}";

        using var res = await httpClient.GetAsync(reqUrl);

        return (await res.Content.ReadFromJsonAsync(CurseForgeModelContext.Default.DataModelCurseForgeAddonInfo))
            ?.Data;
    }

    public async Task<CurseForgeAddonInfo[]?> GetAddons(IEnumerable<long> addonIds)
    {
        var reqUrl = $"{GetApiRoot()}/mods";

        var content = JsonContent.Create(
            new AddonInfoReqModel(addonIds),
            CurseForgeModelContext.Default.AddonInfoReqModel);

        using var res = await httpClient.PostAsync(reqUrl, content);

        res.EnsureSuccessStatusCode();

        return (await res.Content.ReadFromJsonAsync(CurseForgeModelContext.Default
            .DataModelCurseForgeAddonInfoArray))?.Data;
    }

    public async Task<DataModelWithPagination<CurseForgeLatestFileModel[]>?> GetAddonFiles(long addonId, int index = 0, int pageSize = 50)
    {
        var reqUrl = $"{GetApiRoot()}/mods/{addonId}/files?index={index}&pageSize={pageSize}";

        using var res = await httpClient.GetAsync(reqUrl);

        res.EnsureSuccessStatusCode();

        return await res.Content.ReadFromJsonAsync(CurseForgeModelContext.Default
            .DataModelWithPaginationCurseForgeLatestFileModelArray);
    }

    public async Task<CurseForgeLatestFileModel[]?> GetFiles(IEnumerable<long> fileIds)
    {
        var reqUrl = $"{GetApiRoot()}/mods/files";

        var content = JsonContent.Create(
            new FileInfoReqModel(fileIds),
            CurseForgeModelContext.Default.FileInfoReqModel);

        using var res = await httpClient.PostAsync(reqUrl, content);

        res.EnsureSuccessStatusCode();

        return (await res.Content.ReadFromJsonAsync(CurseForgeModelContext.Default
            .DataModelCurseForgeLatestFileModelArray))?.Data;
    }

    public async Task<CurseForgeSearchCategoryModel[]?> GetCategories(int gameId = 432)
    {
        var reqUrl = $"{GetApiRoot()}/categories?gameId={gameId}";

        using var res = await httpClient.GetAsync(reqUrl);

        return (await res.Content.ReadFromJsonAsync(CurseForgeModelContext.Default
            .DataModelCurseForgeSearchCategoryModelArray))?.Data;
    }

    public async Task<CurseForgeFeaturedAddonModel?> GetFeaturedAddons(FeaturedQueryOptions options)
    {
        const string reqUrl = "https://api.curseforge.com/v1/mods/featured";

        var content = JsonContent.Create(options, FeaturedQueryOptionsContext.Default.FeaturedQueryOptions);

        using var res = await httpClient.PostAsync(reqUrl, content);
        res.EnsureSuccessStatusCode();

        return (await res.Content.ReadFromJsonAsync(CurseForgeModelContext.Default
            .DataModelCurseForgeFeaturedAddonModel))?.Data;
    }

    public async Task<string?> GetAddonDownloadUrl(long addonId, long fileId)
    {
        var reqUrl = $"{GetApiRoot()}/mods/{addonId}/files/{fileId}/download-url";

        using var res = await httpClient.GetAsync(reqUrl);

        if (!res.IsSuccessStatusCode)
            throw new CurseForgeModResolveException(addonId, fileId);

        return (await res.Content.ReadFromJsonAsync(CurseForgeModelContext.Default.DataModelString))?.Data;
    }

    public async Task<string?> GetAddonDescriptionHtml(long addonId)
    {
        var reqUrl = $"{GetApiRoot()}/mods/{addonId}/description";

        using var res = await httpClient.GetAsync(reqUrl);
        res.EnsureSuccessStatusCode();

        return (await res.Content.ReadFromJsonAsync(CurseForgeModelContext.Default.DataModelString))?.Data;
    }

    public async Task<CurseForgeFuzzySearchResponseModel?> TryFuzzySearchFile(
        long[] fingerprint,
        int gameId = 432)
    {
        var reqUrl = $"{GetApiRoot()}/fingerprints/{gameId}";

        var content = JsonContent.Create(
            new FuzzyFingerPrintReqModel(fingerprint),
            CurseForgeModelContext.Default.FuzzyFingerPrintReqModel);

        using var res = await httpClient.PostAsync(reqUrl, content);
        res.EnsureSuccessStatusCode();

        return (await res.Content.ReadFromJsonAsync(CurseForgeModelContext.Default
            .DataModelCurseForgeFuzzySearchResponseModel))?.Data;
    }
}