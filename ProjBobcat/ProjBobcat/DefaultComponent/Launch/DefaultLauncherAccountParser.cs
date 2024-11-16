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
    readonly string _fullLauncherAccountPath;
    readonly object _lock = new();

    /// <summary>
    ///     构造函数
    /// </summary>
    /// <param name="rootPath"></param>
    /// <param name="clientToken"></param>
    public DefaultLauncherAccountParser(string rootPath, Guid clientToken) : base(rootPath)
    {
        this._fullLauncherAccountPath = Path.Combine(rootPath, GamePathHelper.GetLauncherAccountPath());

        if (!File.Exists(this._fullLauncherAccountPath))
        {
            this.LauncherAccount = this.GenerateLauncherAccountModel(clientToken);
        }
        else
        {
            var launcherProfileJson =
                File.ReadAllText(this._fullLauncherAccountPath, Encoding.UTF8);
            var val = JsonSerializer.Deserialize(launcherProfileJson,
                LauncherAccountModelContext.Default.LauncherAccountModel);

            if (val == null)
            {
                this.LauncherAccount = this.GenerateLauncherAccountModel(clientToken);
                return;
            }

            this.LauncherAccount = val;
        }
    }

    public LauncherAccountModel LauncherAccount { get; set; }

    public bool ActivateAccount(string uuid)
    {
        lock (this._lock)
        {
            if (!(this.LauncherAccount?.Accounts?.ContainsKey(uuid) ?? false))
                return false;

            this.LauncherAccount.ActiveAccountLocalId = uuid;
            this.Save();
        }

        return true;
    }

    public bool AddNewAccount(string uuid, AccountModel account, out Guid? id)
    {
        if (this.LauncherAccount == null)
        {
            id = null;
            return false;
        }

        this.LauncherAccount.Accounts ??= [];

        lock (this._lock)
        {
            if (this.LauncherAccount.Accounts.ContainsKey(uuid))
            {
                id = null;
                return false;
            }
        }

        lock (this._lock)
        {
            var oldRecord = this.LauncherAccount.Accounts
                .FirstOrDefault(a => a.Value.MinecraftProfile?.Id == account.MinecraftProfile?.Id).Value;
            if (oldRecord != null)
            {
                id = oldRecord.Id;
                return true;
            }
        }


        lock (this._lock)
        {
            var newId = Guid.NewGuid();
            var findResult = this.Find(account.Id);

            if (findResult is { Key: not null, Value: not null })
            {
                newId = account.Id;
                this.LauncherAccount.Accounts[findResult.Value.Key] = account;
            }
            else
            {
                if (account.Id == default) account.Id = newId;

                this.LauncherAccount.Accounts.Add(uuid, account);
            }

            this.Save();
            id = newId;
        }

        return true;
    }

    public KeyValuePair<string, AccountModel>? Find(Guid id)
    {
        lock (this._lock)
        {
            return this.LauncherAccount?.Accounts?.FirstOrDefault(a => a.Value.Id == id);
        }
    }

    public bool RemoveAccount(Guid id)
    {
        var result = this.Find(id);

        if (!result.HasValue) return false;

        var (key, _) = result.Value;

        if (string.IsNullOrEmpty(key)) return false;

        lock (this._lock)
        {
            this.LauncherAccount?.Accounts?.Remove(key);
            this.Save();
        }

        return true;
    }

    public void Save()
    {
        var launcherProfileJson =
            JsonSerializer.Serialize(this.LauncherAccount, typeof(LauncherAccountModel),
                new LauncherAccountModelContext(JsonHelper.CamelCasePropertyNamesSettings()));

        for (var i = 0; i < 3; i++)
            try
            {
                File.WriteAllText(this._fullLauncherAccountPath, launcherProfileJson);
                break;
            }
            catch (IOException)
            {
                if (i == 2) throw;
            }
    }

    LauncherAccountModel GenerateLauncherAccountModel(Guid clientToken)
    {
        var launcherAccount = new LauncherAccountModel
        {
            Accounts = [],
            MojangClientToken = clientToken.ToString("N")
        };

        var launcherProfileJson =
            JsonSerializer.Serialize(launcherAccount, typeof(LauncherAccountModel),
                new LauncherAccountModelContext(JsonHelper.CamelCasePropertyNamesSettings()));

        if (!Directory.Exists(this.RootPath))
            Directory.CreateDirectory(this.RootPath);

        File.WriteAllText(this._fullLauncherAccountPath, launcherProfileJson);

        return launcherAccount;
    }
}