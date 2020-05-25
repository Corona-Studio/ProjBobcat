using System.Collections.Generic;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Interface
{
    public interface IVersionLocator
    {
        ILauncherProfileParser LauncherProfileParser { get; set; }
        VersionInfo GetGame(string id);
        IEnumerable<VersionInfo> GetAllGames();
        string ParseJvmArguments(List<object> arguments);
    }
}