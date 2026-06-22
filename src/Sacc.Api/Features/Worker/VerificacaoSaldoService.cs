using Sacc.Api.Features.Balancete;

namespace Sacc.Api.Features.Worker;

/// <summary>
/// Seam isolando a regra de detecção de contas viradas. Função pura, sem I/O.
/// Quando a definição de saldo/movimento (ver TODO) for confirmada, só esta classe muda.
/// </summary>
public interface IVerificacaoSaldoService
{
    IReadOnlyList<ContaViradaJson> DetectarContasViradas(
        IEnumerable<SaldoContabil> balancete,
        IReadOnlyDictionary<string, string> naturezaPorConta);
}

public sealed class VerificacaoSaldoService : IVerificacaoSaldoService
{
    public IReadOnlyList<ContaViradaJson> DetectarContasViradas(
        IEnumerable<SaldoContabil> balancete,
        IReadOnlyDictionary<string, string> naturezaPorConta)
    {
        // TODO: confirmar com o time contábil se DEBITO/CREDITO representam SALDO ou MOVIMENTO
        // acumulado. A regra abaixo é portada 1:1 da origem e NÃO deve ser "corrigida" sem essa
        // definição — qualquer mudança aqui altera o comportamento observável da verificação.
        var viradas = new List<ContaViradaJson>();

        foreach (var linha in balancete)
        {
            if (!naturezaPorConta.TryGetValue(linha.Conta, out var natureza))
                continue;

            var debito = linha.Debito ?? 0m;
            var credito = linha.Credito ?? 0m;

            var virada = natureza switch
            {
                "D" => credito > debito,
                "C" => debito > credito,
                _ => false,
            };

            if (virada)
                viradas.Add(new ContaViradaJson { Conta = linha.Conta, Descricao = linha.Descricao });
        }

        return viradas;
    }
}
