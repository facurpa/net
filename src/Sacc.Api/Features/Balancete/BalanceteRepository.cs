using Dapper;
using Sacc.Api.Core;
using Sacc.Api.Core.Db;

namespace Sacc.Api.Features.Balancete;

/// <summary>Leitura read-only da view contábil do ERP (SQL Server). Nunca escreve.</summary>
public sealed class BalanceteRepository(IDbConnectionFactory factory, AppSettings settings)
{
    private const string View = "[db_integ_intranet].[sacc].[vw_saldos_contabeis]";

    public async Task<IReadOnlyList<SaldoContabil>> BuscarAsync(int empresaCodigo, int dataInicio, int dataFim, CancellationToken ct = default)
    {
        using var conn = await factory.OpenErpAsync(ct);
        var rows = await conn.QueryAsync<SaldoContabil>(new CommandDefinition(
            $@"SELECT DATA, CONTA, DESCRICAO, DEBITO, CREDITO, EMPRESA
                 FROM {View}
                WHERE EMPRESA = @empresa
                  AND DATA BETWEEN @inicio AND @fim
                ORDER BY CONTA",
            new { empresa = empresaCodigo, inicio = dataInicio, fim = dataFim },
            commandTimeout: settings.ErpQueryTimeout,
            cancellationToken: ct));
        return rows.AsList();
    }
}
