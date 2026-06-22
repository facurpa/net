using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Sacc.Api.Core.Errors;

namespace Sacc.Api.Core.Security;

public sealed record AccessTokenPayload(string Sub, string Email, string Role, string DisplayName, string Jti, string? Scope);

public sealed record RefreshTokenPayload(string Sub, string Jti, DateTime ExpiresAtUtcNaive);

public sealed record NewRefreshToken(string Token, string Jti, DateTime ExpiresAtUtcNaive);

/// <summary>
/// Emissão/decodificação de JWT HS256, com claims idênticas à versão Python para interoperabilidade de token.
/// Access: { sub, email, role, display_name, type:"access", jti, iat, exp } (+ scope no fluxo restrito).
/// Refresh: { sub, type:"refresh", jti, iat, exp }.
/// </summary>
public sealed class JwtService
{
    public const string PasswordChangeOnlyScope = "password_change_only";
    private const int RestrictedAccessMinutes = 15;

    private readonly AppSettings _settings;
    private readonly SigningCredentials _creds;
    private readonly TokenValidationParameters _validationParameters;
    private readonly JwtSecurityTokenHandler _handler = new();

    static JwtService()
    {
        // Mantém as claims com os nomes curtos (sub, email, ...), sem mapeamento para URIs longas.
        JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
    }

    public JwtService(AppSettings settings)
    {
        _settings = settings;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SecretKey));
        _creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        _validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = "sub",
            RoleClaimType = "role",
        };
        _handler.MapInboundClaims = false;
    }

    public TokenValidationParameters ValidationParameters => _validationParameters;

    /// <summary>Access token. <paramref name="restricted"/> ⇒ validade 15 min + scope=password_change_only.</summary>
    public string CreateAccessToken(Guid id, string email, string role, string displayName, bool restricted = false)
    {
        var now = DateTimeOffset.UtcNow;
        var minutes = restricted ? RestrictedAccessMinutes : _settings.AccessTokenExpireMinutes;
        var exp = now.AddMinutes(minutes);

        var payload = new JwtPayload
        {
            ["sub"] = id.ToString(),
            ["email"] = email,
            ["role"] = role,
            ["display_name"] = displayName,
            ["type"] = "access",
            ["jti"] = Guid.NewGuid().ToString(),
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = exp.ToUnixTimeSeconds(),
        };
        if (restricted)
            payload["scope"] = PasswordChangeOnlyScope;

        return _handler.WriteToken(new JwtSecurityToken(new JwtHeader(_creds), payload));
    }

    /// <summary>Refresh token + dados para persistir em <c>refresh_tokens</c> (expires_at em UTC naive).</summary>
    public NewRefreshToken CreateRefreshToken(Guid id)
    {
        var now = DateTimeOffset.UtcNow;
        var exp = now.AddDays(_settings.RefreshTokenExpireDays);
        var jti = Guid.NewGuid().ToString();

        var payload = new JwtPayload
        {
            ["sub"] = id.ToString(),
            ["type"] = "refresh",
            ["jti"] = jti,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = exp.ToUnixTimeSeconds(),
        };
        var token = _handler.WriteToken(new JwtSecurityToken(new JwtHeader(_creds), payload));
        return new NewRefreshToken(token, jti, Core.Clock.ToNaiveUtc(exp.UtcDateTime));
    }

    public AccessTokenPayload DecodeAccess(string token)
    {
        var principal = Validate(token, "access");
        return new AccessTokenPayload(
            Sub: principal.FindFirst("sub")?.Value ?? "",
            Email: principal.FindFirst("email")?.Value ?? "",
            Role: principal.FindFirst("role")?.Value ?? "",
            DisplayName: principal.FindFirst("display_name")?.Value ?? "",
            Jti: principal.FindFirst("jti")?.Value ?? "",
            Scope: principal.FindFirst("scope")?.Value);
    }

    public RefreshTokenPayload DecodeRefresh(string token)
    {
        var principal = Validate(token, "refresh");
        var exp = principal.FindFirst("exp")?.Value;
        var expUtc = exp is not null && long.TryParse(exp, out var s)
            ? DateTimeOffset.FromUnixTimeSeconds(s).UtcDateTime
            : DateTime.UtcNow;
        return new RefreshTokenPayload(
            Sub: principal.FindFirst("sub")?.Value ?? "",
            Jti: principal.FindFirst("jti")?.Value ?? "",
            ExpiresAtUtcNaive: Core.Clock.ToNaiveUtc(expUtc));
    }

    /// <summary>Valida assinatura+expiração e o claim <c>type</c>. Qualquer falha ⇒ 401 "Não autenticado".</summary>
    private ClaimsPrincipal Validate(string token, string expectedType)
    {
        ClaimsPrincipal principal;
        try
        {
            principal = _handler.ValidateToken(token, _validationParameters, out _);
        }
        catch
        {
            throw new UnauthorizedException();
        }
        if (principal.FindFirst("type")?.Value != expectedType)
            throw new UnauthorizedException();
        return principal;
    }
}
