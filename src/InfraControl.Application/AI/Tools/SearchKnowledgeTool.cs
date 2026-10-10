using InfraControl.Application.Knowledge.Articles;
using InfraControl.Application.Knowledge.Articles.Queries;
using MediatR;

namespace InfraControl.Application.AI.Tools;

/// <summary>
/// Tool <c>search_knowledge</c> (FAI-T008/S7 · docs/34 §21/§52):
/// busca artigos publicados de Knowledge pela query MediatR existente
/// (<see cref="SearchKnowledgeArticlesQuery"/> — §20: Tool →
/// Application Service). Nível Read; conteúdo tratado como DADOS
/// (§42 — nunca instrução).
/// </summary>
public sealed class SearchKnowledgeTool(IMediator mediator) : IAiTool
{
    public AiToolDescriptor Descriptor { get; } = new(
        Name: "search_knowledge",
        Description: "Busca artigos de Knowledge por termo (título/conteúdo), retornando título, status e resumo.",
        Access: AiToolAccess.Read,
        Timeout: TimeSpan.FromSeconds(10),
        MaxResults: 20,
        InputSchema: """{"term": "string — termo de busca (obrigatório)", "limit?": "int — máximo de artigos (≤ 20)"}""",
        OutputSchema: """{"findings[]": [{"id", "title", "status", "detail=resumo"}], "total", "source", "timestamp"}""",
        AuditPolicy: "AI.Tool.* (S6 — auditoria de domínio)",
        Source: "Knowledge/SearchKnowledgeArticlesQuery");

    public async Task<AiToolResult> ExecuteAsync(
        AiToolArguments arguments,
        CancellationToken cancellationToken = default)
    {
        var term = arguments.Get("term");
        if (string.IsNullOrWhiteSpace(term))
            throw new AiToolArgumentException("Argumento 'term' é obrigatório para search_knowledge.");

        var limit = Math.Clamp(arguments.GetInt("limit") ?? Descriptor.MaxResults, 1, Descriptor.MaxResults);

        var page = await mediator.Send(
            new SearchKnowledgeArticlesQuery(Term: term.Trim(), Page: 1, PageSize: limit),
            cancellationToken);

        var findings = page.Items
            .Select(article => new AiToolFinding(
                Id: article.Id.ToString(),
                Title: article.Title,
                Status: article.Status.ToString(),
                Detail: Excerpt(article.Content)))
            .ToList();

        return new AiToolResult(
            Descriptor.Name,
            findings,
            page.TotalCount,
            Descriptor.Source,
            DateTimeOffset.UtcNow);
    }

    private static string? Excerpt(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        const int maxLength = 200;
        var normalized = content.Trim();
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength] + "…";
    }
}
