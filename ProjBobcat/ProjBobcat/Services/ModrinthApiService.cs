using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ProjBobcat.Class.Model.Downloading;
using ProjBobcat.Class.Model.Modrinth;
using ProjBobcat.Interface;
using ProjBobcat.Interface.Services;

namespace ProjBobcat.Services;

#region Temp Models

record FileMatchRequestModel(
    [property: JsonPropertyName("hashes")] string[] Hashes,
    [property: JsonPropertyName("algorithm")]
    string Algorithm,
    [property: JsonPropertyName("loaders")]
    string[] Loaders,
    [property: JsonPropertyName("game_versions")]
    string[] GameVersions);

[JsonSerializable(typeof(FileMatchRequestModel))]
partial class ModrinthModelContext : JsonSerializerContext;

#endregion

public class ModrinthApiService(
    HttpClient httpClient,
    ILauncherCoreSettingsProvider settingsProvider) : IModrinthApiService
{
    private const string DefaultApiUrl = "https://api.modrinth.com/v2";

    public async Task<ModrinthCategoryInfo[]?> GetCategories()
    {
        var reqUrl = $"{this.GetApiRoot()}/tag/category";

        using var res = await this.Get(reqUrl);
        var resModel =
            await res.Content.ReadFromJsonAsync(ModrinthCategoryInfoContext.Default.ModrinthCategoryInfoArray);

        return resModel;
    }

    public async Task<ModrinthProjectDependencyInfo?> GetProjectDependenciesInfo(string projectId)
    {
        var reqUrl = $"{this.GetApiRoot()}/project/{projectId}/dependencies";

        using var res = await this.Get(reqUrl);
        var resModel =
            await res.Content.ReadFromJsonAsync(ModrinthProjectDependencyInfoContext.Default
                .ModrinthProjectDependencyInfo);

        return resModel;
    }

    public async Task<ModrinthProjectInfo?> GetProject(string projectId, CancellationToken ct)
    {
        var reqUrl = $"{this.GetApiRoot()}/project/{projectId}";

        using var res = await this.Get(reqUrl, ct);
        var resModel = await res.Content.ReadFromJsonAsync(ModrinthProjectInfoContext.Default.ModrinthProjectInfo, ct);

        return resModel;
    }

    public async Task<ModrinthSearchResult?> GetFeaturedMods()
    {
        var reqUrl = $"{this.GetApiRoot()}/search";

        using var res = await this.Get(reqUrl);
        var resModel = await res.Content.ReadFromJsonAsync(ModrinthSearchResultContext.Default.ModrinthSearchResult);

        return resModel;
    }

    public async Task<ModrinthSearchResult?> SearchMod(ModrinthSearchOptions searchOptions, CancellationToken ct)
    {
        var reqUrl = $"{this.GetApiRoot()}/search{searchOptions}";

        using var res = await this.Get(reqUrl, ct);

        if (!res.IsSuccessStatusCode)
            return null;

        return await res.Content.ReadFromJsonAsync(ModrinthSearchResultContext.Default.ModrinthSearchResult, ct);
    }

    public async Task<ModrinthVersionInfo[]?> GetProjectVersions(string projectId)
    {
        var reqUrl = $"{this.GetApiRoot()}/project/{projectId}/version";

        using var res = await this.Get(reqUrl);
        var resModel = await res.Content.ReadFromJsonAsync(ModrinthVersionInfoContext.Default.ModrinthVersionInfoArray);

        return resModel;
    }

    public async Task<ModrinthVersionInfo?> TryMatchVersionFileByHash(
        string hash,
        HashType hashType)
    {
        var para = hashType switch
        {
            HashType.SHA1 => "?algorithm=sha1",
            HashType.SHA512 => "?algorithm=sha512",
            _ => throw new ArgumentOutOfRangeException(nameof(hashType), hashType, null)
        };
        var reqUrl = $"{this.GetApiRoot()}/version_file/{hash}{para}";

        using var res = await this.Get(reqUrl);
        var resModel = await res.Content.ReadFromJsonAsync(ModrinthVersionInfoContext.Default.ModrinthVersionInfo);

        return resModel;
    }

    public async Task<ModrinthVersionInfo?> GetVersionInfo(string projectId, string versionId)
    {
        var reqUrl = $"{this.GetApiRoot()}/project/{projectId}/version/{versionId}";

        using var res = await this.Get(reqUrl);
        var resModel = await res.Content.ReadFromJsonAsync(ModrinthVersionInfoContext.Default.ModrinthVersionInfo);

        return resModel;
    }

    public async Task<ModrinthVersionInfo?> GetVersionInfo(string versionId)
    {
        var reqUrl = $"{this.GetApiRoot()}/version/{versionId}";

        using var res = await this.Get(reqUrl);
        var resModel = await res.Content.ReadFromJsonAsync(ModrinthVersionInfoContext.Default.ModrinthVersionInfo);

        return resModel;
    }

    public async Task<IReadOnlyDictionary<string, ModrinthVersionInfo>?> TryMatchFile(
        string[] hashes,
        string algorithm,
        string[] loaders,
        string[] gameVersions)
    {
        var reqUrl = $"{this.GetApiRoot()}/version_files/update";

        var body = new FileMatchRequestModel(hashes, algorithm, loaders, gameVersions);

        var content = JsonContent.Create(body, ModrinthModelContext.Default.FileMatchRequestModel);

        using var res = await httpClient.PostAsync(reqUrl, content);

        if (!res.IsSuccessStatusCode) return null;

        return await res.Content.ReadFromJsonAsync(
            ModrinthVersionInfoContext.Default.IReadOnlyDictionaryStringModrinthVersionInfo);
    }

    private string GetApiRoot()
    {
        var customRoot = settingsProvider.ModrinthApiBaseUrl();

        if (!string.IsNullOrEmpty(customRoot))
            return customRoot;

        return DefaultApiUrl;
    }

    private async Task<HttpResponseMessage> Get(string reqUrl, CancellationToken ct = default)
    {
        using var res = new HttpRequestMessage(HttpMethod.Get, reqUrl);
        var req = await httpClient.SendAsync(res, ct);

        req.EnsureSuccessStatusCode();

        return req;
    }
}