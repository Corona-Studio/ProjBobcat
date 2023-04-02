using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model.MicrosoftAuth;

public class XBLProperties
{
    public string AuthMethod { get; set; }
    public string SiteName { get; set; }
    public string RpsTicket { get; set; }
}

public class AuthXBLRequestModel
{
    public XBLProperties Properties { get; set; }
    public string RelyingParty { get; set; }
    public string TokenType { get; set; }

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
partial class AuthXBLRequestModelContext : JsonSerializerContext
{
}