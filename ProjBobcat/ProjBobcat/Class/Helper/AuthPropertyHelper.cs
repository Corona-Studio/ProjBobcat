using System.Collections.Generic;
using System.Linq;
using ProjBobcat.Class.Model.LauncherProfile;
using ProjBobcat.Class.Model.YggdrasilAuth;

namespace ProjBobcat.Class.Helper
{
    public static class AuthPropertyHelper
    {
        public static AuthPropertyModel ToAuthProperty(PropertyModel model,
            Dictionary<string, AuthProfileModel> profiles)
        {
            if (model == null || model.Equals(default(PropertyModel)))
                return null;

            return new AuthPropertyModel
            {
                Name = model.Name,
                UserId = profiles.First().Key,
                Value = model.Value
            };
        }

        public static List<AuthPropertyModel> ToAuthProperties(List<PropertyModel> model,
            Dictionary<string, AuthProfileModel> profiles)
        {
            var result = new List<AuthPropertyModel>();

            if (model == null || !model.Any())
                return result;

            model.ForEach(m => { result.Add(ToAuthProperty(m, profiles)); });

            return result;
        }
    }
}