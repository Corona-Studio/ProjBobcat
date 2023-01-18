using Newtonsoft.Json;
using System.Collections.Generic;

namespace ProjBobcat.Class.Model.Fabric;

public class FabricFileInfo
{
    [JsonProperty("file")] public string File { get; set; }
}

public class ModUpdater
{
    [JsonProperty("strategy")] public string Strategy { get; set; }

    [JsonProperty("url")] public string Url { get; set; }
}

public class Custom
{
    [JsonProperty("modUpdater")] public ModUpdater ModUpdater { get; set; }
}

public class FabricModInfoModel
{
    [JsonProperty("schemaVersion")] public int SchemaVersion { get; set; }

    [JsonProperty("id")] public string Id { get; set; }

    [JsonProperty("version")] public string Version { get; set; }

    [JsonProperty("environment")] public string Environment { get; set; }

    [JsonProperty("entrypoints")] public Dictionary<string, List<string>> Entrypoints { get; set; }

    [JsonProperty("custom")] public Custom Custom { get; set; }

    [JsonProperty("depends")] public Dictionary<string, string> Depends { get; set; }

    [JsonProperty("recommends")] public Dictionary<string, string> Recommends { get; set; }

    [JsonProperty("name")] public string Name { get; set; }

    [JsonProperty("description")] public string Description { get; set; }

    [JsonProperty("icon")] public string Icon { get; set; }

    [JsonProperty("authors")] public List<string> Authors { get; set; }

    [JsonProperty("contacts")] public Dictionary<string, string> Contacts { get; set; }

    [JsonProperty("jars")] public List<FabricFileInfo> Jars { get; set; }
}