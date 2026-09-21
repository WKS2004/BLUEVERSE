using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Blueverse.Auth.Models;

namespace Blueverse.Auth.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;
    private readonly byte[] _key;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _accessTokenMinutes;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;

        var secret = _configuration["JWT_SIGNING_KEY"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException("JWT_SIGNING_KEY must be configured.");
        }

        _key = Encoding.UTF8.GetBytes(secret);
        if (_key.Length < 32)
        {
            throw new InvalidOperationException("JWT_SIGNING_KEY must be at least 32 UTF-8 bytes.");
        }

        _issuer = _configuration["Jwt:Issuer"] ?? "Blueverse.Auth";
        _audience = _configuration["Jwt:Audience"] ?? "Blueverse.Client";
        var configuredAccessTokenMinutes = _configuration["Jwt:AccessTokenMinutes"];
        if (string.IsNullOrWhiteSpace(configuredAccessTokenMinutes))
        {
            _accessTokenMinutes = 15;
        }
        else if (!int.TryParse(configuredAccessTokenMinutes, out _accessTokenMinutes) ||
                 _accessTokenMinutes is < 1 or > 60)
        {
            throw new InvalidOperationException("Jwt:AccessTokenMinutes must be between 1 and 60.");
        }
    }

    public (string Token, DateTime ExpiresAt) GenerateToken(
        User user,
        Guid sessionId,
        int sessionVersion,
        IEnumerable<string> roles,
        IEnumerable<string> permissions)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Name, user.FullName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("token_version", user.TokenVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new Claim("session_id", sessionId.ToString()),
            new Claim("session_version", sessionVersion.ToString(System.Globalization.CultureInfo.InvariantCulture))
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
            claims.Add(new Claim("role", role));
        }

        foreach (var permission in permissions)
        {
            claims.Add(new Claim("permission", permission));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_accessTokenMinutes),
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(_key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return (tokenHandler.WriteToken(token), tokenDescriptor.Expires!.Value);
    }
}
