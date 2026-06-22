using Microsoft.AspNetCore.Mvc;
using Sacc.Api.Core;
using Sacc.Api.Core.Errors;
using Sacc.Api.Core.Validation;

namespace Sacc.Api.Features.Periodos;

// TODO(security): exigir autenticação (paridade — hoje público, ver Seção 13.1).
[ApiController]
[Route("api/periodos")]
public sealed class PeriodosController(PeriodosRepository repo) : ControllerBase
{
    [HttpGet("")]
    public async Task<IReadOnlyList<PeriodoResponse>> Listar(CancellationToken ct)
    {
        var rows = await repo.ListarAsync(ct);
        return rows.Select(Map).ToList();
    }

    [HttpGet("ativo")]
    public async Task<PeriodoResponse> Ativo(CancellationToken ct)
    {
        var p = await repo.GetAtivoAsync(ct) ?? throw new NotFoundException("Nenhum período cadastrado.");
        return Map(p);
    }

    [HttpPost("")]
    public async Task<IActionResult> Criar([FromBody] PeriodoCreate req, CancellationToken ct)
    {
        DateYmd.Require(req.DataInicio, "data_inicio");
        DateYmd.Require(req.DataFim, "data_fim");
        if (req.EmpresaCodigo <= 0)
            throw new ValidationException("empresa_codigo deve ser maior que zero");
        if (req.DataFim < req.DataInicio)
            throw new ValidationException("data_fim deve ser maior ou igual a data_inicio");

        var p = await repo.CriarAsync(req.DataInicio, req.DataFim, req.EmpresaCodigo, "system", Clock.UtcNow(), ct);
        return StatusCode(StatusCodes.Status201Created, Map(p));
    }

    private static PeriodoResponse Map(Periodo p) =>
        new(p.Id, p.DataInicio, p.DataFim, p.EmpresaCodigo, p.Versao, p.CriadoEm, p.CriadoPor);
}
