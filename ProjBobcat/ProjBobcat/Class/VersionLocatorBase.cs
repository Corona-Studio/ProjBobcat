using System.Collections.Generic;
using System.Text.Json;
using ProjBobcat.Class.Model;
using ProjBobcat.Interface;

namespace ProjBobcat.Class;

public abstract class VersionLocatorBase(string rootPath) : LauncherParserBase(rootPath), IVersionLocator
{
    public ILauncherProfileParser? LauncherProfileParser { get; init; }
    public ILauncherAccountParser? LauncherAccountParser { get; init; }

    public abstract VersionInfo? GetGame(string id);

    public abstract IEnumerable<VersionInfo> GetAllGames();

    public abstract IEnumerable<string> ParseJvmArguments(JsonElement[] arguments);
    private protected abstract VersionInfo? ToVersion(string id);

    public abstract (List<NativeFileInfo>, List<FileInfo>) GetNatives(Library[] libraries);

    private protected abstract (IEnumerable<string>, Dictionary<string, string>) ParseGameArguments(
        (string, JsonElement[]) arguments);

    public abstract RawVersionModel? ParseRawVersion(string id);
}