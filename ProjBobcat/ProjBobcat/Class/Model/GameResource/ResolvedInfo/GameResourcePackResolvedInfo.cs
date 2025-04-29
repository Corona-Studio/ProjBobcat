using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.GameResource.ResolvedInfo;

public interface IResourcePackDescription
{
    string Text { get; }
}

public record PlainTextResourcePackDescription(string Text) : IResourcePackDescription;

public record ObjectResourcePackDescription(
    [property: JsonPropertyName("bold")] bool Bold,
    [property: JsonPropertyName("italic")] bool Italic,
    [property: JsonPropertyName("underlined")]
    bool Underlined,
    [property: JsonPropertyName("strikethrough")]
    bool Strikethrough,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("color")] string? Color) : IResourcePackDescription;

public record GameResourcePackResolvedInfo(
    string FileName,
    IResourcePackDescription[]? Descriptions,
    int Version,
    byte[]? IconBytes);

[JsonSerializable(typeof(ObjectResourcePackDescription[]))]
partial class GameResourcePackDescriptionModelContext : JsonSerializerContext;