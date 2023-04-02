using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.Microsoft.Graph;

public class DeviceIdResponseModel
{
    [JsonPropertyName("user_code")] public string UserCode { get; set; }

    [JsonPropertyName("device_code")] public string DeviceCode { get; set; }

    [JsonPropertyName("verification_uri")] public string VerificationUri { get; set; }

    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }

    [JsonPropertyName("interval")] public int Interval { get; set; }

    [JsonPropertyName("message")] public string Message { get; set; }
}

[JsonSerializable(typeof(DeviceIdResponseModel))]
partial class DeviceIdResponseModelContext : JsonSerializerContext
{
}