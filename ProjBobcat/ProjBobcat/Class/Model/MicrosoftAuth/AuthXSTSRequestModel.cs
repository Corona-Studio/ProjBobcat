using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.MicrosoftAuth;

public class XSTSProperties
{
    public string SandboxId { get; set; }
    public string[] UserTokens { get; set; }
}

public class AuthXSTSRequestModel
{
    public XSTSProperties Properties { get; set; }
    public string RelyingParty { get; set; }
    public string TokenType { get; set; }

    public static AuthXSTSRequestModel Get(string token, string relyingParty = "rp://api.minecraftservices.com/")
    {
        return new AuthXSTSRequestModel
        {
            Properties = new XSTSProperties
            {
                SandboxId = "RETAIL",
                UserTokens = new[]
                {
                    token
                }
            },
            RelyingParty = relyingParty,
            TokenType = "JWT"
        };
    }
}

[JsonSerializable(typeof(AuthXSTSRequestModel))]
partial class AuthXSTSRequestModelContext : JsonSerializerContext
{
}