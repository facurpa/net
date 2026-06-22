namespace Sacc.Api.Features.Balancete;

/// <summary>
/// Linha da view do ERP <c>[db_integ_intranet].[sacc].[vw_saldos_contabeis]</c>.
/// Mapeamento Dapper case-insensitive: DATA→Data, CONTA→Conta, ... EMPRESA→Empresa.
/// </summary>
public sealed class SaldoContabil
{
    public int Data { get; set; }
    public string Conta { get; set; } = "";
    public string Descricao { get; set; } = "";
    public decimal? Debito { get; set; }
    public decimal? Credito { get; set; }
    public int Empresa { get; set; }
}
