using System.Diagnostics;
using Sacc.Api.Core;
using Sacc.Api.Features.Balancete;
using Sacc.Api.Features.Destinatarios;
using Sacc.Api.Features.EmailTemplates;
using Sacc.Api.Features.Logs;
using Sacc.Api.Features.Periodos;
using Sacc.Api.Features.PlanoContas;

namespace Sacc.Api.Features.Worker;

/// <summary>Orquestra a verificação de contas viradas (agendada ou manual).</summary>
public sealed class VerificacaoService(
    WorkerRepository workerRepo,
    PeriodosRepository periodosRepo,
    EmailTemplatesRepository templatesRepo,
    DestinatariosRepository destinatariosRepo,
    PlanoContasRepository planoRepo,
    BalanceteRepository balanceteRepo,
    IVerificacaoSaldoService detector,
    IEmailService email,
    IncidenteService incidentes,
    AppSettings settings,
    ILogger<VerificacaoService> logger)
{
    public async Task ExecutarAsync(string tipoExecucao, CancellationToken ct = default)
    {
        // Idempotência diária: execução AGENDADA não roda duas vezes no mesmo dia (fuso São Paulo).
        // Execução MANUAL nunca é bloqueada.
        if (tipoExecucao == "agendada" && settings.SkipSeJaExecutadoHoje)
        {
            var tz = SchedulerSetup.SaoPaulo();
            var agoraSp = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
            var inicioDiaSp = new DateTimeOffset(agoraSp.Year, agoraSp.Month, agoraSp.Day, 0, 0, 0, agoraSp.Offset);
            var inicioUtc = inicioDiaSp.UtcDateTime;
            var fimUtc = inicioDiaSp.AddDays(1).UtcDateTime;

            if (await workerRepo.ExisteAgendadaNoIntervaloAsync(inicioUtc, fimUtc, ct))
            {
                logger.LogInformation("verificacao_agendada_ignorada motivo=ja_executado_hoje");
                return;
            }
        }

        var sw = Stopwatch.StartNew();
        var log = new LogExecucao
        {
            Id = Guid.NewGuid(),
            IniciadoEm = Clock.UtcNow(),
            Status = LogStatus.Executando,
            TipoExecucao = tipoExecucao,
            AlertaEnviado = false,
        };
        await workerRepo.CriarLogAsync(log, ct);

        try
        {
            var periodo = await periodosRepo.GetAtivoAsync(ct);
            if (periodo is null)
            {
                await FinalizarConfigAusenteAsync(log, sw, "Nenhum período de verificação cadastrado.", tipoExecucao, ct);
                return;
            }

            var template = await templatesRepo.GetAtivoAsync(ct);
            if (template is null)
            {
                await FinalizarConfigAusenteAsync(log, sw, "Nenhum template de e-mail cadastrado.", tipoExecucao, ct);
                return;
            }

            var destinatarios = await destinatariosRepo.EmailsAtivosAsync(ct);
            var plano = await planoRepo.NaturezaPorContaAtivosAsync(ct);
            var balancete = await balanceteRepo.BuscarAsync(periodo.EmpresaCodigo, periodo.DataInicio, periodo.DataFim, ct);
            var viradas = detector.DetectarContasViradas(balancete, plano);

            log.EmpresaCodigo = periodo.EmpresaCodigo;
            log.DataInicioRef = periodo.DataInicio;
            log.DataFimRef = periodo.DataFim;
            log.QtdContasAnalisadas = balancete.Count;
            log.QtdContasViradas = viradas.Count;
            log.ContasViradas = viradas.ToList();
            log.FinalizadoEm = Clock.UtcNow();
            log.DuracaoMs = (int)sw.ElapsedMilliseconds;

            if (viradas.Count == 0)
            {
                log.Status = LogStatus.SemAlertas;
                await workerRepo.AtualizarLogAsync(log, ct);
                return;
            }

            if (destinatarios.Count == 0)
            {
                // Há viradas mas ninguém para notificar: sucesso sem alerta enviado.
                log.Status = LogStatus.Sucesso;
                log.AlertaEnviado = false;
                await workerRepo.AtualizarLogAsync(log, ct);
                return;
            }

            var enviado = await email.EnviarAlertaAsync(destinatarios, template.CorpoHtml, log.IniciadoEm, viradas, ct);
            if (!enviado)
            {
                log.Status = LogStatus.Erro;
                log.ErroMensagem = "Falha no envio do e-mail de alerta";
                await workerRepo.AtualizarLogAsync(log, ct);
                await incidentes.NotificarAsync(log, "email_envio", "Falha no envio do e-mail de alerta", null, tipoExecucao, ct);
                return;
            }

            log.Status = LogStatus.Sucesso;
            log.AlertaEnviado = true;
            await workerRepo.AtualizarLogAsync(log, ct);
        }
        catch (Exception ex)
        {
            log.Status = LogStatus.Erro;
            log.ErroMensagem = ex.Message;
            log.FinalizadoEm = Clock.UtcNow();
            log.DuracaoMs = (int)sw.ElapsedMilliseconds;

            var (erroTipo, mensagem) = Incidentes.Classificar(ex);
            log.ErroTipo = erroTipo;
            log.StackTrace = ex.ToString();
            await workerRepo.AtualizarLogAsync(log, ct);

            await incidentes.NotificarAsync(log, erroTipo, mensagem, ex.ToString(), tipoExecucao, ct);
        }
    }

    private async Task FinalizarConfigAusenteAsync(LogExecucao log, Stopwatch sw, string mensagem, string tipoExecucao, CancellationToken ct)
    {
        logger.LogWarning("verificacao_config_ausente {Mensagem}", mensagem);
        log.Status = LogStatus.Erro;
        log.ErroMensagem = mensagem;
        log.ErroTipo = "configuracao_ausente";
        log.FinalizadoEm = Clock.UtcNow();
        log.DuracaoMs = (int)sw.ElapsedMilliseconds;
        await workerRepo.AtualizarLogAsync(log, ct);

        // Incidente por config ausente só quando habilitado (INCIDENT_ON_CONFIG_MISSING).
        if (settings.IncidentOnConfigMissing)
            await incidentes.NotificarAsync(log, "configuracao_ausente", mensagem, null, tipoExecucao, ct);
    }
}
