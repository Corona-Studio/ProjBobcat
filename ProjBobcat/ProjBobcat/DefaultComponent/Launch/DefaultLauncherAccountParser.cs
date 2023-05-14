using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using ProjBobcat.Class;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model.LauncherAccount;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.Launch;

public class DefaultLauncherAccountParser : LauncherParserBase, ILauncherAccountParser
{
    readonly string FullLauncherAccountPath;

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
                JsonSerializer.Serialize(launcherAccount, typeof(LauncherAccountModel),
                    new LauncherAccountModelContext(JsonHelper.CamelCasePropertyNamesSettings()));

            if (!Directory.Exists(RootPath))
                Directory.CreateDirectory(RootPath);

            File.WriteAllText(FullLauncherAccountPath, launcherProfileJson);
        }
        else
        {
            var launcherProfileJson =
                File.ReadAllText(FullLauncherAccountPath, Encoding.UTF8);
            LauncherAccount = JsonSerializer.Deserialize(launcherProfileJson,
                LauncherAccountModelContext.Default.LauncherAccountModel);
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

    public bool AddNewAccount(string uuid, AccountModel account, out Guid? id)
    {
        if (LauncherAccount?.Accounts?.ContainsKey(uuid) ?? false)
        {
            id = null;
            return false;
        }

        if (LauncherAccount == null)
        {
            id = null;
            return false;
        }

        LauncherAccount.Accounts ??= new Dictionary<string, AccountModel>();

        var newId = Guid.NewGuid();
        /*
        var existsAccount = LauncherAccount.Accounts
            .FirstOrDefault(p => p.Value?.RemoteId?.Equals(account.RemoteId, StringComparison.OrdinalIgnoreCase) ?? false);
        var (key, value) = existsAccount;

        if(!string.IsNullOrEmpty(key) && value != null)
        {
            LauncherAccount.Accounts[key] = value;
        }
        else
        */
        {
            var findResult = Find(account.Id);
            if (findResult is { Key: not null, Value: not null })
            {
                newId = account.Id;
                LauncherAccount.Accounts[findResult.Value.Key] = account;
            }
            else
            {
                if (account.Id == default) account.Id = newId;

                LauncherAccount.Accounts.Add(uuid, account);
            }
        }

        Save();

        id = newId;
        return true;
    }

    public KeyValuePair<string, AccountModel>? Find(Guid id)
    {
        return LauncherAccount?.Accounts?.FirstOrDefault(a => a.Value.Id == id);
    }

    public bool RemoveAccount(Guid id)
    {
        var result = Find(id);
        if (!result.HasValue) return false;

        var (key, value) = result.Value;
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
            JsonSerializer.Serialize(LauncherAccount, typeof(LauncherAccountModel),
                new LauncherAccountModelContext(JsonHelper.CamelCasePropertyNamesSettings()));

        File.WriteAllText(FullLauncherAccountPath, launcherProfileJson);
    }
}