using Microsoft.AspNetCore.Mvc;
using Sacc.Api.Core;
using Sacc.Api.Core.Errors;

namespace Sacc.Api.Features.EmailTemplates;

// TODO(security): exigir autenticação (paridade — hoje público, ver Seção 13.1).
[ApiController]
[Route("api/email-templates")]
public sealed class EmailTemplatesController(EmailTemplatesRepository repo) : ControllerBase
{
    [HttpGet("")]
    public async Task<IReadOnlyList<EmailTemplateSummary>> Listar(CancellationToken ct)
    {
        var rows = await repo.ListarAsync(ct);
        return rows.Select(t => new EmailTemplateSummary(t.Id, t.Versao, t.CriadoEm, t.CriadoPor)).ToList();
    }

    [HttpGet("ativo")]
    public async Task<EmailTemplateResponse> Ativo(CancellationToken ct)
    {
        var t = await repo.GetAtivoAsync(ct) ?? throw new NotFoundException("Nenhum template cadastrado.");
        return new EmailTemplateResponse(t.Id, t.CorpoHtml, t.Versao, t.CriadoEm, t.CriadoPor);
    }

    [HttpPost("")]
    public async Task<IActionResult> Criar([FromBody] EmailTemplateCreate req, CancellationToken ct)
    {
        if ((req.CorpoHtml ?? "").Length < 10)
            throw new ValidationException("corpo_html deve ter pelo menos 10 caracteres");

        var t = await repo.CriarAsync(req.CorpoHtml, "system", Clock.UtcNow(), ct);
        return StatusCode(StatusCodes.Status201Created,
            new EmailTemplateResponse(t.Id, t.CorpoHtml, t.Versao, t.CriadoEm, t.CriadoPor));
    }
}
