using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        public static string ResolveUserProperties(this List<PropertyModel> properties)
        {
            if (!(properties?.Any() ?? false))
                return "{}";

            var sb = new StringBuilder();
            sb.Append('{');
            foreach (var item in properties) sb.AppendFormat("\"{0}\":[\"{1}\"],", item.Name, item.Value);

            var totalSb = new StringBuilder();
            totalSb.Append(sb.ToString().TrimEnd(',').Trim()).Append('}');
            return totalSb.ToString();
        }

        /// <summary>
        /// PropertyModel转UserProperty
        /// </summary>
        /// <param name="model">PropertyModel</param>
        /// <param name="profiles">Profile集合</param>
        /// <returns>转换好的UserProperty</returns>
        public static AuthPropertyModel ToAuthProperty(PropertyModel model,
            Dictionary<string, AuthProfileModel> profiles)
        {
            return model is null ? null : new AuthPropertyModel {
                Name = model.Name,
                UserId = profiles.First().Key,
                Value = model.Value
            };
        }

        /// <summary>
        /// PropertyModels转UserProperties
        /// </summary>
        /// <param name="model">PropertyModel集合</param>
        /// <param name="profiles">Profile集合</param>
        /// <returns>转换好的UserProperty</returns>
        public static List<AuthPropertyModel> ToAuthProperties(List<PropertyModel> model,
            Dictionary<string, AuthProfileModel> profiles)
        {
            return model == null ? new List<AuthPropertyModel>() : model.Select(x => ToAuthProperty(x, profiles)).ToList();
        }
    }
}
