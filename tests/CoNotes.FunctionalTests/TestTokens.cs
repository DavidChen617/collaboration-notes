using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CoNotes.FunctionalTests;
using Microsoft.IdentityModel.Tokens;

namespace FunctionalTests;

internal static class TestTokens
{
    public static string CreateToken(
        string keycloakSub,
        SecurityKey? signingKey = null,
        DateTime? expires = null,
        IEnumerable<Claim>? extraClaims = null)
    {
        var credentials = new SigningCredentials(
            signingKey ?? FunctionalTestWebAppFactory.SigningKey,
            SecurityAlgorithms.HmacSha256);

        Claim[] claims = [new Claim("sub", keycloakSub), .. extraClaims ?? []];
        var token = new JwtSecurityToken(
            issuer: FunctionalTestWebAppFactory.TestIssuer,
            audience: FunctionalTestWebAppFactory.TestAudience,
            claims: claims,
            expires: expires ?? DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
