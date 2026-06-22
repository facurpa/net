using Dapper;
using Sacc.Api.Core.Db;

namespace Sacc.Api.Features.Periodos;

public sealed class PeriodosRepository(IDbConnectionFactory factory)
{
    public async Task<IReadOnlyList<Periodo>> ListarAsync(CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        var rows = await conn.QueryAsync<Periodo>("SELECT * FROM periodos_verificacao ORDER BY versao DESC");
        return rows.AsList();
    }

    /// <summary>Período ativo = maior versão.</summary>
    public async Task<Periodo?> GetAtivoAsync(CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Periodo>(
            "SELECT * FROM periodos_verificacao ORDER BY versao DESC LIMIT 1");
    }

    /// <summary>Cria com versão = MAX(versao)+1 (replica a race condition existente).</summary>
    public async Task<Periodo> CriarAsync(int dataInicio, int dataFim, int empresaCodigo, string criadoPor, DateTime criadoEm, CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        var proximaVersao = await conn.ExecuteScalarAsync<int>(
            "SELECT COALESCE(MAX(versao), 0) + 1 FROM periodos_verificacao");

        var entity = new Periodo
        {
            Id = Guid.NewGuid(),
            DataInicio = dataInicio,
            DataFim = dataFim,
            EmpresaCodigo = empresaCodigo,
            Versao = proximaVersao,
            CriadoEm = criadoEm,
            CriadoPor = criadoPor,
        };
        await conn.ExecuteAsync(
            @"INSERT INTO periodos_verificacao (id, data_inicio, data_fim, empresa_codigo, versao, criado_em, criado_por)
              VALUES (@Id, @DataInicio, @DataFim, @EmpresaCodigo, @Versao, @CriadoEm, @CriadoPor)", entity);
        return entity;
    }
}
