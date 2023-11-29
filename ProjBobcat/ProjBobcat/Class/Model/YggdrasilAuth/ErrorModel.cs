using System;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.YggdrasilAuth;

public class ErrorModel
{
    [JsonPropertyName("error")] public string? Error { get; init; }

    [JsonPropertyName("errorMessage")] public string? ErrorMessage { get; init; }

    [JsonPropertyName("cause")] public string? Cause { get; init; }

    [JsonIgnore] public Exception? Exception { get; init; }
}

[JsonSerializable(typeof(ErrorModel))]
partial class ErrorModelContext : JsonSerializerContext
{
}