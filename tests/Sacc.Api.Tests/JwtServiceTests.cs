using System.IdentityModel.Tokens.Jwt;
using Sacc.Api.Core;
using Sacc.Api.Core.Errors;
using Sacc.Api.Core.Security;
using Xunit;

namespace Sacc.Api.Tests;

public sealed class JwtServiceTests
{
    private const string Secret = "IgzmX12tdiF-Ib1w_BlYnEhRlzhtcaL4Ce3vufSJtWs";

    private static JwtService Make() => new(new AppSettings
    {
        SecretKey = Secret,
        AccessTokenExpireMinutes = 30,
        RefreshTokenExpireDays = 7,
    });

    [Fact]
    public void Access_token_carrega_as_claims_esperadas()
    {
        var jwt = Make();
        var id = Guid.NewGuid();
        var token = jwt.CreateAccessToken(id, "user@cast4it.com", "admin", "Fulano de Tal");

        var payload = new JwtSecurityTokenHandler().ReadJwtToken(token).Payload;
        Assert.Equal(id.ToString(), payload["sub"]);
        Assert.Equal("user@cast4it.com", payload["email"]);
        Assert.Equal("admin", payload["role"]);
        Assert.Equal("Fulano de Tal", payload["display_name"]);
        Assert.Equal("access", payload["type"]);
        Assert.True(payload.ContainsKey("jti"));
        Assert.True(payload.ContainsKey("iat"));
        Assert.True(payload.ContainsKey("exp"));
        Assert.False(payload.ContainsKey("scope"));
        Assert.False(payload.ContainsKey("nbf"));

        var decoded = jwt.DecodeAccess(token);
        Assert.Equal(id.ToString(), decoded.Sub);
        Assert.Equal("admin", decoded.Role);
        Assert.Null(decoded.Scope);
    }

    [Fact]
    public void Access_token_restrito_inclui_scope()
    {
        var jwt = Make();
        var token = jwt.CreateAccessToken(Guid.NewGuid(), "u@x.com", "usuario", "U", restricted: true);
        var decoded = jwt.DecodeAccess(token);
        Assert.Equal("password_change_only", decoded.Scope);
    }

    [Fact]
    public void Header_e_hs256_sem_kid()
    {
        var jwt = Make();
        var token = jwt.CreateAccessToken(Guid.NewGuid(), "u@x.com", "usuario", "U");
        var header = new JwtSecurityTokenHandler().ReadJwtToken(token).Header;
        Assert.Equal("HS256", header.Alg);
        Assert.Equal("JWT", header.Typ);
        Assert.False(header.ContainsKey("kid"));
    }

    [Fact]
    public void Refresh_token_decodifica_e_rejeita_tipo_errado()
    {
        var jwt = Make();
        var id = Guid.NewGuid();
        var refresh = jwt.CreateRefreshToken(id);

        var decoded = jwt.DecodeRefresh(refresh.Token);
        Assert.Equal(id.ToString(), decoded.Sub);
        Assert.Equal(refresh.Jti, decoded.Jti);

        // Um access token não pode ser decodificado como refresh.
        var access = jwt.CreateAccessToken(id, "u@x.com", "usuario", "U");
        Assert.Throws<UnauthorizedException>(() => jwt.DecodeRefresh(access));
    }

    [Fact]
    public void Token_com_assinatura_invalida_e_rejeitado()
    {
        var jwt = Make();
        var token = jwt.CreateAccessToken(Guid.NewGuid(), "u@x.com", "usuario", "U");
        var adulterado = token[..^3] + (token[^3] == 'a' ? "bbb" : "aaa");
        Assert.Throws<UnauthorizedException>(() => jwt.DecodeAccess(adulterado));
    }
}
