using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.MicrosoftAuth;

public class OwnershipItem
{
    [JsonPropertyName("name")] public string Name { get; set; }

    [JsonPropertyName("signature")] public string Signature { get; set; }
}

public class MojangOwnershipResponseModel
{
    [JsonPropertyName("items")] public OwnershipItem[] Items { get; set; }

    [JsonPropertyName("signature")] public string Signature { get; set; }

    [JsonPropertyName("keyId")] public string KeyId { get; set; }
}

[JsonSerializable(typeof(MojangOwnershipResponseModel))]
partial class MojangOwnershipResponseModelContext : JsonSerializerContext
{
}