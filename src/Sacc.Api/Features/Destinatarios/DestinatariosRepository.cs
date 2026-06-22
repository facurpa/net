using Dapper;
using Sacc.Api.Core.Db;

namespace Sacc.Api.Features.Destinatarios;

public sealed class DestinatariosRepository(IDbConnectionFactory factory)
{
    public async Task<IReadOnlyList<Destinatario>> ListarAsync(CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        var rows = await conn.QueryAsync<Destinatario>("SELECT * FROM destinatarios ORDER BY nome");
        return rows.AsList();
    }

    public async Task<IReadOnlyList<string>> EmailsAtivosAsync(CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        var rows = await conn.QueryAsync<string>("SELECT email FROM destinatarios WHERE ativo = true ORDER BY nome");
        return rows.AsList();
    }

    public async Task<Destinatario?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Destinatario>(
            "SELECT * FROM destinatarios WHERE id = @id", new { id });
    }

    public async Task CriarAsync(Destinatario d, CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        await conn.ExecuteAsync(
            @"INSERT INTO destinatarios (id, nome, email, ativo, criado_em, atualizado_em)
              VALUES (@Id, @Nome, @Email, @Ativo, @CriadoEm, @AtualizadoEm)", d);
    }

    public async Task AtualizarAsync(Destinatario d, CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        await conn.ExecuteAsync(
            @"UPDATE destinatarios
                 SET nome = @Nome, email = @Email, ativo = @Ativo, atualizado_em = @AtualizadoEm
               WHERE id = @Id", d);
    }

    public async Task<bool> ExcluirAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        var n = await conn.ExecuteAsync("DELETE FROM destinatarios WHERE id = @id", new { id });
        return n > 0;
    }
}
