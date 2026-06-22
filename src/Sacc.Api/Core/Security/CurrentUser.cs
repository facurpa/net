using Sacc.Api.Core.Errors;

namespace Sacc.Api.Core.Security;

/// <summary>
/// Usuário autenticado da requisição atual (equivalente ao retorno de <c>get_current_user</c>).
/// Populado pelo evento <c>OnTokenValidated</c> após carregar e validar o usuário no banco.
/// </summary>
public sealed class CurrentUser
{
    public required Guid Oid { get; init; }
    public required string Email { get; init; }
    public required string DisplayName { get; init; }
    public required string Role { get; init; }
    public string? Scope { get; init; }

    public bool IsAdmin => string.Equals(Role, "admin", StringComparison.Ordinal);

    public const string HttpContextItemKey = "Sacc.CurrentUser";
}

/// <summary>Acesso ao <see cref="CurrentUser"/> da requisição. Lança 401 se ausente.</summary>
public sealed class CurrentUserAccessor(IHttpContextAccessor http)
{
    public CurrentUser User =>
        http.HttpContext?.Items[CurrentUser.HttpContextItemKey] as CurrentUser
        ?? throw new UnauthorizedException();
}
