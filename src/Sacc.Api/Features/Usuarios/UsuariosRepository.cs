using Dapper;
using Sacc.Api.Core;
using Sacc.Api.Core.Db;

namespace Sacc.Api.Features.Usuarios;

public sealed class UsuariosRepository(IDbConnectionFactory factory)
{
    public async Task<(IReadOnlyList<Usuario> Items, int Total)> ListarAsync(int page, int pageSize, CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        var total = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM usuarios");
        var items = await conn.QueryAsync<Usuario>(
            @"SELECT * FROM usuarios
               ORDER BY criado_em DESC
               LIMIT @limit OFFSET @offset",
            new { limit = pageSize, offset = (page - 1) * pageSize });
        return (items.AsList(), total);
    }

    public async Task<Usuario?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Usuario>(
            "SELECT * FROM usuarios WHERE id = @id", new { id });
    }

    public async Task AtualizarAsync(Usuario u, CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        await conn.ExecuteAsync(
            @"UPDATE usuarios
                 SET nome_completo = @NomeCompleto,
                     role = @Role,
                     ativo = @Ativo,
                     atualizado_em = @atualizadoEm
               WHERE id = @Id",
            new { u.Id, u.NomeCompleto, u.Role, u.Ativo, atualizadoEm = Clock.UtcNowNaive() });
    }

    public async Task ResetSenhaAsync(Guid id, string passwordHash, CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        await conn.ExecuteAsync(
            @"UPDATE usuarios
                 SET password_hash = @passwordHash,
                     must_change_password = true,
                     falhas_login = 0,
                     bloqueado_ate = NULL,
                     atualizado_em = @agora
               WHERE id = @id",
            new { id, passwordHash, agora = Clock.UtcNowNaive() });
    }
}
