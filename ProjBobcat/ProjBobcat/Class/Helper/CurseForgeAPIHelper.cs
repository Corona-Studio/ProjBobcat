using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ProjBobcat.Class.Model.CurseForge;
using ProjBobcat.Class.Model.CurseForge.API;

namespace ProjBobcat.Class.Helper
{
    public static class CurseForgeAPIHelper
    {
        private const string BaseUrl = "https://addons-ecs.forgesvc.net";

        private static async Task<string> Get(string reqUrl)
        {
            using var req = await HttpHelper.Get(reqUrl);
            req.EnsureSuccessStatusCode();

            var resContent = await req.Content.ReadAsStringAsync();

            return resContent;
        }

        public static async Task<List<CurseForgeAddonInfo>> SearchAddons(SearchOptions options)
        {
            var reqUrl = $"{BaseUrl}/api/v2/addon/search{options}";

            var resContent = await Get(reqUrl);
            var resModel = JsonConvert.DeserializeObject<List<CurseForgeAddonInfo>>(resContent);

            return resModel;
        }

        public static async Task<string> GetAddonDescription(int addonId)
        {
            var reqUrl = $"{BaseUrl}/api/v2/addon/${addonId}/description";

            var resContent = await Get(reqUrl);

            return resContent;
        }

        public static async Task<List<CurseForgeLatestFileModel>> GetAddonFiles(int addonId)
        {
            var reqUrl = $"{BaseUrl}/api/v2/addon/{addonId}/files";

            var resContent = await Get(reqUrl);
            var resModel = JsonConvert.DeserializeObject<List<CurseForgeLatestFileModel>>(resContent);

            return resModel;
        }

        public static async Task<List<CurseForgeSearchCategoryModel>> GetCategories(int gameId = 432)
        {
            var reqUrl = $"{BaseUrl}/api/v2/category?gameId={gameId}";

            var resContent = await Get(reqUrl);
            var resModel = JsonConvert.DeserializeObject<List<CurseForgeSearchCategoryModel>>(resContent);

            return resModel;
        }

        public static async Task<CurseForgeFeaturedAddonModel> GetFeaturedAddons(FeaturedQueryOptions options)
        {
            var reqUrl = $"{BaseUrl}/api/v2/addon/featured";
            var reqJson = JsonConvert.SerializeObject(options);

            using var req = await HttpHelper.Post(reqUrl, reqJson);
            req.EnsureSuccessStatusCode();

            var resContent = await req.Content.ReadAsStringAsync();
            var resModel = JsonConvert.DeserializeObject<CurseForgeFeaturedAddonModel>(resContent);

            return resModel;
        }

        public static async Task<string> GetAddonDownloadUrl(long addonId, long fileId)
        {
            var reqUrl = $"{BaseUrl}/api/v2/addon/{addonId}/file/{fileId}/download-url";

            var resContent = await Get(reqUrl);

            return resContent;
        }
    }
}