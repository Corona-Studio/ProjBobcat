using System.Collections.Generic;

namespace ProjBobcat.Class.Model.MicrosoftAuth
{
    public class XSTSProperties
    {
        public string SandboxId { get; set; }
        public List<string> UserTokens { get; set; }
    }

    public class AuthXSTSRequestModel
    {
        public static AuthXSTSRequestModel Get(string token) =>
            new AuthXSTSRequestModel
            {
                Properties = new XSTSProperties
                {
                    SandboxId = "RETAIL",
                    UserTokens = new List<string>
                    {
                        token
                    }
                },
                RelyingParty = "rp://api.minecraftservices.com/",
                TokenType = "JWT"
            };

        public XSTSProperties Properties { get; set; }
        public string RelyingParty { get; set; }
        public string TokenType { get; set; }
    }
}