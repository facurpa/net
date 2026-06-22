using System.Text.Json;

namespace Sacc.Api.Core.Errors;

/// <summary>
/// Traduz exceções em respostas JSON no estilo FastAPI (<c>{ "detail": ... }</c>).
/// <see cref="ApiException"/> usa seu próprio status/headers; qualquer outra vira 500.
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ApiException ex)
        {
            await WriteAsync(context, ex.StatusCode, ex.Detail, ex.Headers);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro não tratado em {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteAsync(context, 500, "Erro interno do servidor", null);
        }
    }

    private static async Task WriteAsync(HttpContext context, int status, string detail,
        IReadOnlyDictionary<string, string>? headers)
    {
        if (context.Response.HasStarted) return;
        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        if (headers is not null)
            foreach (var (k, v) in headers)
                context.Response.Headers[k] = v;

        await context.Response.WriteAsync(JsonSerializer.Serialize(new { detail }));
    }
}
