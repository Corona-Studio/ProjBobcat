using System.Collections.Immutable;

namespace ProjBobcat.Class.Model.GameResource.ResolvedInfo;

public record GameModResolvedInfo(
    string? Author,
    string FilePath,
    IImmutableList<string>? ModList,
    string? Title,
    string? Version,
    string? ModType,
    bool IsEnabled)
{
    public ModLoaderType LoaderType { get; init; }
}