using Sacc.Api.Features.Balancete;
using Sacc.Api.Features.Worker;
using Xunit;

namespace Sacc.Api.Tests;

public sealed class DetectarContasViradasTests
{
    private readonly IVerificacaoSaldoService _svc = new VerificacaoSaldoService();

    private static SaldoContabil Linha(string conta, decimal? debito, decimal? credito, string descricao = "Conta") =>
        new() { Conta = conta, Descricao = descricao, Debito = debito, Credito = credito, Empresa = 24, Data = 20250101 };

    [Fact]
    public void Devedora_vira_quando_credito_maior_que_debito()
    {
        var plano = new Dictionary<string, string> { ["1.1.01"] = "D" };
        var balancete = new[] { Linha("1.1.01", debito: 100, credito: 150, descricao: "Caixa") };

        var viradas = _svc.DetectarContasViradas(balancete, plano);

        Assert.Single(viradas);
        Assert.Equal("1.1.01", viradas[0].Conta);
        Assert.Equal("Caixa", viradas[0].Descricao);
    }

    [Fact]
    public void Devedora_nao_vira_quando_debito_maior_ou_igual()
    {
        var plano = new Dictionary<string, string> { ["1.1.01"] = "D" };
        var balancete = new[]
        {
            Linha("1.1.01", debito: 200, credito: 150),
            Linha("1.1.01", debito: 150, credito: 150), // igual não vira
        };
        Assert.Empty(_svc.DetectarContasViradas(balancete, plano));
    }

    [Fact]
    public void Credora_vira_quando_debito_maior_que_credito()
    {
        var plano = new Dictionary<string, string> { ["2.1.01"] = "C" };
        var balancete = new[] { Linha("2.1.01", debito: 300, credito: 100) };
        Assert.Single(_svc.DetectarContasViradas(balancete, plano));
    }

    [Fact]
    public void Credora_nao_vira_quando_credito_maior_ou_igual()
    {
        var plano = new Dictionary<string, string> { ["2.1.01"] = "C" };
        var balancete = new[]
        {
            Linha("2.1.01", debito: 100, credito: 300),
            Linha("2.1.01", debito: 300, credito: 300),
        };
        Assert.Empty(_svc.DetectarContasViradas(balancete, plano));
    }

    [Fact]
    public void Conta_fora_do_plano_e_ignorada()
    {
        var plano = new Dictionary<string, string> { ["1.1.01"] = "D" };
        var balancete = new[] { Linha("9.9.99", debito: 0, credito: 999) };
        Assert.Empty(_svc.DetectarContasViradas(balancete, plano));
    }

    [Fact]
    public void Debito_e_credito_nulos_tratados_como_zero()
    {
        var plano = new Dictionary<string, string> { ["1.1.01"] = "D", ["2.1.01"] = "C" };
        var balancete = new[]
        {
            Linha("1.1.01", debito: null, credito: 10),  // D: 10 > 0 → vira
            Linha("2.1.01", debito: null, credito: null), // C: 0 > 0 falso → não vira
        };
        var viradas = _svc.DetectarContasViradas(balancete, plano);
        Assert.Single(viradas);
        Assert.Equal("1.1.01", viradas[0].Conta);
    }

    [Fact]
    public void Multiplas_viradas_preservam_ordem_do_balancete()
    {
        var plano = new Dictionary<string, string> { ["A"] = "D", ["B"] = "C" };
        var balancete = new[]
        {
            Linha("A", debito: 1, credito: 9),
            Linha("B", debito: 9, credito: 1),
        };
        var viradas = _svc.DetectarContasViradas(balancete, plano);
        Assert.Equal(new[] { "A", "B" }, viradas.Select(v => v.Conta).ToArray());
    }
}
