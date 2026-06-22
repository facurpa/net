using System.Collections.Generic;
using Dapper;
using Sacc.Api.Features.Worker;

namespace Sacc.Api.Core.Db;

/// <summary>Configuração global do Dapper. Chamar uma vez no startup.</summary>
public static class DapperConfig
{
    private static bool _initialized;

    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;

        // snake_case (banco) ↔ PascalCase (modelos): nome_completo → NomeCompleto.
        DefaultTypeMap.MatchNamesWithUnderscores = true;

        // Colunas jsonb.
        SqlMapper.AddTypeHandler(new JsonbTypeHandler<List<ContaViradaJson>>());
        SqlMapper.AddTypeHandler(new JsonbTypeHandler<Dictionary<string, object?>>());
    }
}
