namespace Sacc.Api.Features.Balancete;

public sealed record SaldoContabilRow(
    int Data,
    string Conta,
    string Descricao,
    decimal? Debito,
    decimal? Credito,
    int EmpresaCodigo);

public sealed record BalanceteResponse(IReadOnlyList<SaldoContabilRow> Items, int Total);
