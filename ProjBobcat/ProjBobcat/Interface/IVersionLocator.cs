using System.Collections.Generic;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Interface
{
    public interface IVersionLocator
    {
        ILauncherProfileParser LauncherProfileParser { get; set; }
        ILauncherAccountParser LauncherAccountParser { get; set; }

        /// <summary>
        ///     获取某个特定ID的游戏信息。
        ///     Get the game info with specific ID.
        /// </summary>
        /// <param name="id">装有游戏版本的文件夹名。The game version folder's name.</param>
        /// <returns></returns>
        VersionInfo GetGame(string id);

        /// <summary>
        ///     获取所有能够正常被解析的游戏信息。
        ///     Fetch all the game versions' information in the .minecraft/ folder.
        /// </summary>
        /// <returns>一个表，包含.minecraft文件夹中所有版本的所有信息。A list, containing all information of all games in .minecraft/ .</returns>
        IEnumerable<VersionInfo> GetAllGames();

        /// <summary>
        ///     解析游戏JVM参数
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        string ParseJvmArguments(List<object> arguments);
    }
}