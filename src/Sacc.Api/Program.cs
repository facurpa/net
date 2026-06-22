using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Sacc.Api.Core;
using Sacc.Api.Core.Db;
using Sacc.Api.Core.Errors;
using Sacc.Api.Core.Logging;
using Sacc.Api.Core.RateLimiting;
using Sacc.Api.Core.Security;
using Sacc.Api.Core.Web;
using Sacc.Api.Features.Auth;
using Sacc.Api.Features.Worker;
using Serilog;

DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

// Configuração tipada (falha o boot se inválida) e infra global do Dapper.
var settings = AppSettings.Load(builder.Configuration);
DapperConfig.Init();

// Logging estruturado: console legível em dev, JSON em prod, com redação de campos sensíveis.
builder.Host.UseSerilog((_, lc) => lc.ConfigureSacc(settings.IsProd));

var jwt = new JwtService(settings);

builder.Services.AddSingleton(settings);
builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton<PasswordHasher>();
builder.Services.AddSingleton<IDbConnectionFactory>(_ => new DbConnectionFactory(settings));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUserAccessor>();
builder.Services.AddScoped<AuthRepository>();
builder.Services.AddScoped<Sacc.Api.Features.PlanoContas.PlanoContasRepository>();
builder.Services.AddScoped<Sacc.Api.Features.Destinatarios.DestinatariosRepository>();
builder.Services.AddScoped<Sacc.Api.Features.EmailTemplates.EmailTemplatesRepository>();
builder.Services.AddScoped<Sacc.Api.Features.Periodos.PeriodosRepository>();
builder.Services.AddScoped<Sacc.Api.Features.Logs.LogsRepository>();
builder.Services.AddScoped<Sacc.Api.Features.Usuarios.UsuariosRepository>();
builder.Services.AddScoped<Sacc.Api.Features.Balancete.BalanceteRepository>();

// Worker
builder.Services.AddSingleton<Sacc.Api.Features.Worker.IVerificacaoSaldoService, Sacc.Api.Features.Worker.VerificacaoSaldoService>();
builder.Services.AddSingleton<Sacc.Api.Features.Worker.IEmailService, Sacc.Api.Features.Worker.EmailService>();
builder.Services.AddSingleton<Sacc.Api.Features.Worker.IBackgroundTaskQueue, Sacc.Api.Features.Worker.BackgroundTaskQueue>();
builder.Services.AddHostedService<Sacc.Api.Features.Worker.QueuedHostedService>();
builder.Services.AddScoped<Sacc.Api.Features.Worker.WorkerRepository>();
builder.Services.AddScoped<Sacc.Api.Features.Worker.IncidenteService>();
builder.Services.AddScoped<Sacc.Api.Features.Worker.VerificacaoService>();
builder.Services.AddSaccScheduler(settings);

builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        o.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    });

// Erros de validação de corpo (binding) → 422 no estilo FastAPI.
builder.Services.Configure<ApiBehaviorOptions>(o =>
{
    o.InvalidModelStateResponseFactory = ctx =>
    {
        var detail = ctx.ModelState
            .SelectMany(kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage))
            .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m)) ?? "Dados inválidos";
        return new ObjectResult(new { detail }) { StatusCode = 422 };
    };
});

builder.Services.AddSaccAuthentication(jwt);
builder.Services.AddAuthorization(o => o.AddRequireAdmin());
builder.Services.AddRateLimiter(o => o.AddSaccPolicies());

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(settings.CorsOrigins).AllowAnyHeader().AllowAnyMethod()));

if (!settings.IsProd)
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
}

var app = builder.Build();

// Startup guard de prod (Seção 13.3): balancete público não pode subir em produção.
if (settings.IsProd && settings.BalancetePublic)
{
    app.Logger.LogError("BALANCETE_PUBLIC=true não é permitido em produção. Abortando o boot.");
    throw new InvalidOperationException("BALANCETE_PUBLIC=true em produção.");
}

// Bootstrap do admin inicial.
await AdminBootstrap.RunAsync(app.Services, settings, app.Logger);

if (settings.RunScheduler)
    app.Logger.LogInformation("scheduler_iniciado hora={Hora} minuto={Minuto} tz=America/Sao_Paulo",
        settings.WorkerCronHour, settings.WorkerCronMinute);

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (!settings.IsProd)
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.RoutePrefix = "docs");
}

app.MapControllers();
app.MapGet("/health", () => Results.Json(new { status = "ok" }));

app.Run();

public partial class Program;
