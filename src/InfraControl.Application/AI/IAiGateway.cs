namespace InfraControl.Application.AI;

/// <summary>
/// AI Gateway (ADR-036): único caminho da aplicação para um LLM.
/// Pipeline por chamada: capacidade → router (provider/modelo) →
/// resiliência (timeout/retry) → provider → fallback (§54) →
/// hook de auditoria (exatamente 1 entrada por request).
/// </summary>
public interface IAiGateway
{
    Task<AiCompletion> CompleteAsync(
        AiCompletionRequest request,
        CancellationToken cancellationToken = default);
}
