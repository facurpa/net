namespace Sacc.Api.Features.Auth;

/// <summary>Evento de auditoria (<c>auth_eventos</c>). <c>criado_em</c> é timestamp SEM tz (UTC naive).</summary>
public sealed class AuthEvento
{
    public string Tipo { get; set; } = "";
    public Guid? UsuarioId { get; set; }
    public string? EmailTentado { get; set; }
    public string? IpOrigem { get; set; }
    public string? UserAgent { get; set; }
    public Dictionary<string, object?>? Detalhes { get; set; }
}

/// <summary>Tipos de evento registrados em <c>auth_eventos</c>.</summary>
public static class AuthEventos
{
    public const string LoginFalha = "login_falha";
    public const string LoginSucesso = "login_sucesso";
    public const string ContaBloqueada = "conta_bloqueada";
    public const string SenhaAlterada = "senha_alterada";
    public const string UsuarioCriado = "usuario_criado";
    public const string UsuarioEditado = "usuario_editado";
}
