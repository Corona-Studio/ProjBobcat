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
    public string JavaExecutable { get; set; }

    public uint MinMemory { get; set; }
    public uint MaxMemory { get; set; }
    public ResolutionModel Resolution { get; set; }
    public GcType GcType { get; set; }
    public IEnumerable<string> AddtionalJvmArguments { get; set; }
    public ServerSettings ServerSettings { get; set; }
    public string AdvanceArguments { get; set; }
}

public class LaunchSettings
{
    public string GameName { get; set; }

    /// <summary>
    ///     Real game root, should be the root of /saves, /logs
    /// </summary>
    public string GamePath { get; set; }

    /// <summary>
    ///     The game resource path, should be the root of  /libraries
    /// </summary>
    public string GameResourcePath { get; set; }

    /// <summary>
    ///     Real version id, like 1.14, 1.14-forge-xxxx
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    ///     游戏窗口标题
    /// </summary>
    public string WindowTitle { get; set; }

    public IVersionLocator VersionLocator { get; set; }

    public IAuthenticator Authenticator { get; set; }
    public ProfileInfoModel? SelectedProfile { get; set; }

    public bool VersionInsulation { get; set; }
    public string LauncherName { get; set; }

    public override string ToString()
    {
        var sb = new StringBuilder();

        sb
            .AppendLine()
            .AppendFormat("Game Name: {0}", GameName).AppendLine()
            .AppendFormat("Game Resource Path: {0}", GameResourcePath).AppendLine()
            .AppendFormat("Version: {0}", Version).AppendLine()
            .AppendFormat("Authenticator: {0}", Authenticator?.GetType().Name ?? "-").AppendLine()
            .AppendFormat("Version Insulation: {0}", VersionInsulation).AppendLine()
            .AppendLine();

        return sb.ToString();
    }
#nullable enable
    public GameArguments? FallBackGameArguments { get; set; }
    public GameArguments? GameArguments { get; set; }
#nullable restore
}