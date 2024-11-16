using System;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.Microsoft.Graph;

public class GraphResponseErrorModel
{
    [JsonPropertyName("error")] public required string ErrorType { get; init; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; set; }

    [JsonPropertyName("error_codes")] public int[]? ErrorCodes { get; set; }

    [JsonPropertyName("timestamp")] public DateTime TimeStamp { get; set; }

    [JsonPropertyName("trace_id")] public Guid TraceId { get; set; }

    [JsonPropertyName("correlation_id")] public Guid CorrelationId { get; set; }

    [JsonPropertyName("error_uri")] public string? ErrorUri { get; set; }
}

[JsonSerializable(typeof(GraphResponseErrorModel))]
partial class GraphResponseErrorModelContext : JsonSerializerContext;