namespace Sacc.Api.Features.EmailTemplates;

public sealed record EmailTemplateCreate(string CorpoHtml);

public sealed record EmailTemplateSummary(Guid Id, int Versao, DateTime CriadoEm, string CriadoPor);

public sealed record EmailTemplateResponse(Guid Id, string CorpoHtml, int Versao, DateTime CriadoEm, string CriadoPor);
