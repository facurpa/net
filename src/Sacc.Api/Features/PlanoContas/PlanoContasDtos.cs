namespace Sacc.Api.Features.PlanoContas;

public sealed record PlanoContaCreate(string Conta, string Descricao, string Natureza);

public sealed record PlanoContaUpdate(string? Descricao, string? Natureza, bool? Ativo);

public sealed record PlanoContaResponse(
    Guid Id,
    string Conta,
    string Descricao,
    string Natureza,
    bool Ativo,
    DateTime AtualizadoEm,
    string? AtualizadoPor);
