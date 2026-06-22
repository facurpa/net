using Microsoft.Data.SqlClient;
using Npgsql;
using Sacc.Api.Core;
using Sacc.Api.Features.Auth;
using Sacc.Api.Features.Logs;

namespace Sacc.Api.Features.Worker;

/// <summary>Marca ausência de configuração obrigatória (período/template).</summary>
public sealed class ConfigAusenteException(string message) : Exception(message);

public static class Incidentes
{
    /// <summary>
    /// Classifica a exceção por heurística (tipo + substrings). Timeout é checado ANTES de conexão.
    /// </summary>
    public static (string ErroTipo, string Mensagem) Classificar(Exception ex)
    {
        var msg = ex.Message ?? "";
        var lower = msg.ToLowerInvariant();

        if (ex is ConfigAusenteException)
            return ("configuracao_ausente", msg);

        if (ex is TimeoutException || lower.Contains("timeout"))
            return ("timeout", msg);

        if (ex is NpgsqlException || lower.Contains("operational"))
            return ("postgres_conexao", msg);

        if (ex is SqlException)
        {
            var conexao = new[] { "login", "connect", "server", "network" };
            if (conexao.Any(k => lower.Contains(k)))
                return ("erp_conexao", msg);
            return ("view_leitura", msg);
        }

        return ("excecao_nao_tratada", msg);
    }

    /// <summary>Ação recomendada (texto exato exibido no e-mail de incidente).</summary>
    public static string AcaoRecomendada(string erroTipo) => erroTipo switch
    {
        "erp_conexao" => "Validar disponibilidade do ambiente ERP/TOTVS.",
        "view_leitura" => "Validar a view contábil e a disponibilidade do ERP/TOTVS.",
        "postgres_conexao" => "Validar disponibilidade do banco de configuração.",
        "email_envio" => "Validar credenciais e disponibilidade do servidor SMTP.",
        "configuracao_ausente" => "Cadastrar período de verificação e/ou template de e-mail.",
        _ => "Analisar os logs da aplicação e o stack trace para diagnóstico.",
    };
}

/// <summary>
/// Notifica incidentes do worker: log estruturado <c>worker_incidente</c>, dedup por execução
/// (flag <c>alerta_enviado</c>) e e-mail operacional. Nunca propaga exceção.
/// </summary>
public sealed class IncidenteService(
    WorkerRepository repo,
    IEmailService email,
    AuthRepository authRepo,
    AppSettings settings,
    ILogger<IncidenteService> logger)
{
    public async Task NotificarAsync(LogExecucao log, string erroTipo, string mensagem, string? stackTrace,
        string tipoExecucao, CancellationToken ct = default)
    {
        try
        {
            if (log.AlertaEnviado) return; // dedup: no máximo 1 e-mail de incidente por execução.

            log.ErroTipo = erroTipo;
            if (stackTrace is not null) log.StackTrace = stackTrace;
            log.TipoExecucao = tipoExecucao;
            log.FinalizadoEm ??= Clock.UtcNow();

            logger.LogError("worker_incidente {ErroTipo} {Mensagem} {TipoExecucao}", erroTipo, mensagem, tipoExecucao);

            var destinatarios = await ResolverDestinatariosAsync(ct);
            if (destinatarios.Count > 0)
            {
                var detalhes = TrechoFinal(stackTrace ?? mensagem, 1500);
                var contexto = new IncidentEmailContext(
                    TipoExecucao: tipoExecucao,
                    Ambiente: settings.Env,
                    Erro: mensagem,
                    Detalhes: detalhes,
                    AcaoRecomendada: Incidentes.AcaoRecomendada(erroTipo),
                    DataHora: log.FinalizadoEm ?? Clock.UtcNow());
                await email.EnviarAlertaIncidenteAsync(destinatarios, contexto, ct);
            }

            log.AlertaEnviado = true;
            await repo.AtualizarLogAsync(log, ct);
        }
        catch (Exception ex)
        {
            // Uma falha de alerta nunca derruba o worker.
            logger.LogError(ex, "falha ao notificar incidente");
        }
    }

    private async Task<IReadOnlyList<string>> ResolverDestinatariosAsync(CancellationToken ct)
    {
        if (settings.IncidentAlertEmails.Length > 0)
            return settings.IncidentAlertEmails;
        return await authRepo.EmailsAdminsAtivosAsync(ct);
    }

    private static string TrechoFinal(string s, int max) =>
        s.Length <= max ? s : s[^max..];
}
