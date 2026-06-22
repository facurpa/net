namespace Sacc.Api.Features.Periodos;

/// <summary>Entidade da tabela <c>periodos_verificacao</c>. Datas em inteiro YYYYMMDD.</summary>
public sealed class Periodo
{
    public Guid Id { get; set; }
    public int DataInicio { get; set; }
    public int DataFim { get; set; }
    public int EmpresaCodigo { get; set; }
    public int Versao { get; set; }
    public DateTime CriadoEm { get; set; }
    public string CriadoPor { get; set; } = "";
}
