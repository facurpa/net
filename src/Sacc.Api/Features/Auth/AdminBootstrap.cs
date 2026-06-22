using System.Text.RegularExpressions;
using Sacc.Api.Core;
using Sacc.Api.Core.Security;
using Sacc.Api.Features.Usuarios;

namespace Sacc.Api.Features.Auth;

/// <summary>Bootstrap do admin inicial no startup (equivalente ao <c>lifespan</c>).</summary>
public static class AdminBootstrap
{
    // Aceita domínios internos (.local). Igual ao regex da origem.
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    public static async Task RunAsync(IServiceProvider services, AppSettings settings, ILogger logger, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(settings.InitialAdminEmail) || string.IsNullOrWhiteSpace(settings.InitialAdminPassword))
            return;

        var email = settings.InitialAdminEmail.Trim();
        if (!EmailRegex.IsMatch(email))
            throw new InvalidOperationException($"INITIAL_ADMIN_EMAIL inválido: '{email}'.");

        using var scope = services.CreateScope();
        var hasher = scope.ServiceProvider.GetRequiredService<PasswordHasher>();
        var repo = scope.ServiceProvider.GetRequiredService<AuthRepository>();

        // Validação da política — aborta o boot se a senha for fraca.
        hasher.ValidarSenhaPolitica(settings.InitialAdminPassword);

        if (await repo.ContarAdminsAtivosAsync(ct) > 0)
            return;

        var agora = Clock.UtcNowNaive();
        await repo.InserirUsuarioAsync(new Usuario
        {
            Id = Guid.NewGuid(),
            Email = email,
            NomeCompleto = settings.InitialAdminNome,
            PasswordHash = hasher.Hash(settings.InitialAdminPassword),
            Role = "admin",
            Ativo = true,
            MustChangePassword = true,
            CriadoEm = agora,
            CriadoPor = "SYSTEM_BOOTSTRAP",
            AtualizadoEm = agora,
        }, ct);

        logger.LogWarning(
            "Admin inicial criado ({Email}). REMOVA INITIAL_ADMIN_EMAIL e INITIAL_ADMIN_PASSWORD do .env após o primeiro login.",
            email);
    }
}
