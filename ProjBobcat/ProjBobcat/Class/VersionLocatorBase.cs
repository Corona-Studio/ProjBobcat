using System.Collections.Generic;
using System.Text.Json;
using ProjBobcat.Class.Model;
using ProjBobcat.Interface;

namespace ProjBobcat.Class;

public abstract class VersionLocatorBase : IVersionLocator
{
    public ILauncherProfileParser? LauncherProfileParser { get; init; }
    public ILauncherAccountParser? LauncherAccountParser { get; init; }

    public abstract IVersionInfo GetGame(string id);

    public abstract ResolvedGameVersion? ResolveGame(
        IVersionInfo rawVersionInfo,
        NativeReplacementPolicy nativeReplacementPolicy,
        JavaRuntimeInfo? javaRuntimeInfo);

    public abstract IEnumerable<IVersionInfo> GetAllGames();

    public abstract IEnumerable<string> ParseJvmArguments(JsonElement[] arguments);

    public abstract (List<NativeFileInfo>, List<FileInfo>) GetNatives(Library[] libraries);

    public abstract (GameBrokenReason?, RawVersionModel?) ParseRawVersion(string id);
}