using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.MicrosoftAuth;

public class MojangErrorResponseModel
{
    [JsonPropertyName("path")] public string Path { get; set; }

    [JsonPropertyName("errorType")] public string ErrorType { get; set; }

    [JsonPropertyName("error")] public string Error { get; set; }

    [JsonPropertyName("errorMessage")] public string ErrorMessage { get; set; }

    [JsonPropertyName("developerMessage")] public string DeveloperMessage { get; set; }
}

[JsonSerializable(typeof(MojangErrorResponseModel))]
partial class MojangErrorResponseModelContext : JsonSerializerContext
{
}