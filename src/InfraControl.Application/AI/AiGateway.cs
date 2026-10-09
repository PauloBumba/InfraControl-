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
///   a chamada) e, desde o S6, persiste a ação <c>AI.*</c> na auditoria
///   de domínio existente (<c>AuditAiAuditSink</c> · ADR-040);
/// - limites §55 (S6): prompt acima de <c>MaxPromptCharacters</c> é
///   recusado antes de gastar tokens (auditorado) e
///   <c>MaxCompletionTokens</c> vira <c>num_predict</c> no provider
///   quando o chamador não define geração.
///
/// Providers/router chegam por DI (S4 — <c>ConfigurationProviderRegistry</c>).
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
        using var activity = AiDiagnostics.Source.StartActivity("ai.complete");
        AiDiagnosticsTag(activity, request, request.TraceId);
        AiProviderSelection? selection = null;
        string? providerUsed = null;
        string? modelUsed = null;
        var usedFallback = false;
        string? fallbackReason = null;

        try
        {
            // §55: limite de tamanho do prompt — recusa ANTES de gastar
            // tokens, com auditoria (o throw cai no catch e é registrado).
            if (request.Prompt.Length > _options.MaxPromptCharacters)
            {
                throw new AiGatewayException(
                    $"Prompt excede o limite de {_options.MaxPromptCharacters} caracteres (§55).");
            }

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
                // §54: registrar o motivo da troca (vira FallbackReason
                // na auditoria).
                fallbackReason = primaryFailure.Message;
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

            AiDiagnosticsFinish(activity, request.Capability, providerUsed, modelUsed,
                usedFallback, success: true, error: null);
            RecordTelemetry(request.Capability, providerUsed, modelUsed, usedFallback,
                success: true, result.PromptTokens, result.CompletionTokens, started);

            await TryRecordAsync(BuildEntry(
                request, providerUsed, modelUsed, usedFallback,
                success: true, error: null,
                result.PromptTokens, result.CompletionTokens, started, fallbackReason));

            return completion;
        }
        catch (Exception failure)
        {
            AiDiagnosticsFinish(activity, request.Capability, providerUsed, modelUsed,
                usedFallback, success: false, error: failure.Message);
            RecordTelemetry(request.Capability, providerUsed, modelUsed, usedFallback,
                success: false, promptTokens: null, completionTokens: null, started);

            await TryRecordAsync(BuildEntry(
                request, providerUsed, modelUsed, usedFallback,
                success: false, error: failure.Message,
                promptTokens: null, completionTokens: null, started, fallbackReason));

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

        // §55: limite de conclusão — quando o chamador não especificou,
        // o cap do gateway (Ai:Gateway:MaxCompletionTokens) vira o
        // MaxTokens da geração (provider mapeia para num_predict).
        var providerRequest = request with
        {
            Model = model,
            Generation = request.Generation ?? EffectiveGenerationOptions(),
        };

        // Policy por provider: o circuit breaker fica isolado por
        // provedor (ResilienceExecutor usa o nome como chave).
        return await _resilience.ExecuteAsync(
            $"ai/{providerName}",
            token => provider.CompleteAsync(providerRequest, token),
            new ResilienceOptions(_options.MaxRetries, _options.EffectiveTimeout),
            cancellationToken);
    }

    // ── Limites §55 (S6) ───────────────────────────────────────────────
    private AiGenerationOptions? EffectiveGenerationOptions() =>
        _options.MaxCompletionTokens is int maxTokens
            ? new AiGenerationOptions(MaxTokens: maxTokens)
            : null;

    // ── Telemetria §43 (S5) ────────────────────────────────────────────
    private static void AiDiagnosticsTag(
        System.Diagnostics.Activity? activity,
        AiCompletionRequest request,
        string? traceId)
    {
        if (activity is null) return;
        activity.SetTag("ai.capability", request.Capability.ToString());
        activity.SetTag("ai.trace_id", traceId);
    }

    private static void AiDiagnosticsFinish(
        System.Diagnostics.Activity? activity,
        AiCapability capability,
        string? provider,
        string? model,
        bool usedFallback,
        bool success,
        string? error)
    {
        if (activity is null) return;
        activity.SetTag("ai.capability", capability.ToString());
        activity.SetTag("ai.provider", provider);
        activity.SetTag("ai.model", model);
        activity.SetTag("ai.used_fallback", usedFallback);
        if (success)
        {
            activity.SetStatus(System.Diagnostics.ActivityStatusCode.Ok);
        }
        else
        {
            activity.SetStatus(System.Diagnostics.ActivityStatusCode.Error, error);
        }
    }

    private static void RecordTelemetry(
        AiCapability capability,
        string? provider,
        string? model,
        bool usedFallback,
        bool success,
        long? promptTokens,
        long? completionTokens,
        long started)
    {
        var tags = new System.Diagnostics.TagList
        {
            { "ai.capability", capability.ToString() },
            { "ai.provider", provider ?? "none" },
            { "ai.model", model ?? "none" },
            { "ai.used_fallback", usedFallback },
            { "ai.success", success },
        };

        AiDiagnostics.Requests.Add(1, tags);
        AiDiagnostics.DurationMs.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds, tags);
        if (promptTokens is > 0)
            AiDiagnostics.PromptTokens.Add(promptTokens.Value, tags);
        if (completionTokens is > 0)
            AiDiagnostics.CompletionTokens.Add(completionTokens.Value, tags);
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
        long started,
        string? fallbackReason) =>
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
            DateTimeOffset.UtcNow,
            request.Actor,
            fallbackReason);

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
