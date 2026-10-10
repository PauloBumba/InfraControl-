using FluentAssertions;
using InfraControl.Application.AI;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Infrastructure.AI;
using InfraControl.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.AI;

/// <summary>
/// FAI-T007/S6 (ADR-040 · docs/34 §41/§54): o hook de auditoria do
/// gateway persiste a ação <c>AI.*</c> no mecanismo de auditoria de
/// domínio existente (F0-T014 — sem migração) com ator (§41), trace
/// (§43), outcome e motivo de fallback (§54); e o fail-open do gateway
/// segura falha do sink REAL (não só do fake do S3). Sem Docker: fakes.
/// </summary>
public sealed class AuditAiAuditSinkTests
{
    private sealed class FakeAuditWriter : IAuditWriter
    {
        public List<AuditEntry> Entries { get; } = [];
        public Exception? Fault { get; set; }

        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
        {
            if (Fault is not null)
                return Task.FromException(Fault);
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(SaveCount);
        }

        public void Dispose() { }
    }

    private static AiAuditEntry Entry(
        bool success = true,
        bool usedFallback = false,
        string? fallbackReason = null,
        string? actor = null) =>
        new(
            AiCapability.Copilot,
            "ollama",
            "modelo-primario",
            success,
            usedFallback,
            TimeSpan.FromMilliseconds(120),
            PromptTokens: 10,
            CompletionTokens: 5,
            Error: success ? null : "boom",
            TraceId: "trace-1",
            TimestampUtc: DateTimeOffset.UtcNow,
            Actor: actor,
            FallbackReason: fallbackReason);

    [Fact]
    public async Task RecordAsync_grava_ac_AI_capacidade_na_auditoria_de_dominio()
    {
        var writer = new FakeAuditWriter();
        var unitOfWork = new FakeUnitOfWork();
        var sink = new AuditAiAuditSink(writer, unitOfWork, NullLogger<AuditAiAuditSink>.Instance);

        await sink.RecordAsync(Entry(actor: "joao"));

        var audit = writer.Entries.Should().ContainSingle().Which;
        audit.Action.Should().Be("AI.Copilot");
        audit.Resource.Should().Be("ollama/modelo-primario");
        audit.Outcome.Should().Be("Succeeded");
        audit.ActorId.Should().Be("joao");
        audit.CorrelationId.Should().Be("trace-1");
        audit.Error.Should().BeNull();
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task RecordAsync_fallback_vira_SucceededViaFallback_com_motivo_54()
    {
        var writer = new FakeAuditWriter();
        var sink = new AuditAiAuditSink(writer, new FakeUnitOfWork(), NullLogger<AuditAiAuditSink>.Instance);

        await sink.RecordAsync(Entry(usedFallback: true, fallbackReason: "timeout no primário"));

        var audit = writer.Entries.Should().ContainSingle().Which;
        audit.Outcome.Should().Be("SucceededViaFallback");
        audit.Error.Should().Be("Fallback após: timeout no primário");
    }

    [Fact]
    public async Task RecordAsync_falha_vira_Failed_com_erro()
    {
        var writer = new FakeAuditWriter();
        var sink = new AuditAiAuditSink(writer, new FakeUnitOfWork(), NullLogger<AuditAiAuditSink>.Instance);

        await sink.RecordAsync(Entry(success: false));

        var audit = writer.Entries.Should().ContainSingle().Which;
        audit.Outcome.Should().Be("Failed");
        audit.Error.Should().Be("boom");
    }

    [Fact]
    public async Task Gateway_com_sink_real_quebrado_continua_devolvendo_a_chamada_fail_open()
    {
        var provider = new FakeAiProvider("ollama");
        var gateway = new AiGateway(
            new FakeAiRouter(new AiProviderSelection("ollama", "modelo-primario")),
            new FakeAiRegistry(provider),
            new FakeResilienceExecutor(),
            new AuditAiAuditSink(
                new FakeAuditWriter { Fault = new InvalidOperationException("banco de auditoria fora") },
                new FakeUnitOfWork(),
                NullLogger<AuditAiAuditSink>.Instance));

        var completion = await gateway.CompleteAsync(
            new AiCompletionRequest(AiCapability.Copilot, "prompt"));

        completion.Text.Should().Be("resposta-ollama");
    }
}
