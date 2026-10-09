using InfraControl.Application.AI;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace InfraControl.Infrastructure.AI;

/// <summary>
/// Hook de auditoria do gateway (FAI-T007/S6 · ADR-040 · docs/34 §41/
/// §43/§54/§55): UMA entrada por request de IA, gravada no MECanismo
/// de auditoria de domínio existente (F0-T014 — <c>IAuditWriter</c> +
/// <c>IUnitOfWork</c>, sem migração nova) e emitida no log
/// estruturado.
///
/// - action = <c>AI.{capacidade}</c> (prefixo <c>AI.*</c> do ADR-040);
/// - resource = <c>{provider}/{modelo}</c>;
/// - outcome = <c>Succeeded</c> / <c>SucceededViaFallback</c> /
///   <c>Failed</c>;
/// - actorId = quem perguntou (§41, repassado pelo chamador);
/// - correlationId = traceId (§43);
/// - error = motivo da falha OU "Fallback após: …" (§54 — registrar o
///   motivo da troca).
///
/// O gateway envolve este hook em try/catch (fail-open): falha de
/// auditoria não derruba a chamada de IA (testado no S3 e re-testado
/// aqui com o sink real).
/// </summary>
public sealed class AuditAiAuditSink(
    IAuditWriter auditWriter,
    IUnitOfWork unitOfWork,
    ILogger<AuditAiAuditSink> logger) : IAiAuditSink
{
    public async Task RecordAsync(AiAuditEntry entry, CancellationToken cancellationToken = default)
    {
        var action = $"AI.{entry.Capability}";
        var resource = entry.ProviderName is null
            ? "não configurado"
            : $"{entry.ProviderName}/{entry.Model}";
        var outcome = !entry.Success
            ? "Failed"
            : entry.UsedFallback ? "SucceededViaFallback" : "Succeeded";
        var error = !entry.Success
            ? entry.Error
            : entry.UsedFallback && entry.FallbackReason is not null
                ? $"Fallback após: {entry.FallbackReason}"
                : null;

        if (entry.Success)
        {
            logger.LogInformation(
                "AI {Action} via {Resource} em {LatencyMs} ms (fallback={UsedFallback}, tokens={PromptTokens}/{CompletionTokens}, actor={Actor}, trace={TraceId})",
                action, resource, entry.Latency.TotalMilliseconds, entry.UsedFallback,
                entry.PromptTokens, entry.CompletionTokens, entry.Actor, entry.TraceId);
        }
        else
        {
            logger.LogWarning(
                "AI {Action} FALHOU ({Resource}) em {LatencyMs} ms: {Error} (actor={Actor}, trace={TraceId})",
                action, resource, entry.Latency.TotalMilliseconds, error,
                entry.Actor, entry.TraceId);
        }

        await auditWriter.WriteAsync(
            new AuditEntry(
                action,
                resource,
                outcome,
                actorId: entry.Actor,
                correlationId: entry.TraceId,
                error: error),
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
