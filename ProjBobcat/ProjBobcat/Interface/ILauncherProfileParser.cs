using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.LauncherProfile;

namespace ProjBobcat.Interface
{
    /// <summary>
    ///     官方launcher_profile.json适配器接口
    /// </summary>
    public interface ILauncherProfileParser
    {
        /// <summary>
        ///     启动器信息字段
        /// </summary>
        LauncherProfileModel LauncherProfile { get; set; }

        /// <summary>
        ///     添加新的验证信息
        /// </summary>
        /// <param name="authInfo">验证信息</param>
        /// <param name="uuid">标识符</param>
        void AddNewAuthInfo(AuthInfoModel authInfo, PlayerUUID uuid);

        /// <summary>
        ///     添加新的游戏信息
        /// </summary>
        /// <param name="gameProfile">游戏信息</param>
        void AddNewGameProfile(GameProfileModel gameProfile);

        /// <summary>
        ///     获取验证信息
        /// </summary>
        /// <param name="uuid">标识符</param>
        /// <returns>找到的验证信息</returns>
        AuthInfoModel GetAuthInfo(PlayerUUID uuid);

        /// <summary>
        ///     获取游戏信息
        /// </summary>
        /// <param name="name">游戏名称</param>
        /// <returns>找到的游戏信息</returns>
        GameProfileModel GetGameProfile(string name);

        /// <summary>
        ///     选择一个用户信息作为默认用户
        /// </summary>
        /// <param name="uuid"></param>
        void SelectUser(PlayerUUID uuid);

        /// <summary>
        ///     选择某个游戏信息作为默认游戏
        /// </summary>
        /// <param name="name"></param>
        void SelectGameProfile(string name);

        /// <summary>
        ///     确定游戏信息是否存在
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        bool IsGameProfileExist(string name);

        /// <summary>
        ///     确认验证信息是否存在
        /// </summary>
        /// <param name="uuid"></param>
        /// <param name="userName"></param>
        /// <returns></returns>
        bool IsAuthInfoExist(PlayerUUID uuid, string userName);

        /// <summary>
        ///     清空游戏信息
        /// </summary>
        void EmptyGameProfiles();

        /// <summary>
        ///     清空验证信息
        /// </summary>
        void EmptyAuthInfo();

        /// <summary>
        ///     删除一个游戏信息
        /// </summary>
        /// <param name="name"></param>
        void RemoveGameProfile(string name);

        /// <summary>
        ///     删除一个验证信息
        /// </summary>
        /// <param name="uuid"></param>
        void RemoveAuthInfo(PlayerUUID uuid);

        /// <summary>
        ///     保存整个launcher_profiles
        /// </summary>
        void SaveProfile();
    }
}