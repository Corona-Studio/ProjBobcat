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
    readonly object _lock = new();
    readonly string _fullLauncherProfilePath;

    /// <summary>
    ///     构造函数
    /// </summary>
    /// <param name="rootPath"></param>
    /// <param name="clientToken"></param>
    public DefaultLauncherProfileParser(string rootPath, Guid clientToken) : base(rootPath)
    {
        _fullLauncherProfilePath = Path.Combine(rootPath, GamePathHelper.GetLauncherProfilePath());
        
        if (!File.Exists(_fullLauncherProfilePath))
        {
            LauncherProfile = GenerateLauncherProfile(clientToken, rootPath);
        }
        else
        {
            var launcherProfileJson =
                File.ReadAllText(_fullLauncherProfilePath, Encoding.UTF8);
            var result = JsonSerializer.Deserialize(launcherProfileJson,
                LauncherProfileModelContext.Default.LauncherProfileModel);

            if (result == null)
            {
                LauncherProfile = GenerateLauncherProfile(clientToken, rootPath);
                return;
            }
            
            LauncherProfile = result;
        }
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

        LauncherProfile = launcherProfile;

        var launcherProfileJson =
            JsonSerializer.Serialize(launcherProfile, typeof(LauncherProfileModel),
                new LauncherProfileModelContext(JsonHelper.CamelCasePropertyNamesSettings()));

        if (!Directory.Exists(RootPath))
            Directory.CreateDirectory(rootPath);

        File.WriteAllText(_fullLauncherProfilePath, launcherProfileJson);
        
        return launcherProfile;
    }

    public LauncherProfileModel LauncherProfile { get; set; }

    public void AddNewGameProfile(GameProfileModel gameProfile)
    {
        if (string.IsNullOrEmpty(gameProfile.Name)) return;
        if (IsGameProfileExist(gameProfile.Name)) return;

        lock (_lock)
        {
            LauncherProfile.Profiles!.Add(gameProfile.Name, gameProfile);
            SaveProfile();
        }
    }

    public void EmptyGameProfiles()
    {
        lock (_lock)
        {
            LauncherProfile.Profiles?.Clear();
            SaveProfile();
        }
    }

    public GameProfileModel GetGameProfile(string name)
    {
        lock (_lock)
        {
            var profile = LauncherProfile.Profiles!.FirstOrDefault(
                              p => p.Value.Name?.Equals(name, StringComparison.Ordinal) ?? false).Value ??
                          throw new UnknownGameNameException(name);

            profile.Resolution ??= new ResolutionModel();

            return profile;
        }
    }

    public bool IsGameProfileExist(string name)
    {
        lock (_lock)
        {
            return LauncherProfile.Profiles!
                .Any(p => p.Value.Name?.Equals(name, StringComparison.Ordinal) ?? false);
        }
    }

    public void RemoveGameProfile(string name)
    {
        lock (_lock)
        {
            LauncherProfile.Profiles!.Remove(name);
        }
    }

    public void SaveProfile()
    {
        var launcherProfileJson =
            JsonSerializer.Serialize(LauncherProfile, typeof(LauncherProfileModel),
                new LauncherProfileModelContext(JsonHelper.CamelCasePropertyNamesSettings()));

        for (var i = 0; i < 3; i++)
        {
            try
            {
                File.WriteAllText(_fullLauncherProfilePath, launcherProfileJson);
                break;
            }
            catch (IOException)
            {
                if (i == 2) throw;
            }
        }
    }

    public void SelectGameProfile(string name)
    {
        if (!IsGameProfileExist(name)) throw new KeyNotFoundException();

        LauncherProfile.SelectedUser ??= new SelectedUserModel();
        LauncherProfile.SelectedUser.Profile = name;
        SaveProfile();
    }

    public void SelectUser(PlayerUUID uuid)
    {
        LauncherProfile.SelectedUser ??= new SelectedUserModel();
        LauncherProfile.SelectedUser.Account = uuid.ToString();
        SaveProfile();
    }
}