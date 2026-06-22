namespace Sacc.Api.Features.Periodos;

public sealed record PeriodoCreate(int DataInicio, int DataFim, int EmpresaCodigo);

public sealed record PeriodoResponse(
    Guid Id,
    int DataInicio,
    int DataFim,
    int EmpresaCodigo,
    int Versao,
    DateTime CriadoEm,
    string CriadoPor);
