namespace Sacc.Api.Features.Destinatarios;

public sealed record DestinatarioCreate(string Nome, string Email);

public sealed record DestinatarioUpdate(string? Nome, string? Email);

public sealed record DestinatarioResponse(Guid Id, string Nome, string Email, bool Ativo, DateTime AtualizadoEm);

public sealed record ToggleAtivoResponse(Guid Id, bool Ativo);
