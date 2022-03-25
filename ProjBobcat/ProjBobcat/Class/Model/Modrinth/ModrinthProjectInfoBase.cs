using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.Modrinth;

public class ModrinthProjectInfoBase
{
    [JsonProperty("project_type")]
    public string ProjectType { get; set; }

    [JsonProperty("slug")]
    public string Slug { get; set; }

    [JsonProperty("title")]
    public string Title { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; }

    [JsonProperty("categories")]
    public List<string> Categories { get; set; }

    [JsonProperty("versions")]
    public List<string> Versions { get; set; }

    [JsonProperty("downloads")]
    public int Downloads { get; set; }

    [JsonProperty("icon_url")]
    public string IconUrl { get; set; }

    [JsonProperty("client_side")]
    public string ClientSide { get; set; }

    [JsonProperty("server_side")]
    public string ServerSide { get; set; }

    [JsonProperty("gallery")]
    public List<object> Gallery { get; set; }
}