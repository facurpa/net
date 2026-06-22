using Dapper;
using Sacc.Api.Core;
using Sacc.Api.Core.Db;

namespace Sacc.Api.Features.PlanoContas;

public sealed class PlanoContasRepository(IDbConnectionFactory factory)
{
    public async Task<IReadOnlyList<PlanoConta>> ListarAsync(CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        var rows = await conn.QueryAsync<PlanoConta>("SELECT * FROM plano_contas ORDER BY conta");
        return rows.AsList();
    }

    /// <summary>Plano de contas ativo como mapa <c>conta → natureza</c> (para a verificação).</summary>
    public async Task<IReadOnlyDictionary<string, string>> NaturezaPorContaAtivosAsync(CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        var rows = await conn.QueryAsync<(string conta, string natureza)>(
            "SELECT conta, natureza FROM plano_contas WHERE ativo = true");
        var dict = new Dictionary<string, string>();
        foreach (var (conta, natureza) in rows)
            dict[conta] = natureza;
        return dict;
    }

    public async Task<PlanoConta?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<PlanoConta>(
            "SELECT * FROM plano_contas WHERE id = @id", new { id });
    }

    public async Task<PlanoConta> CriarAsync(PlanoConta c, CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        await conn.ExecuteAsync(
            @"INSERT INTO plano_contas (id, conta, descricao, natureza, ativo, criado_em, atualizado_em, atualizado_por)
              VALUES (@Id, @Conta, @Descricao, @Natureza, @Ativo, @CriadoEm, @AtualizadoEm, @AtualizadoPor)", c);
        return c;
    }

    public async Task AtualizarAsync(PlanoConta c, CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        await conn.ExecuteAsync(
            @"UPDATE plano_contas
                 SET descricao = @Descricao,
                     natureza = @Natureza,
                     ativo = @Ativo,
                     atualizado_em = @AtualizadoEm,
                     atualizado_por = @AtualizadoPor
               WHERE id = @Id", c);
    }

    public async Task<bool> ExcluirAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        var n = await conn.ExecuteAsync("DELETE FROM plano_contas WHERE id = @id", new { id });
        return n > 0;
    }
}
