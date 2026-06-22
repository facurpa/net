using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sacc.Api.Core;
using Sacc.Api.Core.Errors;
using Sacc.Api.Core.RateLimiting;
using Sacc.Api.Core.Security;
using Sacc.Api.Features.Usuarios;

namespace Sacc.Api.Features.Auth;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    AuthRepository repo,
    JwtService jwt,
    PasswordHasher hasher,
    CurrentUserAccessor current,
    AppSettings settings) : ControllerBase
{
    /// <summary>Piso de tempo de resposta do login (mitigação de timing).</summary>
    private const double MinResponseSeconds = 0.2;

    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();
    private string? UserAgent => Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<LoginTokenResponse> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var usuario = await repo.GetByEmailAsync(req.Email, ct);

        async Task<LoginTokenResponse> FalhaAsync(Guid? usuarioId)
        {
            await repo.InserirEventoAsync(new AuthEvento
            {
                Tipo = AuthEventos.LoginFalha,
                UsuarioId = usuarioId,
                EmailTentado = req.Email,
                IpOrigem = Ip,
                UserAgent = UserAgent,
            }, ct);
            await FloorAsync(sw);
            throw new UnauthorizedException("Credenciais inválidas");
        }

        if (usuario is null || !usuario.Ativo)
            return await FalhaAsync(usuario?.Id);

        if (usuario.BloqueadoAte is { } ate && ate > Core.Clock.UtcNowNaive())
            return await FalhaAsync(usuario.Id);

        if (!hasher.Verify(usuario.PasswordHash, req.Senha))
        {
            var falhas = usuario.FalhasLogin + 1;
            DateTime? bloqueado = null;
            if (falhas >= settings.MaxLoginAttempts)
            {
                bloqueado = Core.Clock.ToNaiveUtc(DateTime.UtcNow.AddMinutes(settings.LockoutDurationMinutes));
                await repo.InserirEventoAsync(new AuthEvento
                {
                    Tipo = AuthEventos.ContaBloqueada,
                    UsuarioId = usuario.Id,
                    EmailTentado = req.Email,
                    IpOrigem = Ip,
                    UserAgent = UserAgent,
                }, ct);
            }
            await repo.AtualizarFalhasAsync(usuario.Id, falhas, bloqueado, ct);
            return await FalhaAsync(usuario.Id);
        }

        // Sucesso.
        await repo.RegistrarLoginSucessoAsync(usuario.Id, ct);
        if (hasher.NeedsRehash(usuario.PasswordHash))
            await repo.AtualizarHashAsync(usuario.Id, hasher.Hash(req.Senha), ct);

        await repo.InserirEventoAsync(new AuthEvento
        {
            Tipo = AuthEventos.LoginSucesso,
            UsuarioId = usuario.Id,
            EmailTentado = req.Email,
            IpOrigem = Ip,
            UserAgent = UserAgent,
        }, ct);

        var user = new UserInfo(usuario.Id, usuario.Email, usuario.NomeCompleto, usuario.Role);

        if (usuario.MustChangePassword)
        {
            var restrito = jwt.CreateAccessToken(usuario.Id, usuario.Email, usuario.Role, usuario.NomeCompleto, restricted: true);
            await FloorAsync(sw);
            return new LoginTokenResponse
            {
                AccessToken = restrito,
                RefreshToken = null,
                RequirePasswordChange = true,
                User = user,
            };
        }

        var access = jwt.CreateAccessToken(usuario.Id, usuario.Email, usuario.Role, usuario.NomeCompleto);
        var refresh = jwt.CreateRefreshToken(usuario.Id);
        await repo.InserirRefreshTokenAsync(refresh.Jti, usuario.Id, refresh.ExpiresAtUtcNaive, ct);

        await FloorAsync(sw);
        return new LoginTokenResponse
        {
            AccessToken = access,
            RefreshToken = refresh.Token,
            RequirePasswordChange = false,
            User = user,
        };
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Refresh)]
    public async Task<RefreshTokenResponse> Refresh([FromBody] RefreshRequest req, CancellationToken ct)
    {
        var payload = jwt.DecodeRefresh(req.RefreshToken);
        if (await repo.RefreshTokenEstaRevogadoAsync(payload.Jti, ct))
            throw new UnauthorizedException();

        if (!Guid.TryParse(payload.Sub, out var id))
            throw new UnauthorizedException();

        var usuario = await repo.GetByIdAsync(id, ct);
        if (usuario is null || !usuario.Ativo
            || (usuario.BloqueadoAte is { } ate && ate > Core.Clock.UtcNowNaive()))
            throw new UnauthorizedException();

        await repo.RevogarRefreshTokenAsync(payload.Jti, ct);

        var access = jwt.CreateAccessToken(usuario.Id, usuario.Email, usuario.Role, usuario.NomeCompleto);
        var refresh = jwt.CreateRefreshToken(usuario.Id);
        await repo.InserirRefreshTokenAsync(refresh.Jti, usuario.Id, refresh.ExpiresAtUtcNaive, ct);

        return new RefreshTokenResponse(access, refresh.Token);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<OkResponse> Logout([FromBody] LogoutRequest? req, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(req?.RefreshToken))
        {
            try
            {
                var payload = jwt.DecodeRefresh(req.RefreshToken);
                await repo.RevogarRefreshTokenAsync(payload.Jti, ct);
            }
            catch
            {
                // best-effort: logout não falha por refresh inválido.
            }
        }
        return new OkResponse();
    }

    [HttpGet("me")]
    [Authorize]
    public MeResponse Me()
    {
        var u = current.User;
        return new MeResponse(u.Oid, u.Email, u.DisplayName, u.Role);
    }

    [HttpPost("change-password")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.ChangePassword)]
    public async Task<OkResponse> ChangePassword([FromBody] ChangePasswordRequest req, CancellationToken ct)
    {
        hasher.ValidarSenhaPolitica(req.NovaSenha);

        var u = current.User;
        var usuario = await repo.GetByIdAsync(u.Oid, ct);
        if (usuario is null)
            throw new UnauthorizedException();

        if (!hasher.Verify(usuario.PasswordHash, req.SenhaAtual))
            throw new UnauthorizedException("Senha atual incorreta");

        await repo.AlterarSenhaAsync(usuario.Id, hasher.Hash(req.NovaSenha), mustChangePassword: false, ct);
        await repo.InserirEventoAsync(new AuthEvento
        {
            Tipo = AuthEventos.SenhaAlterada,
            UsuarioId = usuario.Id,
            EmailTentado = usuario.Email,
            IpOrigem = Ip,
            UserAgent = UserAgent,
            Detalhes = new Dictionary<string, object?> { ["forcado"] = false },
        }, ct);

        return new OkResponse();
    }

    private static async Task FloorAsync(Stopwatch sw)
    {
        var remaining = MinResponseSeconds - sw.Elapsed.TotalSeconds;
        if (remaining > 0)
            await Task.Delay(TimeSpan.FromSeconds(remaining));
    }
}
