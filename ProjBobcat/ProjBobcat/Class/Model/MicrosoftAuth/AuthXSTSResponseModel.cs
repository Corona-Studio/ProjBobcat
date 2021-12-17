using System;
using System.Collections.Generic;

namespace ProjBobcat.Class.Model.MicrosoftAuth;

public class AuthXSTSResponseModel
{
    public DateTime IssueInstant { get; set; }
    public DateTime NotAfter { get; set; }
    public string Token { get; set; }
    public Dictionary<string, List<Dictionary<string, string>>> DisplayClaims { get; set; }
}