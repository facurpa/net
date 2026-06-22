namespace Sacc.Api.Features.Usuarios;

public sealed record UsuarioCreate(string Email, string NomeCompleto, string Senha, string? Role);

public sealed record UsuarioUpdate(string? NomeCompleto, string? Role, bool? Ativo);

public sealed record ResetPasswordRequest(string NovaSenha);

public sealed record UsuarioResponse(
    Guid Id,
    string Email,
    string NomeCompleto,
    string Role,
    bool Ativo,
    bool MustChangePassword,
    DateTime? UltimoLoginEm,
    DateTime CriadoEm,
    string? CriadoPor);

public sealed record UsuarioListResponse(IReadOnlyList<UsuarioResponse> Items, int Total, int Page, int PageSize);
