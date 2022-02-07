using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
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
    readonly string FullLauncherProfilePath;

    /// <summary>
    ///     构造函数
    /// </summary>
    /// <param name="rootPath"></param>
    /// <param name="clientToken"></param>
    public DefaultLauncherProfileParser(string rootPath, Guid clientToken)
    {
        RootPath = rootPath;
        FullLauncherProfilePath = Path.Combine(rootPath, GamePathHelper.GetLauncherProfilePath());

        if (!File.Exists(FullLauncherProfilePath))
        {
            var launcherProfile = new LauncherProfileModel
            {
                ClientToken = clientToken.ToString("D"),
                LauncherVersion = new LauncherVersionModel
                {
                    Format = 1,
                    Name = string.Empty
                },
                Profiles = new Dictionary<string, GameProfileModel>()
            };

            LauncherProfile = launcherProfile;

            var launcherProfileJson =
                JsonConvert.SerializeObject(launcherProfile, JsonHelper.CamelCasePropertyNamesSettings);

            if (!Directory.Exists(RootPath))
                Directory.CreateDirectory(RootPath);

            File.WriteAllText(FullLauncherProfilePath, launcherProfileJson);
        }
        else
        {
            var launcherProfileJson =
                File.ReadAllText(FullLauncherProfilePath, Encoding.UTF8);
            LauncherProfile = JsonConvert.DeserializeObject<LauncherProfileModel>(launcherProfileJson);
        }
    }

    public LauncherProfileModel LauncherProfile { get; set; }

    public void AddNewGameProfile(GameProfileModel gameProfile)
    {
        if (IsGameProfileExist(gameProfile.Name)) return;

        LauncherProfile.Profiles.Add(gameProfile.Name, gameProfile);
        SaveProfile();
    }

    public void EmptyGameProfiles()
    {
        LauncherProfile.Profiles?.Clear();
        SaveProfile();
    }

    public GameProfileModel GetGameProfile(string name)
    {
        var profile = LauncherProfile.Profiles.FirstOrDefault(
                   p => p.Value.Name.Equals(name, StringComparison.Ordinal)).Value ??
               throw new UnknownGameNameException(name);

        profile.Resolution ??= new ResolutionModel();

        return profile;
    }

    public bool IsGameProfileExist(string name)
    {
        return LauncherProfile.Profiles.Any(p => p.Value.Name.Equals(name, StringComparison.Ordinal));
    }

    public void RemoveGameProfile(string name)
    {
        LauncherProfile.Profiles.Remove(name);
    }

    public void SaveProfile()
    {
        if (File.Exists(FullLauncherProfilePath))
            File.Delete(FullLauncherProfilePath);

        var launcherProfileJson =
            JsonConvert.SerializeObject(LauncherProfile, JsonHelper.CamelCasePropertyNamesSettings);

        File.WriteAllText(FullLauncherProfilePath, launcherProfileJson);
    }

    public void SelectGameProfile(string name)
    {
        if (!IsGameProfileExist(name)) throw new KeyNotFoundException();

        LauncherProfile.SelectedUser.Profile = name;
        SaveProfile();
    }

    public void SelectUser(PlayerUUID uuid)
    {
        LauncherProfile.SelectedUser.Account = uuid.ToString();
        SaveProfile();
    }
}