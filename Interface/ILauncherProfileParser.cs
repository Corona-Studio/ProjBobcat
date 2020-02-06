using ProjBobcat.Class.Model.LauncherProfile;

namespace ProjBobcat.Interface
{
    public interface ILauncherProfileParser
    {
        LauncherProfileModel LauncherProfile { get; set; }
        void AddNewAuthInfo(AuthInfoModel authInfo, string guid);
        void AddNewGameProfile(GameProfileModel gameProfile);
        AuthInfoModel GetAuthInfo(string uuid);
        GameProfileModel GetGameProfile(string name);
        void SelectUser(string uuid);
        void SelectGameProfile(string name);
        bool IsGameProfileExist(string name);
        bool IsAuthInfoExist(string uuid, string userName);
        void EmptyGameProfiles();
        void EmptyAuthInfo();
        void RemoveGameProfile(string name);
        void RemoveAuthInfo(string uuid);
        void SaveProfile();
    }
}