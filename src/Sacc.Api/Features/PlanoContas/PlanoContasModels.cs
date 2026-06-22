namespace Sacc.Api.Features.PlanoContas;

/// <summary>Entidade da tabela <c>plano_contas</c> (timestamps com tz / UTC).</summary>
public sealed class PlanoConta
{
    public Guid Id { get; set; }
    public string Conta { get; set; } = "";
    public string Descricao { get; set; } = "";
    public string Natureza { get; set; } = "";
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }
    public string? AtualizadoPor { get; set; }
}
