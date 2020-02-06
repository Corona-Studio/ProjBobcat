using ProjBobcat.Class.Model;
using System.Collections.Generic;

namespace ProjBobcat.Interface
{
    public interface IVersionLocator
    {
        VersionInfo GetGame(string id);
        IEnumerable<VersionInfo> GetAllGames();
        ILauncherProfileParser LauncherProfileParser { get; set; }
        string ParseJvmArguments(List<object> arguments);
    }
}