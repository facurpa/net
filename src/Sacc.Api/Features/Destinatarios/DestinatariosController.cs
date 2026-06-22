using Microsoft.AspNetCore.Mvc;
using Sacc.Api.Core;
using Sacc.Api.Core.Db;
using Sacc.Api.Core.Errors;
using Sacc.Api.Core.Validation;

namespace Sacc.Api.Features.Destinatarios;

// TODO(security): exigir autenticação (paridade — hoje público, ver Seção 13.1).
[ApiController]
[Route("api/destinatarios")]
public sealed class DestinatariosController(DestinatariosRepository repo) : ControllerBase
{
    [HttpGet("")]
    public async Task<IReadOnlyList<DestinatarioResponse>> Listar(CancellationToken ct)
    {
        var rows = await repo.ListarAsync(ct);
        return rows.Select(Map).ToList();
    }

    [HttpPost("")]
    public async Task<IActionResult> Criar([FromBody] DestinatarioCreate req, CancellationToken ct)
    {
        var nome = (req.Nome ?? "").Trim();
        if (nome.Length is 0 or > 200)
            throw new ValidationException("nome deve ter entre 1 e 200 caracteres");
        var email = EmailValidator.Require(req.Email);

        var agora = Clock.UtcNow();
        var entity = new Destinatario
        {
            Id = Guid.NewGuid(),
            Nome = nome,
            Email = email,
            Ativo = true,
            CriadoEm = agora,
            AtualizadoEm = agora,
        };

        try
        {
            await repo.CriarAsync(entity, ct);
        }
        catch (Exception ex) when (ex.IsUniqueViolation())
        {
            throw new ConflictException($"E-mail '{email}' já cadastrado.");
        }

        return StatusCode(StatusCodes.Status201Created, Map(entity));
    }

    [HttpPut("{id:guid}")]
    public async Task<DestinatarioResponse> Atualizar(Guid id, [FromBody] DestinatarioUpdate req, CancellationToken ct)
    {
        var entity = await repo.GetByIdAsync(id, ct) ?? throw new NotFoundException("Destinatário não encontrado.");

        if (req.Nome is not null)
        {
            if (req.Nome.Trim().Length is 0 or > 200)
                throw new ValidationException("nome deve ter entre 1 e 200 caracteres");
            entity.Nome = req.Nome.Trim();
        }
        if (req.Email is not null)
            entity.Email = EmailValidator.Require(req.Email);

        entity.AtualizadoEm = Clock.UtcNow();

        try
        {
            await repo.AtualizarAsync(entity, ct);
        }
        catch (Exception ex) when (ex.IsUniqueViolation())
        {
            throw new ConflictException($"E-mail '{entity.Email}' já cadastrado.");
        }

        return Map(entity);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        if (!await repo.ExcluirAsync(id, ct))
            throw new NotFoundException("Destinatário não encontrado.");
        return NoContent();
    }

    [HttpPatch("{id:guid}/toggle-ativo")]
    public async Task<ToggleAtivoResponse> ToggleAtivo(Guid id, CancellationToken ct)
    {
        var entity = await repo.GetByIdAsync(id, ct) ?? throw new NotFoundException("Destinatário não encontrado.");
        entity.Ativo = !entity.Ativo;
        entity.AtualizadoEm = Clock.UtcNow();
        await repo.AtualizarAsync(entity, ct);
        return new ToggleAtivoResponse(entity.Id, entity.Ativo);
    }

    private static DestinatarioResponse Map(Destinatario d) =>
        new(d.Id, d.Nome, d.Email, d.Ativo, d.AtualizadoEm);
}
