using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Sacc.Api.Core.RateLimiting;

/// <summary>
/// Políticas de rate limit por IP (equivalente ao slowapi <c>get_remote_address</c>):
/// login=5/min, refresh=20/min, change-password=3/min. Excedido ⇒ 429.
/// </summary>
public static class RateLimitPolicies
{
    public const string Login = "login";
    public const string Refresh = "refresh";
    public const string ChangePassword = "change-password";

    public static RateLimiterOptions AddSaccPolicies(this RateLimiterOptions options)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        AddFixedPerIp(options, Login, 5);
        AddFixedPerIp(options, Refresh, 20);
        AddFixedPerIp(options, ChangePassword, 3);
        return options;
    }

    private static void AddFixedPerIp(RateLimiterOptions options, string policy, int permitPerMinute)
    {
        options.AddPolicy(policy, context =>
        {
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: $"{policy}:{ip}",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                });
        });
    }
}
