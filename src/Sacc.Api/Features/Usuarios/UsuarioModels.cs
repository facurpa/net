namespace Sacc.Api.Features.Usuarios;

/// <summary>Entidade da tabela <c>usuarios</c> (colunas timestamp SEM tz; UTC naive).</summary>
public sealed class Usuario
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string NomeCompleto { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "usuario";
    public bool Ativo { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public int FalhasLogin { get; set; }
    public DateTime? BloqueadoAte { get; set; }
    public DateTime? UltimoLoginEm { get; set; }
    public DateTime CriadoEm { get; set; }
    public string? CriadoPor { get; set; }
    public DateTime AtualizadoEm { get; set; }
}
