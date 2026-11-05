using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace TaskPlanner.Helpers;

public static class TokenHelper
{
    public const string Issuer = "https://planner.local/";
    public const string Audience = "https://planner.local/";
    public static readonly byte[] Key = Encoding.UTF8.GetBytes("planner-demo-key-2026-sutkt-ok!!");

    public static string CreateAccessToken(int userId, string email, string name)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Name, name),
            new Claim("typ", "access")
        };
        return Write(claims, DateTime.UtcNow.AddMinutes(15));
    }

    public static string CreateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static string Write(IEnumerable<Claim> claims, DateTime expires)
    {
        var handler = new JwtSecurityTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = Issuer,
            Audience = Audience,
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Key),
                SecurityAlgorithms.HmacSha256Signature)
        };
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}
