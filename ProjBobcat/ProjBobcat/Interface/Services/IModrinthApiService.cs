using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProjBobcat.Class.Model.Downloading;
using ProjBobcat.Class.Model.Modrinth;

namespace ProjBobcat.Interface.Services;

public interface IModrinthApiService
{
    Task<ModrinthCategoryInfo[]?> GetCategories();

    Task<ModrinthProjectDependencyInfo?> GetProjectDependenciesInfo(string projectId);

    Task<ModrinthProjectInfo?> GetProject(string projectId, CancellationToken ct);

    Task<ModrinthSearchResult?> GetFeaturedMods();

    Task<ModrinthSearchResult?> SearchMod(ModrinthSearchOptions searchOptions, CancellationToken ct);

    Task<ModrinthVersionInfo[]?> GetProjectVersions(string projectId);

    Task<ModrinthVersionInfo?> TryMatchVersionFileByHash(string hash, HashType hashType);

    Task<ModrinthVersionInfo?> GetVersionInfo(string projectId, string versionId);

    Task<ModrinthVersionInfo?> GetVersionInfo(string versionId);

    Task<IReadOnlyDictionary<string, ModrinthVersionInfo>?> TryMatchFile(
        string[] hashes,
        string algorithm,
        string[] loaders,
        string[] gameVersions);
}