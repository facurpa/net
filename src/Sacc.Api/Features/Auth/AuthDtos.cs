using System.Text.Json.Serialization;

namespace Sacc.Api.Features.Auth;

public sealed record LoginRequest(string Email, string Senha);

public sealed record RefreshRequest(string RefreshToken);

public sealed record LogoutRequest(string? RefreshToken);

public sealed record ChangePasswordRequest(string SenhaAtual, string NovaSenha);

public sealed record UserInfo(Guid Id, string Email, string NomeCompleto, string? Role);

/// <summary>Resposta do <c>/login</c>. <c>refresh_token</c> é omitido no fluxo de troca forçada de senha.</summary>
public sealed class LoginTokenResponse
{
    public string AccessToken { get; init; } = "";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RefreshToken { get; init; }

    public string TokenType { get; init; } = "bearer";
    public bool RequirePasswordChange { get; init; }
    public UserInfo User { get; init; } = null!;
}

/// <summary>Resposta do <c>/refresh</c>: apenas tokens.</summary>
public sealed record RefreshTokenResponse(string AccessToken, string RefreshToken, string TokenType = "bearer");

public sealed record MeResponse(Guid Id, string Email, string NomeCompleto, string Role);

public sealed record OkResponse(bool Ok = true);
