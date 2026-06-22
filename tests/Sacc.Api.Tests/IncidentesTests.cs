using Sacc.Api.Features.Worker;
using Xunit;

namespace Sacc.Api.Tests;

public sealed class IncidentesTests
{
    [Fact]
    public void Timeout_e_classificado_antes_de_conexao()
    {
        var (tipo, _) = Incidentes.Classificar(new TimeoutException("operação expirou"));
        Assert.Equal("timeout", tipo);

        var (tipo2, _) = Incidentes.Classificar(new Exception("Connection timeout expired"));
        Assert.Equal("timeout", tipo2);
    }

    [Fact]
    public void Config_ausente_classificada()
    {
        var (tipo, msg) = Incidentes.Classificar(new ConfigAusenteException("Nenhum período cadastrado."));
        Assert.Equal("configuracao_ausente", tipo);
        Assert.Equal("Nenhum período cadastrado.", msg);
    }

    [Fact]
    public void Excecao_generica_e_nao_tratada()
    {
        var (tipo, _) = Incidentes.Classificar(new InvalidOperationException("algo quebrou"));
        Assert.Equal("excecao_nao_tratada", tipo);
    }

    [Theory]
    [InlineData("erp_conexao", "Validar disponibilidade do ambiente ERP/TOTVS.")]
    [InlineData("view_leitura", "Validar a view contábil e a disponibilidade do ERP/TOTVS.")]
    [InlineData("postgres_conexao", "Validar disponibilidade do banco de configuração.")]
    [InlineData("email_envio", "Validar credenciais e disponibilidade do servidor SMTP.")]
    [InlineData("configuracao_ausente", "Cadastrar período de verificação e/ou template de e-mail.")]
    [InlineData("timeout", "Analisar os logs da aplicação e o stack trace para diagnóstico.")]
    [InlineData("excecao_nao_tratada", "Analisar os logs da aplicação e o stack trace para diagnóstico.")]
    public void Acao_recomendada_texto_exato(string erroTipo, string esperado)
    {
        Assert.Equal(esperado, Incidentes.AcaoRecomendada(erroTipo));
    }
}
