using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;

namespace ProjBobcat.Class.Helper;

public static class JwtTokenHelper
{
    public static Dictionary<string, string> GetTokenInfo(string token)
    {
        var result = new Dictionary<string, string>();

        var handler = new JwtSecurityTokenHandler();
        var jwtSecurityToken = handler.ReadJwtToken(token);
        var claims = jwtSecurityToken.Claims.ToList();

        foreach (var claim in claims) result.Add(claim.Type, claim.Value);

        return result;
    }
}