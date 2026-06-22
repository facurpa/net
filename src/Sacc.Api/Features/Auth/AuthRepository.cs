using Dapper;
using Sacc.Api.Core;
using Sacc.Api.Core.Db;
using Sacc.Api.Features.Usuarios;

namespace Sacc.Api.Features.Auth;

/// <summary>Acesso a <c>usuarios</c> (para auth), <c>auth_eventos</c> e <c>refresh_tokens</c>.</summary>
public sealed class AuthRepository(IDbConnectionFactory factory)
{
    public async Task<Usuario?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Usuario>(
            "SELECT * FROM usuarios WHERE id = @id", new { id });
    }

    public async Task<Usuario?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Usuario>(
            "SELECT * FROM usuarios WHERE email = @email", new { email });
    }

    /// <summary>Registra falha de login: grava falhas_login e (opcional) bloqueado_ate.</summary>
    public async Task AtualizarFalhasAsync(Guid id, int falhas, DateTime? bloqueadoAte, CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        await conn.ExecuteAsync(
            @"UPDATE usuarios
                 SET falhas_login = @falhas,
                     bloqueado_ate = @bloqueadoAte,
                     atualizado_em = @agora
               WHERE id = @id",
            new { id, falhas, bloqueadoAte, agora = Clock.UtcNowNaive() });
    }

    /// <summary>Login bem-sucedido: zera falhas, limpa lockout, marca último login.</summary>
    public async Task RegistrarLoginSucessoAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        var agora = Clock.UtcNowNaive();
        await conn.ExecuteAsync(
            @"UPDATE usuarios
                 SET falhas_login = 0,
                     bloqueado_ate = NULL,
                     ultimo_login_em = @agora,
                     atualizado_em = @agora
               WHERE id = @id",
            new { id, agora });
    }

    /// <summary>Regrava o hash (rehash) sem alterar demais campos.</summary>
    public async Task AtualizarHashAsync(Guid id, string passwordHash, CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        await conn.ExecuteAsync(
            "UPDATE usuarios SET password_hash = @passwordHash, atualizado_em = @agora WHERE id = @id",
            new { id, passwordHash, agora = Clock.UtcNowNaive() });
    }

    /// <summary>Troca de senha: novo hash, must_change_password, zera falhas/lockout.</summary>
    public async Task AlterarSenhaAsync(Guid id, string passwordHash, bool mustChangePassword, CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        await conn.ExecuteAsync(
            @"UPDATE usuarios
                 SET password_hash = @passwordHash,
                     must_change_password = @mustChangePassword,
                     falhas_login = 0,
                     bloqueado_ate = NULL,
                     atualizado_em = @agora
               WHERE id = @id",
            new { id, passwordHash, mustChangePassword, agora = Clock.UtcNowNaive() });
    }

    public async Task InserirEventoAsync(AuthEvento ev, CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        await conn.ExecuteAsync(
            @"INSERT INTO auth_eventos (id, tipo, usuario_id, email_tentado, ip_origem, user_agent, detalhes, criado_em)
              VALUES (@id, @tipo, @usuarioId, @emailTentado, @ipOrigem, @userAgent, @detalhes, @criadoEm)",
            new
            {
                id = Guid.NewGuid(),
                tipo = ev.Tipo,
                usuarioId = ev.UsuarioId,
                emailTentado = ev.EmailTentado,
                ipOrigem = ev.IpOrigem,
                userAgent = ev.UserAgent,
                detalhes = ev.Detalhes,
                criadoEm = Clock.UtcNowNaive(),
            });
    }

    public async Task InserirRefreshTokenAsync(string jti, Guid usuarioId, DateTime expiresAtNaive, CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        await conn.ExecuteAsync(
            "INSERT INTO refresh_tokens (jti, usuario_id, expires_at) VALUES (@jti, @usuarioId, @expiresAt)",
            new { jti, usuarioId, expiresAt = expiresAtNaive });
    }

    public async Task RevogarRefreshTokenAsync(string jti, CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        await conn.ExecuteAsync(
            "UPDATE refresh_tokens SET revogado_em = @agora WHERE jti = @jti AND revogado_em IS NULL",
            new { jti, agora = Clock.UtcNowNaive() });
    }

    /// <summary>true se o refresh é inexistente, revogado ou expirado.</summary>
    public async Task<bool> RefreshTokenEstaRevogadoAsync(string jti, CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<(DateTime expires_at, DateTime? revogado_em)?>(
            "SELECT expires_at, revogado_em FROM refresh_tokens WHERE jti = @jti", new { jti });
        if (row is null) return true;
        if (row.Value.revogado_em is not null) return true;
        return row.Value.expires_at <= Clock.UtcNowNaive();
    }

    public async Task<int> ContarAdminsAtivosAsync(CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM usuarios WHERE role = 'admin' AND ativo = true");
    }

    public async Task<string[]> EmailsAdminsAtivosAsync(CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        var emails = await conn.QueryAsync<string>(
            "SELECT email FROM usuarios WHERE role = 'admin' AND ativo = true");
        return emails.ToArray();
    }

    public async Task InserirUsuarioAsync(Usuario u, CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        await conn.ExecuteAsync(
            @"INSERT INTO usuarios
                (id, email, nome_completo, password_hash, role, ativo, must_change_password,
                 falhas_login, criado_em, criado_por, atualizado_em)
              VALUES
                (@Id, @Email, @NomeCompleto, @PasswordHash, @Role, @Ativo, @MustChangePassword,
                 0, @CriadoEm, @CriadoPor, @AtualizadoEm)",
            u);
    }
}
