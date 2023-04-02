using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.Microsoft.Graph;

public class GraphAuthResultModel
{
    [JsonPropertyName("token_type")] public string TokenType { get; set; }

    [JsonPropertyName("scope")] public string Scope { get; set; }

    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }

    [JsonPropertyName("access_token")] public string AccessToken { get; set; }

    [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; }

    [JsonPropertyName("id_token")] public string IdToken { get; set; }
}

[JsonSerializable(typeof(GraphAuthResultModel))]
public partial class GraphAuthResultModelContext : JsonSerializerContext
{
}