using System.Collections.Generic;
using ProjBobcat.DefaultComponent.Authenticator;

namespace ProjBobcat.Class.Model.MicrosoftAuth
{
    public class AuthTokenRequestModel
    {
        public static Dictionary<string, string> GetRefresh(string token, string clientId, string redirect)
        {
            return new()
            {
                {
                    "client_id",
                    clientId
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
                    redirect
                }
            };
        }

        /// <summary>
        ///     Get request model
        /// </summary>
        /// <param name="authCode"></param>
        /// <returns></returns>
        public static Dictionary<string, string> Get(string authCode, string clientId, string redirect)
        {
            return new()
            {
                {
                    "client_id",
                    clientId
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
                    redirect
                }
            };
        }
    }
}