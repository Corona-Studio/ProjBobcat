﻿using System.Threading.Tasks;
using System.Threading;
using ProjBobcat.Class.Model.CurseForge;
using ProjBobcat.Class.Model.CurseForge.API;
using System.Collections.Generic;

namespace ProjBobcat.Interface.Services;

public interface ICurseForgeApiService
{
    Task<DataModelWithPagination<CurseForgeAddonInfo[]>?> SearchAddons(SearchOptions options, CancellationToken ct);

    Task<CurseForgeAddonInfo?> GetAddon(long addonId);

    Task<CurseForgeAddonInfo[]?> GetAddons(IEnumerable<long> addonIds);

    Task<DataModelWithPagination<CurseForgeLatestFileModel[]>?> GetAddonFiles(
        long addonId,
        int index = 0,
        int pageSize = 50);

    Task<CurseForgeLatestFileModel[]?> GetFiles(IEnumerable<long> fileIds);

    Task<CurseForgeSearchCategoryModel[]?> GetCategories(int gameId = 432);

    Task<CurseForgeFeaturedAddonModel?> GetFeaturedAddons(FeaturedQueryOptions options);

    Task<string?> GetAddonDownloadUrl(long addonId, long fileId);

    Task<string?> GetAddonDescriptionHtml(long addonId);

    Task<CurseForgeFuzzySearchResponseModel?> TryFuzzySearchFile(
        long[] fingerprint,
        int gameId = 432);
}