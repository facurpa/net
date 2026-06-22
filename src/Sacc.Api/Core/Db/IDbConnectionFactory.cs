using System.Data;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace Sacc.Api.Core.Db;

/// <summary>Cria conexões para o Postgres (config) e para o SQL Server do ERP (read-only).</summary>
public interface IDbConnectionFactory
{
    /// <summary>Conexão aberta com o Postgres <c>sacc_db</c>.</summary>
    Task<IDbConnection> OpenPostgresAsync(CancellationToken ct = default);

    /// <summary>Conexão aberta com a view contábil do ERP (SQL Server).</summary>
    Task<IDbConnection> OpenErpAsync(CancellationToken ct = default);
}

public sealed class DbConnectionFactory(AppSettings settings) : IDbConnectionFactory
{
    public async Task<IDbConnection> OpenPostgresAsync(CancellationToken ct = default)
    {
        var conn = new NpgsqlConnection(settings.NpgsqlConnectionString);
        await conn.OpenAsync(ct);
        return conn;
    }

    public async Task<IDbConnection> OpenErpAsync(CancellationToken ct = default)
    {
        var conn = new SqlConnection(settings.SqlServerConnectionString);
        await conn.OpenAsync(ct);
        return conn;
    }
}
