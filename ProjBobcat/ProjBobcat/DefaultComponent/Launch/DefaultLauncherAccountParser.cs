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
    readonly object _lock = new();
    readonly string _fullLauncherAccountPath;

    /// <summary>
    ///     构造函数
    /// </summary>
    /// <param name="rootPath"></param>
    /// <param name="clientToken"></param>
    public DefaultLauncherAccountParser(string rootPath, Guid clientToken) : base(rootPath)
    {
        _fullLauncherAccountPath = Path.Combine(rootPath, GamePathHelper.GetLauncherAccountPath());

        if (!File.Exists(_fullLauncherAccountPath))
        {
            LauncherAccount = GenerateLauncherAccountModel(clientToken);
        }
        else
        {
            var launcherProfileJson =
                File.ReadAllText(_fullLauncherAccountPath, Encoding.UTF8);
            var val = JsonSerializer.Deserialize(launcherProfileJson,
                LauncherAccountModelContext.Default.LauncherAccountModel);

            if (val == null)
            {
                LauncherAccount = GenerateLauncherAccountModel(clientToken);
                return;
            }

            LauncherAccount = val;
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

        if (!Directory.Exists(RootPath))
            Directory.CreateDirectory(RootPath);

        File.WriteAllText(_fullLauncherAccountPath, launcherProfileJson);

        return launcherAccount;
    }

    public LauncherAccountModel LauncherAccount { get; set; }

    public bool ActivateAccount(string uuid)
    {
        lock (_lock)
        {
            if (!(LauncherAccount?.Accounts?.ContainsKey(uuid) ?? false))
                return false;

            LauncherAccount.ActiveAccountLocalId = uuid;
            Save();
        }
        
        return true;
    }

    public bool AddNewAccount(string uuid, AccountModel account, out Guid? id)
    {
        if (LauncherAccount == null)
        {
            id = null;
            return false;
        }
        
        LauncherAccount.Accounts ??= [];

        lock (_lock)
        {
            if (LauncherAccount.Accounts.ContainsKey(uuid))
            {
                id = null;
                return false;
            }
        }

        lock (_lock)
        {
            var oldRecord = LauncherAccount.Accounts
                .FirstOrDefault(a => a.Value.MinecraftProfile?.Id == account.MinecraftProfile?.Id).Value;
            if (oldRecord != null)
            {
                id = oldRecord.Id;
                return true;
            }
        }
        

        lock (_lock)
        {
            var newId = Guid.NewGuid();
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

            Save();
            id = newId;
        }
        
        return true;
    }

    public KeyValuePair<string, AccountModel>? Find(Guid id)
    {
        lock (_lock)
        {
            return LauncherAccount?.Accounts?.FirstOrDefault(a => a.Value.Id == id);
        }
    }

    public bool RemoveAccount(Guid id)
    {
        var result = Find(id);

        if (!result.HasValue) return false;

        var (key, _) = result.Value;

        if (string.IsNullOrEmpty(key)) return false;

        lock (_lock)
        {
            LauncherAccount?.Accounts?.Remove(key);
            Save();
        }
        
        return true;
    }

    public void Save()
    {
        var launcherProfileJson =
            JsonSerializer.Serialize(LauncherAccount, typeof(LauncherAccountModel),
                new LauncherAccountModelContext(JsonHelper.CamelCasePropertyNamesSettings()));

        for (var i = 0; i < 3; i++)
        {
            try
            {
                File.WriteAllText(_fullLauncherAccountPath, launcherProfileJson);
                break;
            }
            catch (IOException)
            {
                if (i == 2) throw;
            }
        }
    }
}