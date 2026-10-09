using InfraControl.Application.AI;
using Microsoft.Extensions.Logging;

namespace InfraControl.Infrastructure.AI;

/// <summary>
/// Hook de auditoria do gateway (ADR-036/040 · docs/34 §41/§43): emite
/// UMA entrada estruturada por request de IA no log — a observabilidade
/// já existente (Serilog → Seq/console → otel-collector) captura. A
/// persistência na auditoria de domínio (ação <c>AI.*</c>) é do S6,
/// quando o schema for decidido.
/// </summary>
public sealed class LoggingAiAuditSink(ILogger<LoggingAiAuditSink> logger) : IAiAuditSink
{
    public Task RecordAsync(AiAuditEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry.Success)
        {
            logger.LogInformation(
                "AI {Capability} via {Provider}/{Model} em {LatencyMs} ms (fallback={UsedFallback}, tokens={PromptTokens}/{CompletionTokens}, trace={TraceId})",
                entry.Capability,
                entry.ProviderName,
                entry.Model,
                entry.Latency.TotalMilliseconds,
                entry.UsedFallback,
                entry.PromptTokens,
                entry.CompletionTokens,
                entry.TraceId);
        }
        else
        {
            logger.LogWarning(
                "AI {Capability} FALHOU ({Provider}/{Model}) em {LatencyMs} ms: {Error} (trace={TraceId})",
                entry.Capability,
                entry.ProviderName,
                entry.Model,
                entry.Latency.TotalMilliseconds,
                entry.Error,
                entry.TraceId);
        }

        return Task.CompletedTask;
    }
}
