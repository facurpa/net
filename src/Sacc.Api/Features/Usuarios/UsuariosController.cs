using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sacc.Api.Core;
using Sacc.Api.Core.Db;
using Sacc.Api.Core.Errors;
using Sacc.Api.Core.Security;
using Sacc.Api.Core.Validation;
using Sacc.Api.Features.Auth;

namespace Sacc.Api.Features.Usuarios;

[ApiController]
[Route("api/usuarios")]
[Authorize(Policy = AdminAuthorization.PolicyName)]
public sealed class UsuariosController(
    UsuariosRepository repo,
    AuthRepository authRepo,
    PasswordHasher hasher,
    CurrentUserAccessor current) : ControllerBase
{
    private static readonly string[] RolesValidas = ["admin", "usuario"];

    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();
    private string? UserAgent => Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;

    [HttpPost("")]
    public async Task<IActionResult> Criar([FromBody] UsuarioCreate req, CancellationToken ct)
    {
        var email = EmailValidator.Require(req.Email);
        var nome = (req.NomeCompleto ?? "").Trim();
        if (nome.Length is < 2 or > 255)
            throw new ValidationException("nome_completo deve ter entre 2 e 255 caracteres");
        var role = ValidarRole(req.Role) ?? "usuario";
        hasher.ValidarSenhaPolitica(req.Senha);

        var agora = Clock.UtcNowNaive();
        var entity = new Usuario
        {
            Id = Guid.NewGuid(),
            Email = email,
            NomeCompleto = nome,
            PasswordHash = hasher.Hash(req.Senha),
            Role = role,
            Ativo = true,
            MustChangePassword = false,
            CriadoEm = agora,
            CriadoPor = current.User.Email,
            AtualizadoEm = agora,
        };

        try
        {
            await authRepo.InserirUsuarioAsync(entity, ct);
        }
        catch (Exception ex) when (ex.IsUniqueViolation())
        {
            throw new ConflictException("Email já cadastrado");
        }

        await authRepo.InserirEventoAsync(new AuthEvento
        {
            Tipo = AuthEventos.UsuarioCriado,
            UsuarioId = entity.Id,
            EmailTentado = entity.Email,
            IpOrigem = Ip,
            UserAgent = UserAgent,
            Detalhes = new Dictionary<string, object?> { ["criado_por"] = current.User.Email, ["role"] = role },
        }, ct);

        return StatusCode(StatusCodes.Status201Created, Map(entity));
    }

    [HttpGet("")]
    public async Task<UsuarioListResponse> Listar([FromQuery] int page = 1, [FromQuery(Name = "page_size")] int pageSize = 20, CancellationToken ct = default)
    {
        if (page < 1)
            throw new ValidationException("page deve ser maior ou igual a 1");
        if (pageSize is < 1 or > 100)
            throw new ValidationException("page_size deve estar entre 1 e 100");

        var (items, total) = await repo.ListarAsync(page, pageSize, ct);
        return new UsuarioListResponse(items.Select(Map).ToList(), total, page, pageSize);
    }

    [HttpGet("{id:guid}")]
    public async Task<UsuarioResponse> Detalhe(Guid id, CancellationToken ct)
    {
        var u = await repo.GetByIdAsync(id, ct) ?? throw new NotFoundException("Usuário não encontrado");
        return Map(u);
    }

    [HttpPut("{id:guid}")]
    public async Task<UsuarioResponse> Atualizar(Guid id, [FromBody] UsuarioUpdate req, CancellationToken ct)
    {
        var u = await repo.GetByIdAsync(id, ct) ?? throw new NotFoundException("Usuário não encontrado");

        if (req.NomeCompleto is not null)
        {
            if (req.NomeCompleto.Trim().Length is < 2 or > 255)
                throw new ValidationException("nome_completo deve ter entre 2 e 255 caracteres");
            u.NomeCompleto = req.NomeCompleto.Trim();
        }

        var novaRole = ValidarRole(req.Role);
        if (novaRole is not null && novaRole != u.Role)
        {
            if (id == current.User.Oid)
                throw new BadRequestException("Não é possível alterar a própria role");
            // Rebaixar o último admin ativo.
            if (u.Role == "admin" && u.Ativo && await authRepo.ContarAdminsAtivosAsync(ct) <= 1)
                throw new BadRequestException("Não é possível remover o último administrador ativo");
            u.Role = novaRole;
        }

        if (req.Ativo is not null && req.Ativo.Value != u.Ativo)
        {
            // Desativar o último admin ativo.
            if (!req.Ativo.Value && u.Role == "admin" && u.Ativo && await authRepo.ContarAdminsAtivosAsync(ct) <= 1)
                throw new BadRequestException("Não é possível remover o último administrador ativo");
            u.Ativo = req.Ativo.Value;
        }

        await repo.AtualizarAsync(u, ct);
        await authRepo.InserirEventoAsync(new AuthEvento
        {
            Tipo = AuthEventos.UsuarioEditado,
            UsuarioId = u.Id,
            EmailTentado = u.Email,
            IpOrigem = Ip,
            UserAgent = UserAgent,
            Detalhes = new Dictionary<string, object?> { ["editado_por"] = current.User.Email },
        }, ct);

        return Map(u);
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<OkResponse> ResetPassword(Guid id, [FromBody] ResetPasswordRequest req, CancellationToken ct)
    {
        hasher.ValidarSenhaPolitica(req.NovaSenha);
        var u = await repo.GetByIdAsync(id, ct) ?? throw new NotFoundException("Usuário não encontrado");

        await repo.ResetSenhaAsync(u.Id, hasher.Hash(req.NovaSenha), ct);
        await authRepo.InserirEventoAsync(new AuthEvento
        {
            Tipo = AuthEventos.SenhaAlterada,
            UsuarioId = u.Id,
            EmailTentado = u.Email,
            IpOrigem = Ip,
            UserAgent = UserAgent,
            Detalhes = new Dictionary<string, object?> { ["resetado_por"] = current.User.Email, ["forcado"] = true },
        }, ct);

        return new OkResponse();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken ct)
    {
        var u = await repo.GetByIdAsync(id, ct) ?? throw new NotFoundException("Usuário não encontrado");

        if (id == current.User.Oid)
            throw new BadRequestException("Não é possível desativar a própria conta");
        if (u.Role == "admin" && u.Ativo && await authRepo.ContarAdminsAtivosAsync(ct) <= 1)
            throw new BadRequestException("Não é possível remover o último administrador ativo");

        u.Ativo = false;
        await repo.AtualizarAsync(u, ct);
        await authRepo.InserirEventoAsync(new AuthEvento
        {
            Tipo = AuthEventos.UsuarioEditado,
            UsuarioId = u.Id,
            EmailTentado = u.Email,
            IpOrigem = Ip,
            UserAgent = UserAgent,
            Detalhes = new Dictionary<string, object?> { ["editado_por"] = current.User.Email, ["ativo"] = false },
        }, ct);

        return NoContent();
    }

    private static string? ValidarRole(string? role)
    {
        if (role is null) return null;
        var r = role.Trim().ToLowerInvariant();
        if (!RolesValidas.Contains(r))
            throw new ValidationException("role deve ser 'admin' ou 'usuario'");
        return r;
    }

    private static UsuarioResponse Map(Usuario u) =>
        new(u.Id, u.Email, u.NomeCompleto, u.Role, u.Ativo, u.MustChangePassword,
            u.UltimoLoginEm, u.CriadoEm, u.CriadoPor);
}
