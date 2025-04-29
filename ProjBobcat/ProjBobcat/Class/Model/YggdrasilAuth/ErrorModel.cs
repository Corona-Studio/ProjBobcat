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

        if (!string.IsNullOrEmpty(this.Error))
            sb.AppendLine("[ERROR]").AppendLine(this.Error);
        if (!string.IsNullOrEmpty(this.ErrorMessage))
            sb.AppendLine("[ERROR MESSAGE]").AppendLine(this.ErrorMessage);
        if (!string.IsNullOrEmpty(this.Cause))
            sb.AppendLine("[CAUSE]").AppendLine(this.Cause);
        if (this.Exception != null)
            sb.AppendLine("[EXCEPTION]").AppendLine(this.Exception.ToString());

        return sb.AppendLine().ToString();
    }
}

[JsonSerializable(typeof(ErrorModel))]
partial class ErrorModelContext : JsonSerializerContext;