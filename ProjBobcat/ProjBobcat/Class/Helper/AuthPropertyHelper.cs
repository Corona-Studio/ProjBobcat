using ProjBobcat.Class.Model.Auth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.YggdrasilAuth;

namespace ProjBobcat.Class.Helper;

/// <summary>
///     AuthProperty工具类
/// </summary>
public static class AuthPropertyHelper
{
    extension(IEnumerable<PropertyModel>? properties)
    {
        /// <summary>
        ///     解析User Property
        /// </summary>
        /// <returns>解析好的User Property</returns>
        public string ResolveUserProperties()
        {
            if (properties == null) return "{}";

            var keyValues = properties
                .ToDictionary(
                    item => item.Name,
                    item => new[] { item.Value });

            return JsonSerializer.Serialize(keyValues, SerializerContext.Default.DictionaryStringStringArray);
        }

        /// <summary>
        ///     PropertyModels转UserProperties
        /// </summary>
        /// <param name="profiles">Profile集合</param>
        /// <returns>转换好的UserProperty</returns>
        public IEnumerable<AuthPropertyModel> ToAuthProperties(
            IReadOnlyDictionary<Guid, AuthProfileModel> profiles)
        {
            return properties == null
                ? []
                : properties
                    .Select(model => model.ToAuthProperty(profiles))
                    .OfType<AuthPropertyModel>();
        }
    }

    extension(PropertyModel? model)
    {
        /// <summary>
        ///     PropertyModel转UserProperty
        /// </summary>
        /// <param name="profiles">Profile集合</param>
        /// <returns>转换好的UserProperty</returns>
        public AuthPropertyModel? ToAuthProperty(
            IReadOnlyDictionary<Guid, AuthProfileModel> profiles)
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
    }
}