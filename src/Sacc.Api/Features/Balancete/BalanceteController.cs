using Microsoft.AspNetCore.Mvc;
using Sacc.Api.Core;
using Sacc.Api.Core.Errors;
using Sacc.Api.Core.Security;
using Sacc.Api.Core.Validation;

namespace Sacc.Api.Features.Balancete;

// TODO(security): smoke test temporário público (Seção 13.3). O guard de boot aborta em prod.
[ApiController]
[Route("api/balancete")]
public sealed class BalanceteController(
    BalanceteRepository repo,
    AppSettings settings,
    CurrentUserAccessor current) : ControllerBase
{
    [HttpGet("")]
    public async Task<BalanceteResponse> Consultar(
        [FromQuery(Name = "empresa_codigo")] int empresaCodigo,
        [FromQuery(Name = "data_inicio")] int dataInicio,
        [FromQuery(Name = "data_fim")] int dataFim,
        CancellationToken ct)
    {
        // Quando o flag de público está desligado, exige autenticação (lança 401 se ausente).
        if (!settings.BalancetePublic)
            _ = current.User;

        if (!DateYmd.TryParse(dataInicio, out _))
            throw new BadRequestException("data_inicio deve ser uma data válida no formato YYYYMMDD");
        if (!DateYmd.TryParse(dataFim, out _))
            throw new BadRequestException("data_fim deve ser uma data válida no formato YYYYMMDD");
        if (dataFim < dataInicio)
            throw new BadRequestException("data_fim deve ser maior ou igual a data_inicio");

        IReadOnlyList<SaldoContabil> rows;
        try
        {
            rows = await repo.BuscarAsync(empresaCodigo, dataInicio, dataFim, ct);
        }
        catch (Exception ex)
        {
            throw new BadGatewayException($"Falha ao consultar ERP: {ex.Message}");
        }

        var items = rows
            .Select(r => new SaldoContabilRow(r.Data, r.Conta, r.Descricao, r.Debito, r.Credito, r.Empresa))
            .ToList();
        return new BalanceteResponse(items, items.Count);
    }
}
