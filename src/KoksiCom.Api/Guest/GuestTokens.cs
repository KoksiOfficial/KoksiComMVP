using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace KoksiCom.Api.Guest;

public sealed class GuestTokens
{
    public const int LifetimeHours = 12;
    public const string Issuer = "KoksiCom";
    public const string Audience = "KoksiCom";
    public const string NameClaim = "name";

    private readonly JwtSecurityTokenHandler _handler = new() { MapInboundClaims = false };
    private readonly SigningCredentials _credentials;

    public GuestTokens()
        : this(RandomNumberGenerator.GetBytes(32))
    {
    }

    public GuestTokens(byte[] keyBytes)
    {
        var key = new SymmetricSecurityKey(keyBytes);
        _credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        ValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = NameClaim
        };
    }

    public TokenValidationParameters ValidationParameters { get; }

    public string Issue(string name, DateTimeOffset now)
    {
        var expires = now.AddHours(LifetimeHours);
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: [new Claim(NameClaim, name)],
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: _credentials);

        return _handler.WriteToken(token);
    }

    public bool TryReadName(string token, DateTimeOffset now, out string name)
    {
        name = "";
        try
        {
            var parameters = ValidationParameters.Clone();
            parameters.LifetimeValidator = (_, expires, _, _) =>
                expires is not null && expires.Value > now.UtcDateTime;

            var principal = _handler.ValidateToken(token, parameters, out _);
            var value = principal.FindFirst(NameClaim)?.Value;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            name = value;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
