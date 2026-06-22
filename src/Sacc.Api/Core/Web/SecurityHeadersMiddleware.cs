namespace Sacc.Api.Core.Web;

/// <summary>Adiciona <c>X-Content-Type-Options: nosniff</c> e <c>X-Frame-Options: DENY</c> em toda resposta.</summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var h = context.Response.Headers;
            h["X-Content-Type-Options"] = "nosniff";
            h["X-Frame-Options"] = "DENY";
            return Task.CompletedTask;
        });
        return next(context);
    }
}
