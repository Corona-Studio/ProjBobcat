using System;
using System.Text;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.YggdrasilAuth;

public class ErrorModel
{
    [JsonPropertyName("error")] public string? Error { get; init; }

    [JsonPropertyName("errorMessage")] public string? ErrorMessage { get; init; }

    [JsonPropertyName("cause")] public string? Cause { get; init; }

    [JsonIgnore] public Exception? Exception { get; init; }

    public override string ToString()
    {
        var sb = new StringBuilder().AppendLine();

        if (!string.IsNullOrEmpty(Error))
            sb.AppendLine("[ERROR]").AppendLine(Error);
        if (!string.IsNullOrEmpty(ErrorMessage))
            sb.AppendLine("[ERROR MESSAGE]").AppendLine(ErrorMessage);
        if (!string.IsNullOrEmpty(Cause))
            sb.AppendLine("[CAUSE]").AppendLine(Cause);
        if (Exception != null)
            sb.AppendLine("[EXCEPTION]").AppendLine(Exception.ToString());

        return sb.AppendLine().ToString();
    }
}

[JsonSerializable(typeof(ErrorModel))]
partial class ErrorModelContext : JsonSerializerContext;