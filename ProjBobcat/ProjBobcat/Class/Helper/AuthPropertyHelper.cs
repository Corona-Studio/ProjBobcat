using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.LauncherProfile;
using ProjBobcat.Class.Model.YggdrasilAuth;

namespace ProjBobcat.Class.Helper;

[JsonSerializable(typeof(Dictionary<string, string[]>))]
partial class UserPropertiesContext : JsonSerializerContext
{
}

/// <summary>
///     AuthProperty工具类
/// </summary>
public static class AuthPropertyHelper
{
    /// <summary>
    ///     解析User Property
    /// </summary>
    /// <param name="properties">Property 集合</param>
    /// <returns>解析好的User Property</returns>
    public static string ResolveUserProperties(this IEnumerable<PropertyModel>? properties)
    {
        if (properties == null) return "{}";

        var keyValues = properties
            .ToDictionary(
                item => item.Name,
                item => new[] { item.Value });

        return JsonSerializer.Serialize(keyValues, UserPropertiesContext.Default.DictionaryStringStringArray);
    }


    /// <summary>
    ///     PropertyModel转UserProperty
    /// </summary>
    /// <param name="model">PropertyModel</param>
    /// <param name="profiles">Profile集合</param>
    /// <returns>转换好的UserProperty</returns>
    public static AuthPropertyModel ToAuthProperty(this PropertyModel model,
        IReadOnlyDictionary<PlayerUUID, AuthProfileModel> profiles)
    {
        return model is null
            ? null
            : new AuthPropertyModel
            {
                Name = model.Name,
                UserId = profiles.First().Key,
                Value = model.Value
            };
    }

    /// <summary>
    ///     PropertyModels转UserProperties
    /// </summary>
    /// <param name="models">PropertyModel集合</param>
    /// <param name="profiles">Profile集合</param>
    /// <returns>转换好的UserProperty</returns>
    public static IEnumerable<AuthPropertyModel> ToAuthProperties(this IEnumerable<PropertyModel> models,
        IReadOnlyDictionary<PlayerUUID, AuthProfileModel> profiles)
    {
        return models == null
            ? new List<AuthPropertyModel>()
            : models.Select(model => model.ToAuthProperty(profiles));
    }
}