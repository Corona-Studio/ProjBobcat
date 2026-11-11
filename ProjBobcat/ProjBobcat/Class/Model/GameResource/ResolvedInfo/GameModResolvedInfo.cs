using System.Collections.Immutable;
using System.Collections.Generic;

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
    public IReadOnlyList<string> ModIds { get; init; } = [];
    public ModLoaderType LoaderType { get; init; }
    public byte[]? IconBytes { get; init; }
}
