namespace Sacc.Api.Features.EmailTemplates;

/// <summary>Entidade da tabela <c>email_templates</c>.</summary>
public sealed class EmailTemplate
{
    public Guid Id { get; set; }
    public string CorpoHtml { get; set; } = "";
    public int Versao { get; set; }
    public DateTime CriadoEm { get; set; }
    public string CriadoPor { get; set; } = "";
}
