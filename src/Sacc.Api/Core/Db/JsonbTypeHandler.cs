using System.Data;
using System.Text.Json;
using Dapper;
using Npgsql;
using NpgsqlTypes;

namespace Sacc.Api.Core.Db;

/// <summary>
/// Dapper type handler para colunas <c>jsonb</c>. Serializa/desserializa com System.Text.Json
/// e marca o parâmetro como <see cref="NpgsqlDbType.Jsonb"/> (nunca grava como <c>text</c>).
/// </summary>
public sealed class JsonbTypeHandler<T> : SqlMapper.TypeHandler<T>
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public override T? Parse(object value)
    {
        if (value is null or DBNull) return default;
        var json = value as string ?? value.ToString()!;
        return JsonSerializer.Deserialize<T>(json, Options);
    }

    public override void SetValue(IDbDataParameter parameter, T? value)
    {
        if (parameter is NpgsqlParameter npg)
            npg.NpgsqlDbType = NpgsqlDbType.Jsonb;

        parameter.Value = value is null
            ? DBNull.Value
            : JsonSerializer.Serialize(value, Options);
    }
}
