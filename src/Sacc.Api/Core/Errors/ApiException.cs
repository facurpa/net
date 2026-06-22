namespace Sacc.Api.Core.Errors;

/// <summary>
/// Exceção de domínio mapeada para uma resposta HTTP no estilo FastAPI: <c>{ "detail": "&lt;mensagem&gt;" }</c>.
/// </summary>
public class ApiException(int statusCode, string detail, IReadOnlyDictionary<string, string>? headers = null)
    : Exception(detail)
{
    public int StatusCode { get; } = statusCode;
    public string Detail { get; } = detail;

    /// <summary>Headers extras a anexar à resposta (ex.: <c>WWW-Authenticate: Bearer</c>).</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; } = headers;
}

public sealed class BadRequestException(string detail) : ApiException(400, detail);

public sealed class UnauthorizedException(string detail = "Não autenticado")
    : ApiException(401, detail, new Dictionary<string, string> { ["WWW-Authenticate"] = "Bearer" });

public sealed class ForbiddenException(string detail = "Permissão insuficiente") : ApiException(403, detail);

public sealed class NotFoundException(string detail) : ApiException(404, detail);

public sealed class ConflictException(string detail) : ApiException(409, detail);

/// <summary>Erro de validação (política de senha, formato de e-mail, etc.) → 422.</summary>
public sealed class ValidationException(string detail) : ApiException(422, detail);

public sealed class BadGatewayException(string detail) : ApiException(502, detail);
