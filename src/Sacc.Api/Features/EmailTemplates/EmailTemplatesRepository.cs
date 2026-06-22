using Dapper;
using Sacc.Api.Core.Db;

namespace Sacc.Api.Features.EmailTemplates;

public sealed class EmailTemplatesRepository(IDbConnectionFactory factory)
{
    public async Task<IReadOnlyList<EmailTemplate>> ListarAsync(CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        var rows = await conn.QueryAsync<EmailTemplate>("SELECT * FROM email_templates ORDER BY versao DESC");
        return rows.AsList();
    }

    /// <summary>Template ativo = maior versão.</summary>
    public async Task<EmailTemplate?> GetAtivoAsync(CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<EmailTemplate>(
            "SELECT * FROM email_templates ORDER BY versao DESC LIMIT 1");
    }

    /// <summary>Cria com versão = MAX(versao)+1 (replica a race condition existente).</summary>
    public async Task<EmailTemplate> CriarAsync(string corpoHtml, string criadoPor, DateTime criadoEm, CancellationToken ct = default)
    {
        using var conn = await factory.OpenPostgresAsync(ct);
        var proximaVersao = await conn.ExecuteScalarAsync<int>(
            "SELECT COALESCE(MAX(versao), 0) + 1 FROM email_templates");

        var entity = new EmailTemplate
        {
            Id = Guid.NewGuid(),
            CorpoHtml = corpoHtml,
            Versao = proximaVersao,
            CriadoEm = criadoEm,
            CriadoPor = criadoPor,
        };
        await conn.ExecuteAsync(
            @"INSERT INTO email_templates (id, corpo_html, versao, criado_em, criado_por)
              VALUES (@Id, @CorpoHtml, @Versao, @CriadoEm, @CriadoPor)", entity);
        return entity;
    }
}
