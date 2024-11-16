using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.MicrosoftAuth;

public class XBLProperties
{
    public required string AuthMethod { get; init; }
    public required string SiteName { get; init; }
    public required string RpsTicket { get; init; }
}

public class AuthXBLRequestModel
{
    public required XBLProperties Properties { get; init; }
    public required string RelyingParty { get; init; }
    public required string TokenType { get; init; }

    public static AuthXBLRequestModel Get(string accessToken)
    {
        return new AuthXBLRequestModel
        {
            Properties = new XBLProperties
            {
                AuthMethod = "RPS",
                SiteName = "user.auth.xboxlive.com",
                RpsTicket = $"d={accessToken}"
            },
            RelyingParty = "http://auth.xboxlive.com",
            TokenType = "JWT"
        };
    }
}

[JsonSerializable(typeof(AuthXBLRequestModel))]
partial class AuthXBLRequestModelContext : JsonSerializerContext;