using System.Collections.Generic;
using System.Text.Json;
using ProjBobcat.Class.Model;
using ProjBobcat.Interface;

namespace ProjBobcat.Class;

public abstract class VersionLocatorBase : LauncherParserBase, IVersionLocator
{
    public ILauncherProfileParser LauncherProfileParser { get; set; }
    public ILauncherAccountParser LauncherAccountParser { get; set; }

    public abstract VersionInfo? GetGame(string id);

    public abstract IEnumerable<VersionInfo> GetAllGames();

    public abstract IEnumerable<string> ParseJvmArguments(IEnumerable<JsonElement> arguments);
    private protected abstract VersionInfo? ToVersion(string id);

    public abstract (List<NativeFileInfo>, List<FileInfo>) GetNatives(IEnumerable<Library> libraries);

    private protected abstract (IEnumerable<string>, Dictionary<string, string>) ParseGameArguments(
        (string, IEnumerable<JsonElement>) arguments);

    public abstract RawVersionModel? ParseRawVersion(string id);
}