using System.Diagnostics;
using InfraControl.Application.AI.Context;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.AI.Chat;

/// <summary>Turno da conversa (papel: <c>user</c> ou <c>assistant</c>).</summary>
public sealed record ChatTurn(string Role, string Content);

/// <summary>Evidência devolvida ao front (§19 — fonte, citação, confiança).</summary>
public sealed record AiChatEvidenceDto(
    string Source,
    string SourceType,
    string Citation,
    string Confidence,
    DateTime TimestampUtc);

/// <summary>Resposta do chat grounded (ADR-052 §5).</summary>
public sealed record AiChatResponse(
    string Answer,
    IReadOnlyList<AiChatEvidenceDto> Evidences,
    string Provider,
    string Model,
    bool UsedFallback);

/// <summary>
/// Chat do Copilot (ADR-052): LEVEL 0 somente leitura, grounding pelo
/// Context Engine (ADR-038) e capacidade <c>Copilot</c> no gateway
/// (ADR-036/§15). Read (ADR-040 §3/§41): a IA enxerga o que o usuário
/// enxerga — qualquer role autenticada conversa.
/// </summary>
public sealed record SendAiChatCommand(IReadOnlyList<ChatTurn> Messages)
    : BaseCommand<AiChatResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Read;
}

public sealed class SendAiChatCommandHandler(
    IRetrievalService retrieval,
    IContextBuilder contextBuilder,
    IAiGateway gateway,
    ICurrentUserContext currentUser) : IRequestHandler<SendAiChatCommand, AiChatResponse>
{
    private const int MaxTurns = 20;
    private const int RetrievalLimit = 10;

    public async Task<AiChatResponse> Handle(
        SendAiChatCommand request,
        CancellationToken cancellationToken)
    {
        Validate(request.Messages);

        var userMessage = request.Messages[^1].Content;
        var traceId = Activity.Current?.TraceId.ToString();

        // Context Engine (ADR-038 · §19): busca híbrida da pergunta →
        // EvidenceSet com confiança/citação; vazio = sem contexto (§44).
        var retrievalResult = await retrieval.RetrieveAsync(
            new RetrievalRequest(userMessage, Limit: RetrievalLimit),
            cancellationToken);
        var evidenceSet = contextBuilder.Build(
            retrievalResult.Hits, traceId, tool: "copilot-chat");

        var completion = await gateway.CompleteAsync(
            new AiCompletionRequest(
                AiCapability.Copilot,
                BuildPrompt(request.Messages),
                BuildSystemPrompt(evidenceSet),
                TraceId: traceId,
                Actor: currentUser.UserName),
            cancellationToken);

        return new AiChatResponse(
            completion.Text,
            evidenceSet.Evidences
                .Select(evidence => new AiChatEvidenceDto(
                    evidence.Source.ToString(),
                    evidence.SourceType,
                    evidence.Citation,
                    evidence.Confidence.ToString(),
                    evidence.TimestampUtc))
                .ToList(),
            completion.ProviderName,
            completion.Model,
            completion.UsedFallback);
    }

    private static void Validate(IReadOnlyList<ChatTurn>? messages)
    {
        if (messages is null || messages.Count == 0)
        {
            throw new ArgumentException("A conversa não pode ser vazia.");
        }

        if (messages.Count > MaxTurns)
        {
            throw new ArgumentException(
                $"A conversa excede o limite de {MaxTurns} turnos.");
        }

        foreach (var turn in messages)
        {
            if (string.IsNullOrWhiteSpace(turn.Content))
            {
                throw new ArgumentException("Cada turno da conversa precisa de conteúdo.");
            }

            if (turn.Role is not ("user" or "assistant"))
            {
                throw new ArgumentException(
                    $"Papel inválido na conversa: '{turn.Role}'. Use 'user' ou 'assistant'.");
            }
        }

        if (messages[^1].Role != "user")
        {
            throw new ArgumentException("O último turno precisa ser do usuário.");
        }
    }

    /// <summary>
    /// System prompt LEVEL 0 (ADR-052 §1/§4 · ADR-040 §1): grounded,
    /// citação obrigatória, sem invenção, sem ação de escrita.
    /// </summary>
    private static string BuildSystemPrompt(EvidenceSet evidenceSet)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("Você é o Copilot de operações de TI do InfraControl.");
        builder.AppendLine(
            "Nível de autonomia: LEVEL 0 — somente leitura: responda, explique e cite evidências; "
            + "não execute ações e não proponha alterações de configuração.");
        builder.AppendLine("Regras:");
        builder.AppendLine(
            "- Use PRIMARIAMENTE as Evidências abaixo e cite-as entre colchetes, ex.: [E1].");
        builder.AppendLine(
            "- Se as evidências não responderem, diga explicitamente que não encontrou "
            + "contexto suficiente — NUNCA invente fatos, IPs, incidentes ou procedimentos.");
        builder.AppendLine("- Responda em português do Brasil, de forma direta e operacional.");

        if (evidenceSet.IsEmpty)
        {
            builder.AppendLine();
            builder.AppendLine(
                "Evidências: NENHUMA — declare que não há contexto recuperado para a pergunta.");
            return builder.ToString();
        }

        builder.AppendLine();
        builder.AppendLine("Evidências:");
        for (var index = 0; index < evidenceSet.Evidences.Count; index++)
        {
            var evidence = evidenceSet.Evidences[index];
            builder.AppendLine(
                $"[E{index + 1}] ({evidence.Source} · {evidence.Confidence} · "
                + $"{evidence.TimestampUtc:yyyy-MM-dd HH:mm}Z) {evidence.Citation} — {evidence.Data}");
        }

        return builder.ToString();
    }

    /// <summary>Histórico + pergunta atual numa única mensagem (§23).</summary>
    private static string BuildPrompt(IReadOnlyList<ChatTurn> messages)
    {
        var builder = new System.Text.StringBuilder();

        if (messages.Count > 1)
        {
            builder.AppendLine("Histórico da conversa:");
            foreach (var turn in messages.Take(messages.Count - 1))
            {
                builder.AppendLine(
                    $"{(turn.Role == "user" ? "Usuário" : "Assistente")}: {turn.Content}");
            }

            builder.AppendLine();
        }

        builder.Append("Pergunta atual do usuário: ").Append(messages[^1].Content);
        return builder.ToString();
    }
}
