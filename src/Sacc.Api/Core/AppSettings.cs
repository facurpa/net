using Microsoft.Data.SqlClient;
using Npgsql;

namespace Sacc.Api.Core;

/// <summary>
/// Typed configuration, equivalent to the Python Pydantic <c>Settings</c>.
/// Reads flat environment-variable names (the legacy <c>.env</c>) so the same file can be reused.
/// Validation runs at construction; invalid configuration fails the boot.
/// </summary>
public sealed class AppSettings
{
    // Database
    public string DatabaseUrl { get; init; } = "";
    public string ErpDatabaseUrl { get; init; } = "";
    public string NpgsqlConnectionString { get; private set; } = "";
    public string SqlServerConnectionString { get; private set; } = "";

    // Security / JWT
    public string SecretKey { get; init; } = "";
    public int AccessTokenExpireMinutes { get; init; } = 30;
    public int RefreshTokenExpireDays { get; init; } = 7;
    public int PasswordMinLength { get; init; } = 12;
    public int MaxLoginAttempts { get; init; } = 5;
    public int LockoutDurationMinutes { get; init; } = 15;

    // Bootstrap admin
    public string? InitialAdminEmail { get; init; }
    public string? InitialAdminPassword { get; init; }
    public string InitialAdminNome { get; init; } = "Administrador";

    // SMTP
    public string SmtpHost { get; init; } = "";
    public int SmtpPort { get; init; } = 587;
    public string SmtpUser { get; init; } = "";
    public string SmtpPassword { get; init; } = "";
    public string SmtpFrom { get; init; } = "";

    // Environment / web
    public string Env { get; init; } = "dev";
    public bool IsProd => string.Equals(Env, "prod", StringComparison.OrdinalIgnoreCase);
    public string[] CorsOrigins { get; init; } = ["http://localhost:3000"];

    // Worker
    public int WorkerCronHour { get; init; } = 7;
    public int WorkerCronMinute { get; init; } = 0;
    public bool RunScheduler { get; init; }
    public string[] IncidentAlertEmails { get; init; } = [];
    public bool IncidentOnConfigMissing { get; init; } = true;

    // Paridade: balancete público (smoke test temporário) — Seção 13.3.
    public bool BalancetePublic { get; init; } = true;

    public static AppSettings Load(IConfiguration cfg)
    {
        string? Get(string key) => cfg[key];
        string Req(string key) =>
            Get(key) ?? throw new InvalidOperationException($"Configuração obrigatória ausente: {key}");
        int GetInt(string key, int fallback) =>
            int.TryParse(Get(key), out var v) ? v : fallback;
        bool GetBool(string key, bool fallback) =>
            bool.TryParse(Get(key), out var v) ? v : fallback;
        string[] GetCsv(string key, string[] fallback)
        {
            var raw = Get(key);
            if (string.IsNullOrWhiteSpace(raw)) return fallback;
            return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        var env = (Get("ENV") ?? "dev").Trim().ToLowerInvariant();
        if (env is not ("dev" or "prod"))
            throw new InvalidOperationException($"ENV inválido: '{env}'. Use 'dev' ou 'prod'.");

        var secret = Req("SECRET_KEY");
        if (secret.Length < 32)
            throw new InvalidOperationException("SECRET_KEY deve ter no mínimo 32 caracteres.");
        var isProd = env == "prod";
        if (isProd && secret.Contains("changeme", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SECRET_KEY de exemplo ('changeme') não pode ser usada em produção.");

        var settings = new AppSettings
        {
            DatabaseUrl = Req("DATABASE_URL"),
            ErpDatabaseUrl = Req("ERP_DATABASE_URL"),
            SecretKey = secret,
            AccessTokenExpireMinutes = GetInt("ACCESS_TOKEN_EXPIRE_MINUTES", 30),
            RefreshTokenExpireDays = GetInt("REFRESH_TOKEN_EXPIRE_DAYS", 7),
            PasswordMinLength = GetInt("PASSWORD_MIN_LENGTH", 12),
            MaxLoginAttempts = GetInt("MAX_LOGIN_ATTEMPTS", 5),
            LockoutDurationMinutes = GetInt("LOCKOUT_DURATION_MINUTES", 15),
            InitialAdminEmail = Get("INITIAL_ADMIN_EMAIL"),
            InitialAdminPassword = Get("INITIAL_ADMIN_PASSWORD"),
            InitialAdminNome = Get("INITIAL_ADMIN_NOME") ?? "Administrador",
            SmtpHost = Req("SMTP_HOST"),
            SmtpPort = GetInt("SMTP_PORT", 587),
            SmtpUser = Get("SMTP_USER") ?? "",
            SmtpPassword = Get("SMTP_PASSWORD") ?? "",
            SmtpFrom = Get("SMTP_FROM") ?? "",
            Env = env,
            CorsOrigins = GetCsv("CORS_ORIGINS", ["http://localhost:3000"]),
            WorkerCronHour = GetInt("WORKER_CRON_HOUR", 7),
            WorkerCronMinute = GetInt("WORKER_CRON_MINUTE", 0),
            RunScheduler = GetBool("RUN_SCHEDULER", false),
            IncidentAlertEmails = GetCsv("INCIDENT_ALERT_EMAILS", []),
            IncidentOnConfigMissing = GetBool("INCIDENT_ON_CONFIG_MISSING", true),
            BalancetePublic = GetBool("BALANCETE_PUBLIC", true),
        };

        settings.NpgsqlConnectionString = ConnectionStrings.ToNpgsql(settings.DatabaseUrl);
        settings.SqlServerConnectionString = ConnectionStrings.ToSqlServer(settings.ErpDatabaseUrl);
        return settings;
    }
}

/// <summary>Converte as URLs no estilo SQLAlchemy do <c>.env</c> legado para connection strings nativas.</summary>
public static class ConnectionStrings
{
    /// <summary>
    /// <c>postgresql+asyncpg://user:pass@host:port/db</c> → connection string Npgsql.
    /// A senha vem URL-encoded (ex.: <c>Cast%402026%21</c>).
    /// </summary>
    public static string ToNpgsql(string url)
    {
        var (user, pass, host, port, db, _) = ParseUrl(url);
        var b = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port ?? 5432,
            Database = db,
            Username = user,
            Password = pass,
        };
        return b.ConnectionString;
    }

    /// <summary>
    /// <c>mssql+aioodbc://user:pass@host:port/db?driver=...&amp;TrustServerCertificate=yes&amp;Encrypt=yes</c>
    /// → connection string Microsoft.Data.SqlClient.
    /// </summary>
    public static string ToSqlServer(string url)
    {
        var (user, pass, host, port, db, query) = ParseUrl(url);
        var b = new SqlConnectionStringBuilder
        {
            DataSource = port is null ? host : $"{host},{port}",
            InitialCatalog = db,
            UserID = user,
            Password = pass,
            TrustServerCertificate = YesNo(query, "TrustServerCertificate", true),
            Encrypt = YesNo(query, "Encrypt", true),
        };
        return b.ConnectionString;
    }

    private static bool YesNo(IReadOnlyDictionary<string, string> q, string key, bool fallback)
    {
        if (!q.TryGetValue(key, out var v)) return fallback;
        return v.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || v.Equals("true", StringComparison.OrdinalIgnoreCase)
            || v == "1";
    }

    private static (string user, string pass, string host, int? port, string db, IReadOnlyDictionary<string, string> query)
        ParseUrl(string url)
    {
        var uri = new Uri(url);
        var userInfo = uri.UserInfo.Split(':', 2);
        var user = Uri.UnescapeDataString(userInfo[0]);
        var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var host = uri.Host;
        int? port = uri.Port > 0 ? uri.Port : null;
        var db = uri.AbsolutePath.Trim('/');

        var query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            query[Uri.UnescapeDataString(kv[0])] = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : "";
        }
        return (user, pass, host, port, db, query);
    }
}
