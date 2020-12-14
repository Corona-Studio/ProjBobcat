using System.Collections.Generic;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.MicrosoftAuth
{
    public class AuthMojangResponseModel
    {
        [JsonProperty("username")] public string UserName { get; set; }

        [JsonProperty("roles")] public List<object> Roles { get; set; }

        [JsonProperty("access_token")] public string AccessToken { get; set; }

        [JsonProperty("token_type")] public string TokenType { get; set; }

        [JsonProperty("expires_in")] public long ExpiresIn { get; set; }
    }
}