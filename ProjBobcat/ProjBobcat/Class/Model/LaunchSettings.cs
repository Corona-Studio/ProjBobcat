using System.Collections.Generic;
using System.Text;
using ProjBobcat.Class.Model.LauncherProfile;
using ProjBobcat.Class.Model.YggdrasilAuth;
using ProjBobcat.Interface;

namespace ProjBobcat.Class.Model;

public class GameArguments
{
    /// <summary>
    ///     Java executable file
    /// </summary>
    public required string JavaExecutable { get; set; }

    public uint MinMemory { get; set; }
    public required uint MaxMemory { get; init; }
    public ResolutionModel? Resolution { get; set; }
    public required GcType GcType { get; init; }
    public IReadOnlyList<string>? AdditionalJvmArguments { get; set; }
    public IReadOnlyList<string>? AdditionalArguments { get; set; }
    public ServerSettings? ServerSettings { get; set; }

    /// <summary>
    /// Join world name (Starting from 1.20, single player only)
    /// </summary>
    public string? JoinWorldName { get; set; }

    public string? AdvanceArguments { get; set; }
}

public class LaunchSettings
{
    public required string GameName { get; init; }

    /// <summary>
    ///     Real game root, should be the root of /saves, /logs
    /// </summary>
    public required string GamePath { get; init; }

    /// <summary>
    ///     The game resource path, should be the root of  /libraries
    /// </summary>
    public required string GameResourcePath { get; init; }

    /// <summary>
    ///     Real version id, like 1.14, 1.14-forge-xxxx
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    ///     游戏窗口标题
    /// </summary>
    public string? WindowTitle { get; init; }

    public required IVersionLocator VersionLocator { get; init; }

    public required IAuthenticator Authenticator { get; init; }
    public ProfileInfoModel? SelectedProfile { get; init; }

    public bool VersionInsulation { get; init; }
    public string? LauncherName { get; init; }

    public GameArguments? FallBackGameArguments { get; init; }
    public required GameArguments GameArguments { get; init; }

    public override string ToString()
    {
        var sb = new StringBuilder();

        sb
            .AppendLine()
            .Append($"Game Name: {this.GameName}").AppendLine()
            .Append($"Game Resource Path: {this.GameResourcePath}").AppendLine()
            .Append($"Version: {this.Version}").AppendLine()
            .Append($"Authenticator: {this.Authenticator?.GetType().Name ?? "-"}").AppendLine()
            .Append($"Version Insulation: {this.VersionInsulation}").AppendLine()
            .AppendLine();

        return sb.ToString();
    }
}
