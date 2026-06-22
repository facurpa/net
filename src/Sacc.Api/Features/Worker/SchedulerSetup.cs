using Quartz;
using Sacc.Api.Core;

namespace Sacc.Api.Features.Worker;

public static class SchedulerSetup
{
    /// <summary>Fuso horário de São Paulo (IANA, com fallback para o id do Windows).</summary>
    public static TimeZoneInfo SaoPaulo()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"); }
    }

    /// <summary>Registra o Quartz com o job de verificação diária. Só quando RUN_SCHEDULER=true.</summary>
    public static IServiceCollection AddSaccScheduler(this IServiceCollection services, AppSettings settings)
    {
        if (!settings.RunScheduler)
            return services;

        // Quartz cron: segundos minutos horas dia-do-mês mês dia-da-semana.
        var cron = $"0 {settings.WorkerCronMinute} {settings.WorkerCronHour} ? * *";
        var tz = SaoPaulo();

        services.AddQuartz(q =>
        {
            q.AddJob<VerificacaoJob>(j => j.WithIdentity(VerificacaoJob.Key));
            q.AddTrigger(t => t
                .ForJob(VerificacaoJob.Key)
                .WithIdentity("verificacao_agendada_trigger", "worker")
                .WithCronSchedule(cron, x => x
                    .InTimeZone(tz)
                    // misfire_grace_time ~3600s: dispara e segue em caso de misfire.
                    .WithMisfireHandlingInstructionFireAndProceed()));
        });
        services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);
        return services;
    }
}
