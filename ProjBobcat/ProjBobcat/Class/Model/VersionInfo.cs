using ProjBobcat.Interface;
using System.Collections.Generic;

namespace ProjBobcat.Class.Model;

public class BrokenVersionInfo(string id) : IVersionInfo
{
    public string Name { get; init; } = id;

    public string DirName { get; init; } = id;

    public required GameBrokenReason BrokenReason { get; init; }
}

public class VersionInfo : IVersionInfo
{
    /// <summary>
    ///     为启动器引用准备的带有tag的名称。
    ///     A name with a tag provided for the launcher's reference.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///     该版本的真实id，例如1.14-forge-xxx
    ///     The real id of this version, like 1.14-forge-xxx
    /// </summary>
    public required string Id { get; init; }

    public required string DirName { get; init; }

    public required string? InheritsFrom { get; set; }

    public required string GameBaseVersion { get; init; }

    public JavaVersionModel? JavaVersion { get; set; }

    public string? Assets { get; init; }

    public RawVersionModel? RawVersion { get; init; }

    public IReadOnlyList<RawVersionModel>? InheritsVersions { get; init; }

    /// <summary>
    ///     在递归式继承中最古老的版本（递归终点）。
    ///     The oldest version inherited (recursive).
    /// </summary>
    public string? RootVersion { get; set; }
}

public record ResolvedGameVersion(
    string? RootVersion,
    string DirName,
    string MainClass,
    string? Assets,
    Asset? AssetInfo,
    Logging? Logging,
    IReadOnlyList<FileInfo> Libraries,
    IReadOnlyList<NativeFileInfo> Natives,
    IReadOnlyList<string>? JvmArguments,
    IReadOnlyList<string>? GameArguments,
    IReadOnlyDictionary<string, string>? AvailableGameArguments);