using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using ProjBobcat.Class.Model.JsonContexts;

namespace ProjBobcat.Class.Helper;

public static class JwtTokenHelper
{
    static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var jsonBytes = Convert.FromBase64String(ReformatBase64String(payload));
        var keyValuePairs = JsonSerializer.Deserialize(jsonBytes, JsonElementContext.Default.JsonElement);

        return keyValuePairs.EnumerateObject().Select(p => new Claim(p.Name, p.Value.ToString()));
    }

    static string ReformatBase64String(string str)
    {
        str = str.Replace('_', '/').Replace('-', '+');
        switch (str.Length % 4)
        {
            case 2:
                str += "==";
                break;
            case 3:
                str += "=";
                break;
        }

        return str;
    }

    public static Dictionary<string, string> GetTokenInfo(string token)
    {
        var claims = ParseClaimsFromJwt(token);

        return claims.ToDictionary(claim => claim.Type, claim => claim.Value);
    }
}