using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StarterApp.Database.Models;

namespace StarterApp.Api;

// File purpose:
// Creates signed JWT access tokens and secure random refresh tokens.
// Also provides deterministic hashing for refresh-token storage/lookup.
public interface ITokenService
{
    TokenResponse CreateTokenPair(User user, IReadOnlyCollection<string> roles);
    string HashRefreshToken(string rawToken);
}

public sealed class TokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly SigningCredentials _signingCredentials;

    public TokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;

        if (string.IsNullOrWhiteSpace(_options.SigningKey) || _options.SigningKey.Length < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be configured and at least 32 characters long.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public TokenResponse CreateTokenPair(User user, IReadOnlyCollection<string> roles)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.GivenName, user.FirstName),
            new(JwtRegisteredClaimNames.FamilyName, user.LastName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        // Role claims drive authorization checks in API endpoints and client UI role checks.
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var jwt = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: _signingCredentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(jwt);

        // 64 random bytes provide a high-entropy refresh token value.
        Span<byte> buffer = stackalloc byte[64];
        RandomNumberGenerator.Fill(buffer);
        var refreshToken = Convert.ToBase64String(buffer);

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAtUtc = expires
        };
    }

    public string HashRefreshToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }
}
