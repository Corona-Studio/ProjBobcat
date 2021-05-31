using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.CurseForge.API
{
    public class FeaturedQueryOptions
    {
        public int GameId { get; set; }
        [JsonProperty("addonIds")]
        public List<int> AddonIds { get; set; }
        [JsonProperty("featuredCount")]
        public int FeaturedCount { get; set; }
        [JsonProperty("popularCount")]
        public int PopularCount { get; set; }
        [JsonProperty("updatedCount")]
        public int UpdatedCount { get; set; }

        public static FeaturedQueryOptions Default => new ()
        {
            AddonIds = Enumerable.Empty<int>().ToList(),
            FeaturedCount = 15,
            GameId = 432,
            PopularCount = 15,
            UpdatedCount = 15
        };
    }
}