using Microsoft.AspNetCore.Mvc;
using Sacc.Api.Core;
using Sacc.Api.Core.Db;
using Sacc.Api.Core.Errors;

namespace Sacc.Api.Features.PlanoContas;

// TODO(security): exigir autenticação (paridade — hoje público, ver Seção 13.1).
[ApiController]
[Route("api/plano-contas")]
public sealed class PlanoContasController(PlanoContasRepository repo) : ControllerBase
{
    [HttpGet("")]
    public async Task<IReadOnlyList<PlanoContaResponse>> Listar(CancellationToken ct)
    {
        var rows = await repo.ListarAsync(ct);
        return rows.Select(Map).ToList();
    }

    [HttpPost("")]
    public async Task<IActionResult> Criar([FromBody] PlanoContaCreate req, CancellationToken ct)
    {
        var conta = (req.Conta ?? "").Trim();
        if (conta.Length is 0 or > 20)
            throw new ValidationException("conta deve ter entre 1 e 20 caracteres");
        if ((req.Descricao ?? "").Length is 0 or > 200)
            throw new ValidationException("descricao deve ter entre 1 e 200 caracteres");
        var natureza = ValidarNatureza(req.Natureza);

        var agora = Clock.UtcNow();
        var entity = new PlanoConta
        {
            Id = Guid.NewGuid(),
            Conta = conta,
            Descricao = req.Descricao,
            Natureza = natureza,
            Ativo = true,
            CriadoEm = agora,
            AtualizadoEm = agora,
            AtualizadoPor = "system",
        };

        try
        {
            await repo.CriarAsync(entity, ct);
        }
        catch (Exception ex) when (ex.IsUniqueViolation())
        {
            throw new ConflictException($"Conta '{conta}' já existe no plano de contas.");
        }

        return StatusCode(StatusCodes.Status201Created, Map(entity));
    }

    [HttpPut("{id:guid}")]
    public async Task<PlanoContaResponse> Atualizar(Guid id, [FromBody] PlanoContaUpdate req, CancellationToken ct)
    {
        var entity = await repo.GetByIdAsync(id, ct) ?? throw new NotFoundException("Conta não encontrada.");

        if (req.Descricao is not null)
        {
            if (req.Descricao.Length is 0 or > 200)
                throw new ValidationException("descricao deve ter entre 1 e 200 caracteres");
            entity.Descricao = req.Descricao;
        }
        if (req.Natureza is not null)
            entity.Natureza = ValidarNatureza(req.Natureza);
        if (req.Ativo is not null)
            entity.Ativo = req.Ativo.Value;

        entity.AtualizadoEm = Clock.UtcNow();
        entity.AtualizadoPor = "system";
        await repo.AtualizarAsync(entity, ct);
        return Map(entity);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        if (!await repo.ExcluirAsync(id, ct))
            throw new NotFoundException("Conta não encontrada.");
        return NoContent();
    }

    private static string ValidarNatureza(string? natureza)
    {
        var n = (natureza ?? "").Trim().ToUpperInvariant();
        if (n is not ("D" or "C"))
            throw new ValidationException("natureza deve ser 'D' ou 'C'");
        return n;
    }

    private static PlanoContaResponse Map(PlanoConta c) =>
        new(c.Id, c.Conta, c.Descricao, c.Natureza, c.Ativo, c.AtualizadoEm, c.AtualizadoPor);
}
