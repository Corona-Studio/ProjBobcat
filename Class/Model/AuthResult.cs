using System.Collections.Generic;
using ProjBobcat.Class.Model.YggdrasilAuth;

namespace ProjBobcat.Class.Model
{
    public class AuthResult
    {
        public AuthStatus AuthStatus { get; set; }
        public string AccessToken { get; set; }
        public ProfileInfoModel SelectedProfile { get; set; }
        public List<ProfileInfoModel> Profiles { get; set; }
        public ErrorModel Error { get; set; }
        public UserInfoModel User { get; set; }
    }
}