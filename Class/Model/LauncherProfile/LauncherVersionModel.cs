using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.LauncherProfile
{
    public class LauncherVersionModel
    {
        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("format")] public int Format { get; set; }
    }
}