using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Sacc.Api.Features.Auth;

namespace Sacc.Api.Core.Security;

public static class AuthenticationSetup
{
    public static IServiceCollection AddSaccAuthentication(this IServiceCollection services, JwtService jwt)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = jwt.ValidationParameters;
                options.MapInboundClaims = false;
                options.Events = new JwtBearerEvents
                {
                    // Após validar assinatura/expiração: exige type=access e carrega/valida o usuário no banco.
                    OnTokenValidated = async ctx =>
                    {
                        var principal = ctx.Principal!;
                        if (principal.FindFirst("type")?.Value != "access")
                        {
                            ctx.Fail("tipo de token inválido");
                            return;
                        }

                        var subRaw = principal.FindFirst("sub")?.Value;
                        if (!Guid.TryParse(subRaw, out var id))
                        {
                            ctx.Fail("sub inválido");
                            return;
                        }

                        var repo = ctx.HttpContext.RequestServices.GetRequiredService<AuthRepository>();
                        var usuario = await repo.GetByIdAsync(id, ctx.HttpContext.RequestAborted);
                        if (usuario is null || !usuario.Ativo)
                        {
                            ctx.Fail("usuário inativo ou inexistente");
                            return;
                        }
                        if (usuario.BloqueadoAte is { } ate && ate > Clock.UtcNowNaive())
                        {
                            ctx.Fail("usuário bloqueado");
                            return;
                        }

                        ctx.HttpContext.Items[CurrentUser.HttpContextItemKey] = new CurrentUser
                        {
                            Oid = usuario.Id,
                            Email = usuario.Email,
                            DisplayName = usuario.NomeCompleto,
                            Role = usuario.Role,
                            Scope = principal.FindFirst("scope")?.Value,
                        };
                    },
                    OnChallenge = async ctx =>
                    {
                        ctx.HandleResponse();
                        await WriteDetailAsync(ctx.Response, 401, "Não autenticado", bearer: true);
                    },
                    OnForbidden = ctx => WriteDetailAsync(ctx.Response, 403, "Permissão insuficiente"),
                };
            });
        return services;
    }

    private static async Task WriteDetailAsync(HttpResponse response, int status, string detail, bool bearer = false)
    {
        if (response.HasStarted) return;
        response.StatusCode = status;
        response.ContentType = "application/json; charset=utf-8";
        if (bearer) response.Headers["WWW-Authenticate"] = "Bearer";
        await response.WriteAsync(JsonSerializer.Serialize(new { detail }));
    }
}
