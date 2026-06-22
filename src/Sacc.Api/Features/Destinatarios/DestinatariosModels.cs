namespace Sacc.Api.Features.Destinatarios;

/// <summary>Entidade da tabela <c>destinatarios</c> (timestamps com tz / UTC).</summary>
public sealed class Destinatario
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = "";
    public string Email { get; set; } = "";
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }
}
