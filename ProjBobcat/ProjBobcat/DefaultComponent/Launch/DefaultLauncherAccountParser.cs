using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using ProjBobcat.Class;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model.LauncherAccount;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.Launch
{
    public class DefaultLauncherAccountParser : LauncherParserBase, ILauncherAccountParser
    {
        private readonly string FullLauncherAccountPath;

        /// <summary>
        ///     构造函数
        /// </summary>
        /// <param name="rootPath"></param>
        /// <param name="clientToken"></param>
        public DefaultLauncherAccountParser(string rootPath, Guid clientToken)
        {
            RootPath = rootPath;
            FullLauncherAccountPath = Path.Combine(rootPath, GamePathHelper.GetLauncherAccountPath());

            if (!File.Exists(FullLauncherAccountPath))
            {
                var launcherAccount = new LauncherAccountModel
                {
                    Accounts = new Dictionary<string, AccountModel>(),
                    MojangClientToken = clientToken.ToString("N")
                };

                LauncherAccount = launcherAccount;

                var launcherProfileJson =
                    JsonConvert.SerializeObject(launcherAccount, JsonHelper.CamelCasePropertyNamesSettings);

                if (!Directory.Exists(RootPath))
                    Directory.CreateDirectory(RootPath);

                FileHelper.Write(FullLauncherAccountPath, launcherProfileJson);
            }
            else
            {
                var launcherProfileJson =
                    File.ReadAllText(FullLauncherAccountPath, Encoding.UTF8);
                LauncherAccount = JsonConvert.DeserializeObject<LauncherAccountModel>(launcherProfileJson);
            }
        }

        public LauncherAccountModel LauncherAccount { get; set; }

        public bool ActivateAccount(string uuid)
        {
            if (!(LauncherAccount?.Accounts?.ContainsKey(uuid) ?? false))
                return false;

            LauncherAccount.ActiveAccountLocalId = uuid;

            Save();
            return true;
        }

        public bool AddNewAccount(string uuid, AccountModel account)
        {
            if (LauncherAccount?.Accounts?.ContainsKey(uuid) ?? false)
                return false;

            if (LauncherAccount == null)
                return false;

            LauncherAccount.Accounts ??= new Dictionary<string, AccountModel>();
            LauncherAccount.Accounts.Add(uuid, account);

            Save();
            return true;
        }

        public KeyValuePair<string, AccountModel> Find(string uuid, string name)
        {
            var account =
                LauncherAccount?.Accounts?
                    .FirstOrDefault(a =>
                        a.Value.MinecraftProfile.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                        a.Value.MinecraftProfile.Id.Equals(uuid, StringComparison.OrdinalIgnoreCase)) ?? default;

            return account;
        }

        public bool RemoveAccount(string uuid, string name)
        {
            var (key, value) = Find(uuid, name);
            if (value == default)
                return false;

            LauncherAccount.Accounts.Remove(key);

            Save();
            return true;
        }

        public void Save()
        {
            if (File.Exists(FullLauncherAccountPath))
                File.Delete(FullLauncherAccountPath);

            var launcherProfileJson =
                JsonConvert.SerializeObject(LauncherAccount, JsonHelper.CamelCasePropertyNamesSettings);

            FileHelper.Write(FullLauncherAccountPath, launcherProfileJson);
        }
    }
}