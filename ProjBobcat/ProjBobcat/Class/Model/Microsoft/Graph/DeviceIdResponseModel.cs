using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.Microsoft.Graph;

public class DeviceIdResponseModel
{
    [JsonProperty("user_code")] public string UserCode { get; set; }

    [JsonProperty("device_code")] public string DeviceCode { get; set; }

    [JsonProperty("verification_uri")] public string VerificationUri { get; set; }

    [JsonProperty("expires_in")] public int ExpiresIn { get; set; }

    [JsonProperty("interval")] public int Interval { get; set; }

    [JsonProperty("message")] public string Message { get; set; }
}