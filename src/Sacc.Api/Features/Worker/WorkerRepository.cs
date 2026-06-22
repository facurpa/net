using Dapper;
using Sacc.Api.Core.Db;
using Sacc.Api.Features.Logs;

namespace Sacc.Api.Features.Worker;

/// <summary>Escrita em <c>logs_execucao</c> (timestamps com tz / UTC; <c>contas_viradas</c> jsonb).</summary>
public sealed class WorkerRepository(IDbConnectionFactory factory)
{
    public async Task CriarLogAsync(LogExecucao log, CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        await conn.ExecuteAsync(
            @"INSERT INTO logs_execucao (id, iniciado_em, status, tipo_execucao, alerta_enviado)
              VALUES (@Id, @IniciadoEm, @Status, @TipoExecucao, @AlertaEnviado)", log);
    }

    public async Task AtualizarLogAsync(LogExecucao log, CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        await conn.ExecuteAsync(
            @"UPDATE logs_execucao SET
                 finalizado_em = @FinalizadoEm,
                 status = @Status,
                 empresa_codigo = @EmpresaCodigo,
                 data_inicio_ref = @DataInicioRef,
                 data_fim_ref = @DataFimRef,
                 qtd_contas_analisadas = @QtdContasAnalisadas,
                 qtd_contas_viradas = @QtdContasViradas,
                 contas_viradas = @ContasViradas,
                 erro_mensagem = @ErroMensagem,
                 tipo_execucao = @TipoExecucao,
                 erro_tipo = @ErroTipo,
                 stack_trace = @StackTrace,
                 duracao_ms = @DuracaoMs,
                 alerta_enviado = @AlertaEnviado
               WHERE id = @Id", log);
    }
}
