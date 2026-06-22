using Quartz;

namespace Sacc.Api.Features.Worker;

/// <summary>Job Quartz da verificação agendada.</summary>
[DisallowConcurrentExecution]
public sealed class VerificacaoJob(VerificacaoService service) : IJob
{
    public static readonly JobKey Key = new("verificacao_agendada", "worker");

    public Task Execute(IJobExecutionContext context) =>
        service.ExecutarAsync("agendada", context.CancellationToken);
}
