using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.Application.AI;

/// <summary>
/// AI Gateway mínimo (FAI-T004/S3 · ADR-036 · docs/34 §9–§10/§54/§55):
///
/// - única porta de entrada para LLM (o router escolhe provider/modelo
///   por capacidade — o chamador não escolhe o modelo);
/// - execução pelo <see cref="IResilienceExecutor"/> já existente, com
///   policy por provider (<c>ai/{provider}</c>), timeout e retry das
///   <see cref="AiGatewayOptions"/> (§55) + circuit breaker do executor;
/// - fallback §54: se o primário falhar (erro/timeout) e houver
///   fallback habilitado, tenta o secundário; cancelamento do chamador
///   NUNCA vira fallback — propaga como <see cref="OperationCanceledException"/>;
/// - exatamente UMA entrada de auditoria por request (sucesso ou
///   falha), emitida pelo hook <see cref="IAiAuditSink"/> antes de
///   devolver; o hook é fail-open (falha de observabilidade não derruba
///   a chamada). Persistência da auditoria é do S6 (ADR-040).
///
/// O registro de providers/router ainda não está no DI (S4 — chega com
/// o <c>ConfigurationProviderRegistry</c>); aqui a classe nasce com
/// injeção direta para ser testável sem Docker.
/// </summary>
public sealed class AiGateway : IAiGateway
{
    private readonly IAiProviderRouter _router;
    private readonly IAiProviderRegistry _registry;
    private readonly IResilienceExecutor _resilience;
    private readonly IAiAuditSink _audit;
    private readonly AiGatewayOptions _options;
    private readonly ILogger<AiGateway> _logger;

    public AiGateway(
        IAiProviderRouter router,
        IAiProviderRegistry registry,
        IResilienceExecutor resilience,
        IAiAuditSink audit,
        AiGatewayOptions? options = null,
        ILogger<AiGateway>? logger = null)
    {
        _router = router;
        _registry = registry;
        _resilience = resilience;
        _audit = audit;
        _options = options ?? new AiGatewayOptions();
        _logger = logger ?? NullLogger<AiGateway>.Instance;
    }

    public async Task<AiCompletion> CompleteAsync(
        AiCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Prompt))
            throw new ArgumentException("O prompt não pode ser vazio.", nameof(request));

        var started = Stopwatch.GetTimestamp();
        AiProviderSelection? selection = null;
        string? providerUsed = null;
        string? modelUsed = null;
        var usedFallback = false;

        try
        {
            selection = _router.Resolve(request.Capability);
            providerUsed = selection.Provider;
            modelUsed = selection.Model;

            AiProviderResult result;
            try
            {
                result = await ExecuteAsync(selection.Provider, selection.Model, request, cancellationToken);
            }
            catch (Exception primaryFailure)
                when (primaryFailure is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                // §54: erro/timeout do primário → fallback (se habilitado
                // e configurado); senão erro claro com a causa preservada.
                if (!_options.FallbackEnabled
                    || selection.FallbackProvider is null
                    || selection.FallbackModel is null)
                {
                    throw new AiGatewayException(
                        $"Falha na chamada de IA ({selection.Provider}, capacidade {request.Capability}).",
                        primaryFailure);
                }

                usedFallback = true;
                providerUsed = selection.FallbackProvider;
                modelUsed = selection.FallbackModel;

                try
                {
                    result = await ExecuteAsync(
                        selection.FallbackProvider, selection.FallbackModel, request, cancellationToken);
                }
                catch (Exception fallbackFailure)
                    when (fallbackFailure is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    throw new AiGatewayException(
                        $"Falha na chamada de IA no fallback ({selection.FallbackProvider}, capacidade {request.Capability}).",
                        fallbackFailure);
                }
            }

            var completion = new AiCompletion(
                result.Text,
                providerUsed,
                modelUsed,
                result.PromptTokens,
                result.CompletionTokens,
                usedFallback);

            await TryRecordAsync(BuildEntry(
                request, providerUsed, modelUsed, usedFallback,
                success: true, error: null,
                result.PromptTokens, result.CompletionTokens, started));

            return completion;
        }
        catch (Exception failure)
        {
            await TryRecordAsync(BuildEntry(
                request, providerUsed, modelUsed, usedFallback,
                success: false, error: failure.Message,
                promptTokens: null, completionTokens: null, started));

            throw;
        }
    }

    private async Task<AiProviderResult> ExecuteAsync(
        string providerName,
        string model,
        AiCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var provider = _registry.Get(providerName);
        var providerRequest = request with { Model = model };

        // Policy por provider: o circuit breaker fica isolado por
        // provedor (ResilienceExecutor usa o nome como chave).
        return await _resilience.ExecuteAsync(
            $"ai/{providerName}",
            token => provider.CompleteAsync(providerRequest, token),
            new ResilienceOptions(_options.MaxRetries, _options.EffectiveTimeout),
            cancellationToken);
    }

    private AiAuditEntry BuildEntry(
        AiCompletionRequest request,
        string? providerName,
        string? model,
        bool usedFallback,
        bool success,
        string? error,
        long? promptTokens,
        long? completionTokens,
        long started) =>
        new(
            request.Capability,
            providerName,
            model,
            success,
            usedFallback,
            Stopwatch.GetElapsedTime(started),
            promptTokens,
            completionTokens,
            error,
            request.TraceId,
            DateTimeOffset.UtcNow);

    private async Task TryRecordAsync(AiAuditEntry entry)
    {
        // Fail-open documentado (ADR-036/§41): o hook de observabilidade
        // não pode derrubar a chamada de IA.
        try
        {
            await _audit.RecordAsync(entry, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "AI audit sink falhou para {Capability} ({Provider}).",
                entry.Capability,
                entry.ProviderName);
        }
    }
}
