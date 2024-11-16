using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using ProjBobcat.Class;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.LauncherProfile;
using ProjBobcat.Exceptions;
using ProjBobcat.Interface;

namespace ProjBobcat.DefaultComponent.Launch;

/// <summary>
///     默认的官方launcher_profile.json适配器
/// </summary>
public sealed class DefaultLauncherProfileParser : LauncherParserBase, ILauncherProfileParser
{
    readonly string _fullLauncherProfilePath;
    readonly object _lock = new();

    /// <summary>
    ///     构造函数
    /// </summary>
    /// <param name="rootPath"></param>
    /// <param name="clientToken"></param>
    public DefaultLauncherProfileParser(string rootPath, Guid clientToken) : base(rootPath)
    {
        this._fullLauncherProfilePath = Path.Combine(rootPath, GamePathHelper.GetLauncherProfilePath());

        if (!File.Exists(this._fullLauncherProfilePath))
        {
            this.LauncherProfile = this.GenerateLauncherProfile(clientToken, rootPath);
        }
        else
        {
            var launcherProfileJson =
                File.ReadAllText(this._fullLauncherProfilePath, Encoding.UTF8);
            var result = JsonSerializer.Deserialize(launcherProfileJson,
                LauncherProfileModelContext.Default.LauncherProfileModel);

            if (result == null)
            {
                this.LauncherProfile = this.GenerateLauncherProfile(clientToken, rootPath);
                return;
            }

            this.LauncherProfile = result;
        }
    }

    public LauncherProfileModel LauncherProfile { get; set; }

    public void AddNewGameProfile(GameProfileModel gameProfile)
    {
        if (string.IsNullOrEmpty(gameProfile.Name)) return;
        if (this.IsGameProfileExist(gameProfile.Name)) return;

        lock (this._lock)
        {
            this.LauncherProfile.Profiles!.Add(gameProfile.Name, gameProfile);
            this.SaveProfile();
        }
    }

    public void EmptyGameProfiles()
    {
        lock (this._lock)
        {
            this.LauncherProfile.Profiles?.Clear();
            this.SaveProfile();
        }
    }

    public GameProfileModel GetGameProfile(string name)
    {
        lock (this._lock)
        {
            var profile = this.LauncherProfile.Profiles!.FirstOrDefault(
                              p => p.Value.Name?.Equals(name, StringComparison.Ordinal) ?? false).Value ??
                          throw new UnknownGameNameException(name);

            profile.Resolution ??= new ResolutionModel();

            return profile;
        }
    }

    public bool IsGameProfileExist(string name)
    {
        lock (this._lock)
        {
            return this.LauncherProfile.Profiles!
                .Any(p => p.Value.Name?.Equals(name, StringComparison.Ordinal) ?? false);
        }
    }

    public void RemoveGameProfile(string name)
    {
        lock (this._lock)
        {
            this.LauncherProfile.Profiles!.Remove(name);
        }
    }

    public void SaveProfile()
    {
        var launcherProfileJson =
            JsonSerializer.Serialize(this.LauncherProfile, typeof(LauncherProfileModel),
                new LauncherProfileModelContext(JsonHelper.CamelCasePropertyNamesSettings()));

        for (var i = 0; i < 3; i++)
            try
            {
                File.WriteAllText(this._fullLauncherProfilePath, launcherProfileJson);
                break;
            }
            catch (IOException)
            {
                if (i == 2) throw;
            }
    }

    public void SelectGameProfile(string name)
    {
        if (!this.IsGameProfileExist(name)) throw new KeyNotFoundException();

        this.LauncherProfile.SelectedUser ??= new SelectedUserModel();
        this.LauncherProfile.SelectedUser.Profile = name;
        this.SaveProfile();
    }

    public void SelectUser(PlayerUUID uuid)
    {
        this.LauncherProfile.SelectedUser ??= new SelectedUserModel();
        this.LauncherProfile.SelectedUser.Account = uuid.ToString();
        this.SaveProfile();
    }

    LauncherProfileModel GenerateLauncherProfile(
        Guid clientToken,
        string rootPath)
    {
        var launcherProfile = new LauncherProfileModel
        {
            ClientToken = clientToken.ToString("D"),
            LauncherVersion = new LauncherVersionModel
            {
                Format = 1,
                Name = string.Empty
            },
            Profiles = [],
            SelectedUser = new SelectedUserModel()
        };

        this.LauncherProfile = launcherProfile;

        var launcherProfileJson =
            JsonSerializer.Serialize(launcherProfile, typeof(LauncherProfileModel),
                new LauncherProfileModelContext(JsonHelper.CamelCasePropertyNamesSettings()));

        if (!Directory.Exists(this.RootPath))
            Directory.CreateDirectory(rootPath);

        File.WriteAllText(this._fullLauncherProfilePath, launcherProfileJson);

        return launcherProfile;
    }
}