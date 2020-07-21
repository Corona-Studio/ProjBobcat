using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.LauncherProfile;
using ProjBobcat.Class.Model.YggdrasilAuth;

namespace ProjBobcat.Class.Helper
{
    /// <summary>
    /// AuthProperty工具类
    /// </summary>
    public static class AuthPropertyHelper
    {
        /// <summary>
        /// 解析User Property
        /// </summary>
        /// <param name="properties">Property 集合</param>
        /// <returns>解析好的User Property</returns>
        public static string ResolveUserProperties(this IEnumerable<PropertyModel> properties)
        {
            Dictionary<string, string> keyValues = new Dictionary<string, string>();
            if (properties != null)
                foreach (var item in properties)
                    keyValues.Add(item.Name, item.Value);
            return Newtonsoft.Json.JsonConvert.SerializeObject(keyValues);
            /*
            if (properties == null)
                return "{}";

            var sb = new StringBuilder();
            sb.Append('{');
            foreach (var item in properties)
                sb.AppendFormat("\"{0}\":[\"{1}\"],", item.Name, item.Value);
            return sb.ToString().TrimEnd(',').Trim() + "}";
            */
        }


        /// <summary>
        /// PropertyModel转UserProperty
        /// </summary>
        /// <param name="model">PropertyModel</param>
        /// <param name="profiles">Profile集合</param>
        /// <returns>转换好的UserProperty</returns>
        public static AuthPropertyModel ToAuthProperty(this PropertyModel model,
            IReadOnlyDictionary<PlayerUUID, AuthProfileModel> profiles)
        {
            return model is null ? null :
                new AuthPropertyModel {
                    Name = model.Name,
                    UserId = profiles.First().Key,
                    Value = model.Value
                };
        }

        /// <summary>
        /// PropertyModels转UserProperties
        /// </summary>
        /// <param name="models">PropertyModel集合</param>
        /// <param name="profiles">Profile集合</param>
        /// <returns>转换好的UserProperty</returns>
        public static IEnumerable<AuthPropertyModel> ToAuthProperties(this IEnumerable<PropertyModel> models,
            IReadOnlyDictionary<PlayerUUID, AuthProfileModel> profiles)
        {
            return models is null ?
                  new List<AuthPropertyModel>() :
                  models.Select(model => model.ToAuthProperty(profiles));
        }
    }
}