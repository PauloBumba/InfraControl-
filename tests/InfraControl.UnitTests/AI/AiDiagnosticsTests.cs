using System.Diagnostics;
using System.Diagnostics.Metrics;
using FluentAssertions;
using InfraControl.Application.AI;

namespace InfraControl.UnitTests.AI;

/// <summary>
/// FAI-T006/S5 (docs/34 §43 · ADR-036): o gateway emite UM activity
/// <c>ai.complete</c> pela fonte <c>InfraControl.AI</c> (capturada pelo
/// OTel via <c>AddSource("InfraControl.*")</c>) com as tags da §43 —
/// capability, provider, model, fallback, sucesso — e registra as
/// métricas <c>ai.requests</c>/<c>ai.tokens.*</c>. Sem Docker: fakes.
/// </summary>
public sealed class AiDiagnosticsTests
{
    private sealed record Observed(double Value, Dictionary<string, object?> Tags);

    private static (AiGateway Gateway, FakeAiProvider Provider, FakeAuditSink Audit) BuildGateway(
        AiProviderSelection? selection = null)
    {
        var provider = new FakeAiProvider("ollama");
        var audit = new FakeAuditSink();
        var gateway = new AiGateway(
            new FakeAiRouter(selection ?? new AiProviderSelection("ollama", "modelo-primario")),
            new FakeAiRegistry(provider),
            new FakeResilienceExecutor(),
            audit);
        return (gateway, provider, audit);
    }

    [Fact]
    public async Task CompleteAsync_emite_activity_ai_complete_com_tags_da_43()
    {
        var (gateway, _, _) = BuildGateway();
        using var listener = CaptureActivities(out var stopped);

        var completion = await gateway.CompleteAsync(
            new AiCompletionRequest(AiCapability.Copilot, "explique o incidente", TraceId: "trace-123"));

        completion.Text.Should().Be("resposta-ollama");
        var activity = stopped
            .Should().ContainSingle(candidate =>
                Equals(candidate.GetTagItem("ai.trace_id"), "trace-123"))
            .Which;
        activity.Source.Name.Should().Be("InfraControl.AI");
        activity.OperationName.Should().Be("ai.complete");
        activity.GetTagItem("ai.capability").Should().Be("Copilot");
        activity.GetTagItem("ai.provider").Should().Be("ollama");
        activity.GetTagItem("ai.model").Should().Be("modelo-primario");
        activity.GetTagItem("ai.used_fallback").Should().Be(false);
        activity.GetTagItem("ai.trace_id").Should().Be("trace-123");
        activity.Status.Should().Be(ActivityStatusCode.Ok);
    }

    [Fact]
    public async Task CompleteAsync_falha_sem_fallback_marca_activity_com_erro()
    {
        var provider = new FakeAiProvider("ollama") { Fault = new InvalidOperationException("boom") };
        var gateway = new AiGateway(
            new FakeAiRouter(new AiProviderSelection("ollama", "modelo-primario")),
            new FakeAiRegistry(provider),
            new FakeResilienceExecutor(),
            new FakeAuditSink());
        using var listener = CaptureActivities(out var stopped);

        var act = async () => await gateway.CompleteAsync(
            new AiCompletionRequest(AiCapability.IncidentAnalysis, "analise", TraceId: "trace-erro-1"));

        await act.Should().ThrowAsync<AiGatewayException>();

        var activity = stopped
            .Should().ContainSingle(candidate =>
                Equals(candidate.GetTagItem("ai.trace_id"), "trace-erro-1"))
            .Which;
        activity.Status.Should().Be(ActivityStatusCode.Error);
        activity.GetTagItem("ai.provider").Should().Be("ollama");
    }

    [Fact]
    public async Task CompleteAsync_incrementa_metrica_ai_requests_com_tags()
    {
        var (gateway, _, _) = BuildGateway();
        using var listener = CaptureMeasurements(out var observations);

        await gateway.CompleteAsync(new AiCompletionRequest(AiCapability.Reporting, "relatório"));

        observations.Any(observation =>
            observation.Tags.ContainsKey("ai.capability") &&
            Equals(observation.Tags["ai.capability"], "Reporting") &&
            observation.Tags.ContainsKey("ai.success") &&
            Equals(observation.Tags["ai.success"], true) &&
            observation.Tags.ContainsKey("ai.provider") &&
            Equals(observation.Tags["ai.provider"], "ollama"))
            .Should().BeTrue("a métrica ai.requests deve ser emitida com as tags §43");
    }

    [Fact]
    public async Task CompleteAsync_registra_tokens_de_prompt_e_conclusao()
    {
        var provider = new FakeAiProvider("ollama")
        {
            Handler = _ => new AiProviderResult("ok", PromptTokens: 12, CompletionTokens: 7),
        };
        var gateway = new AiGateway(
            new FakeAiRouter(new AiProviderSelection("ollama", "modelo-primario")),
            new FakeAiRegistry(provider),
            new FakeResilienceExecutor(),
            new FakeAuditSink());
        using var listener = CaptureMeasurements(out var observations);

        await gateway.CompleteAsync(new AiCompletionRequest(AiCapability.Copilot, "prompt"));

        observations.Any(observation =>
            observation.Tags.ContainsKey("metric") &&
            Equals(observation.Tags["metric"], "ai.tokens.prompt") && observation.Value == 12)
            .Should().BeTrue("ai.tokens.prompt deve registrar 12");
        observations.Any(observation =>
            observation.Tags.ContainsKey("metric") &&
            Equals(observation.Tags["metric"], "ai.tokens.completion") && observation.Value == 7)
            .Should().BeTrue("ai.tokens.completion deve registrar 7");
    }

    private static ActivityListener CaptureActivities(out List<Activity> stopped)
    {
        var captured = new List<Activity>();
        stopped = captured;
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AiDiagnostics.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => captured.Add(activity),
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static MeterListener CaptureMeasurements(out List<Observed> observations)
    {
        var captured = new List<Observed>();
        observations = captured;

        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == AiDiagnostics.SourceName)
                    meterListener.EnableMeasurementEvents(instrument);
            },
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
            Observe(instrument, measurement, tags));
        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
            Observe(instrument, measurement, tags));
        listener.Start();
        return listener;

        void Observe(Instrument instrument, double measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var tagMap = new Dictionary<string, object?> { ["metric"] = instrument.Name };
            foreach (var tag in tags)
                tagMap[tag.Key] = tag.Value;
            captured.Add(new Observed(measurement, tagMap));
        }
    }
}
