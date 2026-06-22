using System.Net;
using System.Text;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Sacc.Api.Core;

namespace Sacc.Api.Features.Worker;

/// <summary>Contexto do e-mail operacional de incidente.</summary>
public sealed record IncidentEmailContext(
    string TipoExecucao,
    string Ambiente,
    string Erro,
    string Detalhes,
    string AcaoRecomendada,
    DateTime DataHora);

public interface IEmailService
{
    /// <summary>Envia o alerta de negócio (contas viradas). Nunca lança; retorna sucesso.</summary>
    Task<bool> EnviarAlertaAsync(IReadOnlyList<string> destinatarios, string corpoHtmlTemplate,
        DateTime dataExecucao, IReadOnlyList<ContaViradaJson> contasViradas, CancellationToken ct = default);

    /// <summary>Envia o alerta operacional de falha. Nunca lança; retorna sucesso.</summary>
    Task<bool> EnviarAlertaIncidenteAsync(IReadOnlyList<string> destinatarios, IncidentEmailContext contexto,
        CancellationToken ct = default);
}

public sealed class EmailService(AppSettings settings, ILogger<EmailService> logger) : IEmailService
{
    public async Task<bool> EnviarAlertaAsync(IReadOnlyList<string> destinatarios, string corpoHtmlTemplate,
        DateTime dataExecucao, IReadOnlyList<ContaViradaJson> contasViradas, CancellationToken ct = default)
    {
        if (destinatarios.Count == 0) return false;

        var dataFmt = FormatarData(dataExecucao);
        var corpo = RenderizarTemplate(corpoHtmlTemplate, dataFmt, contasViradas);
        var assunto = $"[SACC] Alerta — {contasViradas.Count} conta(s) virada(s) em {dataFmt}";
        return await EnviarAsync(destinatarios, assunto, corpo, ct);
    }

    public async Task<bool> EnviarAlertaIncidenteAsync(IReadOnlyList<string> destinatarios, IncidentEmailContext c,
        CancellationToken ct = default)
    {
        if (destinatarios.Count == 0) return false;

        var corpo = RenderizarIncidente(c);
        const string assunto = "[ALERTA] Falha na execução do Monitor de Contas Viradas";
        return await EnviarAsync(destinatarios, assunto, corpo, ct);
    }

    private async Task<bool> EnviarAsync(IReadOnlyList<string> destinatarios, string assunto, string corpoHtml, CancellationToken ct)
    {
        try
        {
            var msg = new MimeMessage();
            msg.From.Add(MailboxAddress.Parse(settings.SmtpFrom));
            foreach (var dest in destinatarios)
                msg.To.Add(MailboxAddress.Parse(dest));
            msg.Subject = assunto;
            msg.Body = new BodyBuilder { HtmlBody = corpoHtml }.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort, SecureSocketOptions.StartTls, ct);
            if (!string.IsNullOrEmpty(settings.SmtpUser) || !string.IsNullOrEmpty(settings.SmtpPassword))
                await client.AuthenticateAsync(settings.SmtpUser, settings.SmtpPassword, ct);
            await client.SendAsync(msg, ct);
            await client.DisconnectAsync(true, ct);
            return true;
        }
        catch (AuthenticationException ex)
        {
            logger.LogError(ex, "email_erro_autenticacao");
            return false;
        }
        catch (SmtpCommandException ex)
        {
            logger.LogError(ex, "email_erro_smtp");
            return false;
        }
        catch (SmtpProtocolException ex)
        {
            logger.LogError(ex, "email_erro_smtp");
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "email_erro_inesperado");
            return false;
        }
    }

    private static string FormatarData(DateTime data) => data.ToString("dd/MM/yyyy HH:mm");

    private static string RenderizarTemplate(string template, string dataFmt, IReadOnlyList<ContaViradaJson> contas)
    {
        return template
            .Replace("{{data_execucao}}", dataFmt)
            .Replace("{{quantidade_contas}}", contas.Count.ToString())
            .Replace("{{tabela_contas_viradas}}", MontarTabela(contas));
    }

    private static string MontarTabela(IReadOnlyList<ContaViradaJson> contas)
    {
        var sb = new StringBuilder();
        sb.Append("<table style=\"width:100%;border-collapse:collapse;font-size:14px;\">");
        sb.Append("<thead><tr>");
        sb.Append("<th style=\"background-color:#1F3864;color:#ffffff;text-align:left;padding:8px;border:1px solid #1F3864;\">Código da Conta</th>");
        sb.Append("<th style=\"background-color:#1F3864;color:#ffffff;text-align:left;padding:8px;border:1px solid #1F3864;\">Descrição</th>");
        sb.Append("</tr></thead><tbody>");
        foreach (var c in contas)
        {
            sb.Append("<tr>");
            sb.Append($"<td style=\"padding:8px;border:1px solid #d0d0d0;\">{WebUtility.HtmlEncode(c.Conta)}</td>");
            sb.Append($"<td style=\"padding:8px;border:1px solid #d0d0d0;\">{WebUtility.HtmlEncode(c.Descricao)}</td>");
            sb.Append("</tr>");
        }
        sb.Append("</tbody></table>");
        return sb.ToString();
    }

    private static string RenderizarIncidente(IncidentEmailContext c)
    {
        string Row(string label, string value) =>
            $"<tr><td style=\"padding:8px;border:1px solid #d0d0d0;font-weight:bold;background-color:#f4f4f7;\">{WebUtility.HtmlEncode(label)}</td>" +
            $"<td style=\"padding:8px;border:1px solid #d0d0d0;\"><pre style=\"margin:0;white-space:pre-wrap;font-family:inherit;\">{WebUtility.HtmlEncode(value)}</pre></td></tr>";

        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:Arial,Helvetica,sans-serif;color:#1a1a1a;\">");
        sb.Append("<h2 style=\"color:#b00020;\">Falha na execução do Monitor de Contas Viradas</h2>");
        sb.Append("<table style=\"width:100%;border-collapse:collapse;font-size:14px;\">");
        sb.Append(Row("Data/Hora", c.DataHora.ToString("dd/MM/yyyy HH:mm")));
        sb.Append(Row("Tipo da Execução", c.TipoExecucao));
        sb.Append(Row("Ambiente", c.Ambiente));
        sb.Append(Row("Erro", c.Erro));
        sb.Append(Row("Detalhes", c.Detalhes));
        sb.Append(Row("Ação Recomendada", c.AcaoRecomendada));
        sb.Append("</table></div>");
        return sb.ToString();
    }
}
