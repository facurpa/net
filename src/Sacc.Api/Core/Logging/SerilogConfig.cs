using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Sacc.Api.Core.Logging;

public static class SerilogConfig
{
    private static readonly string[] SensitiveKeys =
        ["password", "senha", "access_token", "refresh_token", "authorization", "secret"];

    public static LoggerConfiguration ConfigureSacc(this LoggerConfiguration cfg, bool isProd)
    {
        cfg.MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.With(new RedactionEnricher());

        // Console legível em dev; JSON estruturado (timestamp ISO) em prod.
        if (isProd)
            cfg.WriteTo.Console(new CompactJsonFormatter());
        else
            cfg.WriteTo.Console(
                outputTemplate: "[{Timestamp:yyyy-MM-ddTHH:mm:ss.fffZ} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");

        return cfg;
    }

    /// <summary>Redige valores de propriedades cujo nome é sensível ⇒ <c>***REDACTED***</c>.</summary>
    private sealed class RedactionEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            foreach (var key in logEvent.Properties.Keys.ToList())
            {
                if (SensitiveKeys.Any(s => key.Contains(s, StringComparison.OrdinalIgnoreCase)))
                    logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(key, "***REDACTED***"));
            }
        }
    }
}
