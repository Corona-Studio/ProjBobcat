using System.Collections.Generic;
using ProjBobcat.DefaultComponent.Authenticator;

namespace ProjBobcat.Class.Model.MicrosoftAuth
{
    public class AuthTokenRequestModel
    {
        public static Dictionary<string, string> GetRefresh(string token)
        {
            return new()
            {
                {
                    "client_id",
                    MicrosoftAuthenticator.MSClientId
                },
                {
                    "refresh_token",
                    token
                },
                {
                    "grant_type",
                    "refresh_token"
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

        /// <summary>
        ///     Get request model
        /// </summary>
        /// <param name="authCode"></param>
        /// <returns></returns>
        public static Dictionary<string, string> Get(string authCode)
        {
            return new()
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
}