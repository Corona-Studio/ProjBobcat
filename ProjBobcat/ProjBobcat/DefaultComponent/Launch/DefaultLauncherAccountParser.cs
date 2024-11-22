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

#if NET9_0_OR_GREATER
using System.Threading;
#endif

namespace ProjBobcat.DefaultComponent.Launch;

public sealed class DefaultLauncherAccountParser : LauncherParserBase, ILauncherAccountParser
{
    readonly string _fullLauncherAccountPath;

#if NET9_0_OR_GREATER
    readonly Lock _lock = new();
#else
    readonly object _lock = new();
#endif

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

    public required LauncherAccountModel LauncherAccount { get; init; }

    public bool ActivateAccount(string uuid)
    {
#if NET9_0_OR_GREATER
        using (this._lock.EnterScope())
#else
        lock (this._lock)
#endif
        {
            if (!(this.LauncherAccount.Accounts?.ContainsKey(uuid) ?? false))
                return false;

            this.LauncherAccount.ActiveAccountLocalId = uuid;
            this.Save();
        }

        return true;
    }

    public bool AddNewAccount(string uuid, AccountModel account, out Guid? id)
    {
        this.LauncherAccount.Accounts ??= [];

#if NET9_0_OR_GREATER
        using (this._lock.EnterScope())
#else
        lock (this._lock)
#endif
        {
            if (this.LauncherAccount.Accounts.ContainsKey(uuid))
            {
                id = null;
                return false;
            }
        }

#if NET9_0_OR_GREATER
        using (this._lock.EnterScope())
#else
        lock (this._lock)
#endif
        {
            var oldRecord = this.LauncherAccount.Accounts
                .FirstOrDefault(a => a.Value.MinecraftProfile?.Id == account.MinecraftProfile?.Id).Value;
            if (oldRecord != null)
            {
                id = oldRecord.Id;
                return true;
            }
        }

#if NET9_0_OR_GREATER
        using (this._lock.EnterScope())
#else
        lock (this._lock)
#endif
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
#if NET9_0_OR_GREATER
        using (this._lock.EnterScope())
#else
        lock (this._lock)
#endif
        {
            return this.LauncherAccount.Accounts?.FirstOrDefault(a => a.Value.Id == id);
        }
    }

    public bool RemoveAccount(Guid id)
    {
        var result = this.Find(id);

        if (!result.HasValue) return false;

        var (key, _) = result.Value;

        if (string.IsNullOrEmpty(key)) return false;

#if NET9_0_OR_GREATER
        using (this._lock.EnterScope())
#else
        lock (this._lock)
#endif
        {
            this.LauncherAccount.Accounts?.Remove(key);
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