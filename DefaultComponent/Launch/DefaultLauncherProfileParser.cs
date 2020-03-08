using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using ProjBobcat.Class;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model.LauncherProfile;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.Launch
{
    public sealed class DefaultLauncherProfileParser : LauncherProfileParserBase, ILauncherProfileParser
    {
        /// <summary>
        ///     构造函数
        /// </summary>
        /// <param name="rootPath"></param>
        /// <param name="clientToken"></param>
        public DefaultLauncherProfileParser(string rootPath, Guid clientToken)
        {
            RootPath = rootPath;

            if (!File.Exists(GamePathHelper.GetLauncherProfilePath(RootPath)))
            {
                var launcherProfile = new LauncherProfileModel
                {
                    AuthenticationDatabase = new Dictionary<string, AuthInfoModel>(),
                    ClientToken = clientToken.ToString("D"),
                    LauncherVersion = new LauncherVersionModel
                    {
                        Format = 1,
                        Name = ""
                    },
                    Profiles = new Dictionary<string, GameProfileModel>()
                };

                LauncherProfile = launcherProfile;

                var launcherProfileJson = JsonConvert.SerializeObject(launcherProfile, new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });

                if (!Directory.Exists(RootPath))
                    Directory.CreateDirectory(RootPath);

                FileHelper.Write(GamePathHelper.GetLauncherProfilePath(RootPath), launcherProfileJson);
            }
            else
            {
                var launcherProfileJson =
                    File.ReadAllText(GamePathHelper.GetLauncherProfilePath(rootPath), Encoding.UTF8);
                LauncherProfile = JsonConvert.DeserializeObject<LauncherProfileModel>(launcherProfileJson);
            }
        }

        public LauncherProfileModel LauncherProfile { get; set; }

        /// <summary>
        ///     添加新的验证信息
        /// </summary>
        /// <param name="authInfo"></param>
        /// <param name="guid"></param>
        public void AddNewAuthInfo(AuthInfoModel authInfo, string guid)
        {
            if (IsAuthInfoExist(guid, authInfo.UserName)) return;
            if (!(LauncherProfile.AuthenticationDatabase?.Any() ?? false))
                LauncherProfile.AuthenticationDatabase = new Dictionary<string, AuthInfoModel>();

            LauncherProfile.AuthenticationDatabase.Add(
                authInfo.Properties.Any() ? authInfo.Properties.First().UserId : authInfo.Profiles.First().Key,
                authInfo);
            SaveProfile();
        }

        /// <summary>
        ///     添加新的游戏信息
        /// </summary>
        /// <param name="gameProfile"></param>
        public void AddNewGameProfile(GameProfileModel gameProfile)
        {
            if (IsGameProfileExist(gameProfile.Name)) return;

            LauncherProfile.Profiles.Add(gameProfile.Name, gameProfile);
            SaveProfile();
        }

        /// <summary>
        ///     清空验证信息
        /// </summary>
        public void EmptyAuthInfo()
        {
            LauncherProfile.AuthenticationDatabase = new Dictionary<string, AuthInfoModel>();
            SaveProfile();
        }

        /// <summary>
        ///     清空游戏信息
        /// </summary>
        public void EmptyGameProfiles()
        {
            LauncherProfile.Profiles = new Dictionary<string, GameProfileModel>();
            SaveProfile();
        }

        /// <summary>
        ///     获取验证信息
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns></returns>
        public AuthInfoModel GetAuthInfo(string uuid)
        {
            return LauncherProfile.AuthenticationDatabase.TryGetValue(uuid, out var authInfo) ? authInfo : null;
        }

        /// <summary>
        ///     获取游戏信息
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public GameProfileModel GetGameProfile(string name)
        {
            return LauncherProfile.Profiles.FirstOrDefault(p => p.Value.Name.Equals(name, StringComparison.Ordinal))
                .Value;
        }

        /// <summary>
        ///     确认验证信息是否存在
        /// </summary>
        /// <param name="uuid"></param>
        /// <param name="userName"></param>
        /// <returns></returns>
        public bool IsAuthInfoExist(string uuid, string userName)
        {
            if (!(LauncherProfile.AuthenticationDatabase?.Any() ?? false)) return false;

            return LauncherProfile.AuthenticationDatabase.Any(a =>
                       a.Value.Profiles?.First().Key.Equals(uuid, StringComparison.Ordinal) ?? false) &&
                   LauncherProfile.AuthenticationDatabase.Any(a =>
                       a.Value.Profiles?.First().Value.DisplayName.Equals(userName, StringComparison.Ordinal) ?? false);
        }

        /// <summary>
        ///     确定游戏信息是否存在
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool IsGameProfileExist(string name)
        {
            return LauncherProfile.Profiles.Any(p => p.Value.Name.Equals(name, StringComparison.Ordinal));
        }

        /// <summary>
        ///     删除一个验证信息
        /// </summary>
        /// <param name="uuid"></param>
        public void RemoveAuthInfo(string uuid)
        {
            LauncherProfile.AuthenticationDatabase.Remove(uuid);
        }

        /// <summary>
        ///     删除一个游戏信息
        /// </summary>
        /// <param name="name"></param>
        public void RemoveGameProfile(string name)
        {
            LauncherProfile.Profiles.Remove(name);
        }

        /// <summary>
        ///     保存整个launcher_profiles
        /// </summary>
        public void SaveProfile()
        {
            if (File.Exists(GamePathHelper.GetLauncherProfilePath(RootPath)))
                File.Delete(GamePathHelper.GetLauncherProfilePath(RootPath));

            var launcherProfileJson = JsonConvert.SerializeObject(LauncherProfile, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            });

            FileHelper.Write(GamePathHelper.GetLauncherProfilePath(RootPath), launcherProfileJson);
        }

        /// <summary>
        ///     选择某个游戏信息作为默认游戏
        /// </summary>
        /// <param name="name"></param>
        public void SelectGameProfile(string name)
        {
            if (!IsGameProfileExist(name)) throw new KeyNotFoundException();

            LauncherProfile.SelectedUser.Profile = name;
            SaveProfile();
        }

        /// <summary>
        ///     选择一个用户信息作为默认用户
        /// </summary>
        /// <param name="uuid"></param>
        public void SelectUser(string uuid)
        {
            LauncherProfile.SelectedUser.Account = uuid;
            SaveProfile();
        }
    }
}