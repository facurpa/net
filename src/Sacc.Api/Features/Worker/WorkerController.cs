using Microsoft.AspNetCore.Mvc;

namespace Sacc.Api.Features.Worker;

// TODO(security): exigir autenticação (paridade — hoje público, ver Seção 13.2).
[ApiController]
[Route("api/worker")]
public sealed class WorkerController(IBackgroundTaskQueue queue) : ControllerBase
{
    [HttpPost("trigger")]
    public async Task<IActionResult> Trigger()
    {
        await queue.EnqueueAsync((sp, ct) =>
            sp.GetRequiredService<VerificacaoService>().ExecutarAsync("manual", ct));

        return StatusCode(StatusCodes.Status202Accepted,
            new TriggerResponse("verificacao_manual", "Verificação iniciada em background."));
    }
}
