using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.MicrosoftAuth;

public class XSTSProperties
{
    public required string SandboxId { get; init; }
    public required string[] UserTokens { get; init; }
}

public class AuthXSTSRequestModel
{
    public required XSTSProperties Properties { get; init; }
    public required string RelyingParty { get; init; }
    public required string TokenType { get; init; }

    public static AuthXSTSRequestModel Get(string token, string relyingParty = "rp://api.minecraftservices.com/")
    {
        return new AuthXSTSRequestModel
        {
            Properties = new XSTSProperties
            {
                SandboxId = "RETAIL",
                UserTokens = [token]
            },
            RelyingParty = relyingParty,
            TokenType = "JWT"
        };
    }
}

[JsonSerializable(typeof(AuthXSTSRequestModel))]
partial class AuthXSTSRequestModelContext : JsonSerializerContext;