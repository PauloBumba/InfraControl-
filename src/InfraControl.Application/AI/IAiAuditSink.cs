namespace InfraControl.Application.AI;

/// <summary>
/// Hook de auditoria por request de IA (ADR-036/040 · docs/34 §41/§43).
/// O gateway chama ANTES de devolver, com exatamente 1 entrada por
/// request (sucesso ou falha). O hook é fail-open: uma falha do sink não
/// derruba a chamada. S3 entrega apenas o contrato — a persistência
/// (ação <c>AI.*</c> na auditoria existente) é do S6.
/// </summary>
public interface IAiAuditSink
{
    Task RecordAsync(AiAuditEntry entry, CancellationToken cancellationToken = default);
}
