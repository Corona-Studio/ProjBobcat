using System.Collections.Generic;
using ProjBobcat.DefaultComponent.Authenticator;

namespace ProjBobcat.Class.Model.MicrosoftAuth
{
    public class AuthTokenRequestModel
    {
        /// <summary>
        /// Get request model
        /// </summary>
        /// <param name="authCode"></param>
        /// <returns></returns>
        public static Dictionary<string, string> Get(string authCode) => new Dictionary<string, string>
        {
            {
                "client_id",
                MicrosoftAuthenticator.MSClientId
            },
            {
                "code",
                authCode
            },
            {
                "grant_type",
                "authorization_code"
            },
            {
                "redirect_uri",
                MicrosoftAuthenticator.MSAuthRedirectUrl
            },
            {
                "scope",
                MicrosoftAuthenticator.MSAuthScope
            }
        };
    }
}