using ProjBobcat.Class.Model.LauncherProfile;
using ProjBobcat.Interface;

namespace ProjBobcat.Class.Model
{
    public class GameArguments
    {
        /// <summary>
        ///     Java executable file
        /// </summary>
        public string JavaExecutable { get; set; }

        public int MinMemory { get; set; }
        public int MaxMemory { get; set; }
        public ResolutionModel Resolution { get; set; }
        public GcType GcType { get; set; }
        public string AgentPath { get; set; }
        public string JavaAgentAdditionPara { get; set; }
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

        public IVersionLocator VersionLocator { get; set; }
        public IAuthenticator Authenticator { get; set; }
        public bool VersionInsulation { get; set; }
        public string LauncherName { get; set; }
        public GameArguments FallBackGameArguments { get; set; }
        public GameArguments GameArguments { get; set; }
    }
}