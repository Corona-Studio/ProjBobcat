using System;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.YggdrasilAuth;

public class ErrorModel
{
    [JsonPropertyName("error")] public string Error { get; set; }

    [JsonPropertyName("errorMessage")] public string ErrorMessage { get; set; }

    [JsonPropertyName("cause")] public string Cause { get; set; }

    [JsonIgnore] public Exception Exception { get; set; }
}

[JsonSerializable(typeof(ErrorModel))]
partial class ErrorModelContext : JsonSerializerContext
{
}