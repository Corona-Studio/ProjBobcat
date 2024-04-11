using System.Collections.Generic;

namespace ProjBobcat.Class.Model;

public class VersionInfo
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
    public required string Id { get; set; }

    public required string DirName { get; init; }

    public required string? InheritsFrom { get; set; }

    public required string GameBaseVersion { get; init; }

    public JavaVersionModel? JavaVersion { get; set; }

    public required string MainClass { get; set; }
    public string? Assets { get; init; }
    public Asset? AssetInfo { get; set; }
    public required List<FileInfo> Libraries { get; set; }
    public required List<NativeFileInfo> Natives { get; set; }
    public Logging? Logging { get; init; }
    public IReadOnlyList<string>? JvmArguments { get; set; }
    public required IEnumerable<string> GameArguments { get; set; }
    public IReadOnlyDictionary<string, string>? AvailableGameArguments { get; set; }

    /// <summary>
    ///     在递归式继承中最古老的版本（递归终点）。
    ///     The oldest version inherited (recursive).
    /// </summary>
    public string? RootVersion { get; set; }
}