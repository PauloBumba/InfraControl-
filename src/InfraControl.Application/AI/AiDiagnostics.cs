using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace InfraControl.Application.AI;

/// <summary>
/// Telemetria da camada de IA (FAI-T006/S5 · docs/34 §43).
///
/// Fonte <c>InfraControl.AI</c> — o OTel da API já captura tudo sob o
/// padrão <c>InfraControl.*</c> (Program.cs: <c>AddSource</c>/
/// <c>AddMeter</c>), sem mudança de collector. Um <c>Activity</c>
/// (<c>ai.complete</c>) por request do gateway, com as tags da §43
/// (capability, provider, model, fallback, tokens, latência, sucesso)
/// para responder "por que essa chamada demorou 14 segundos?".
///
/// Métricas (§43/§55): <c>ai.requests</c> (contagem por
/// capability/provider/sucesso), <c>ai.request.duration</c> (latência ms),
/// <c>ai.tokens.prompt</c>/<c>ai.tokens.completion</c> (uso de tokens).
/// </summary>
public static class AiDiagnostics
{
    public const string SourceName = "InfraControl.AI";

    public static readonly ActivitySource Source = new(SourceName, "1.0.0");

    public static readonly Meter Meter = new(SourceName, "1.0.0");

    public static readonly Counter<long> Requests = Meter.CreateCounter<long>(
        "ai.requests",
        unit: "{request}",
        description: "Requests de IA passando pelo AI Gateway.");

    public static readonly Histogram<double> DurationMs = Meter.CreateHistogram<double>(
        "ai.request.duration",
        unit: "ms",
        description: "Latência do AI Gateway (resolução + provider) em milissegundos.");

    public static readonly Counter<long> PromptTokens = Meter.CreateCounter<long>(
        "ai.tokens.prompt",
        unit: "{token}",
        description: "Tokens de prompt consumidos por capacidade/provider/modelo.");

    public static readonly Counter<long> CompletionTokens = Meter.CreateCounter<long>(
        "ai.tokens.completion",
        unit: "{token}",
        description: "Tokens de conclusão gerados por capacidade/provider/modelo.");
}
