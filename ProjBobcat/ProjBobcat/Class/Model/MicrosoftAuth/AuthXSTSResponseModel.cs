using System;
using System.Text.Json;

namespace ProjBobcat.Class.Model.MicrosoftAuth;

public class AuthXSTSResponseModel
{
    public DateTime IssueInstant { get; set; }
    public DateTime NotAfter { get; set; }
    public string Token { get; set; }
    public JsonElement DisplayClaims { get; set; }
}