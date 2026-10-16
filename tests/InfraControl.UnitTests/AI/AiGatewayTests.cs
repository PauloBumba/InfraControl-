using FluentAssertions;
using InfraControl.Application.AI;
using InfraControl.Application.Common.Interfaces;

namespace InfraControl.UnitTests.AI;

/// <summary>
/// FAI-T004/S3 (docs/35 §11 · ADR-036/037 · §54/§55): gateway mínimo da
/// IA — resolve capacidade → provider/modelo, executa com resiliência
/// (timeout/retry reutilizando <see cref="IResilienceExecutor"/>), aplica
/// fallback quando o primário falha, emite exatamente UMA entrada de
/// auditoria por request (hook fail-open; persistência é S6/ADR-040) e
/// NÃO tenta fallback quando o cancelamento vem do chamador.
/// Sem Docker: tudo roda com fakes — evidência de integração com Ollama
/// fica pendente de ambiente (docs/35 §12).
/// </summary>
public sealed class AiGatewayTests
{
    private const string PrimaryProvider = "ollama";
    private const string BackupProvider = "backup";

    private static AiProviderSelection WithFallback() =>
        new(PrimaryProvider, "modelo-primario", BackupProvider, "modelo-backup");

    private static AiProviderSelection WithoutFallback() =>
        new(PrimaryProvider, "modelo-primario");

    /// <summary>Provider scriptável: registra chamadas, falha ou responde.</summary>
    private sealed class FakeProvider(string name) : IAiProvider
    {
        public string Name { get; } = name;
        public List<AiCompletionRequest> Calls { get; } = [];
        public Func<AiCompletionRequest, AiProviderResult>? Handler { get; set; }
        public Exception? Fault { get; set; }

        public Task<AiProviderResult> CompleteAsync(
            AiCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(request);
            if (Fault is not null)
                return Task.FromException<AiProviderResult>(Fault);

            return Task.FromResult(Handler is null
                ? new AiProviderResult($"resposta-{Name}")
                : Handler(request));
        }
    }

    /// <summary>Router fixo; selection nula simula capacidade sem mapeamento (§44).</summary>
    private sealed class FakeRouter(AiProviderSelection? selection) : IAiProviderRouter
    {
        public Task<AiProviderSelection> ResolveAsync(
            AiCapability capability,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(selection
            ?? throw new AiProviderNotConfiguredException(
                $"Capacidade de IA '{capability}' não está configurada (nenhum provider/modelo associado)."));
    }

    private sealed class FakeRegistry(params IAiProvider[] providers) : IAiProviderRegistry
    {
        private readonly Dictionary<string, IAiProvider> _providers =
            providers.ToDictionary(provider => provider.Name, StringComparer.OrdinalIgnoreCase);

        public IAiProvider Get(string providerName) =>
            _providers.TryGetValue(providerName, out var provider)
                ? provider
                : throw new AiProviderNotConfiguredException(
                    $"Provider de IA '{providerName}' não está registrado.");
    }

    /// <summary>Executa inline (sem backoff real) e captura policy/opções repassadas.</summary>
    private sealed class FakeResilience : IResilienceExecutor
    {
        public List<(string Policy, ResilienceOptions? Options)> Calls { get; } = [];

        public Task<T> ExecuteAsync<T>(
            string policyName,
            Func<CancellationToken, Task<T>> operation,
            ResilienceOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((policyName, options));
            cancellationToken.ThrowIfCancellationRequested();
            return operation(cancellationToken);
        }
    }

    private sealed class FakeAuditSink : IAiAuditSink
    {
        public List<AiAuditEntry> Entries { get; } = [];
        public Exception? Fault { get; set; }

        public Task RecordAsync(AiAuditEntry entry, CancellationToken cancellationToken = default)
        {
            if (Fault is not null)
                return Task.FromException(Fault);

            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private static AiGateway CreateGateway(
        IAiProviderRouter router,
        IAiProviderRegistry registry,
        FakeResilience? resilience = null,
        FakeAuditSink? audit = null,
        AiGatewayOptions? options = null) =>
        new(router, registry, resilience ?? new FakeResilience(), audit ?? new FakeAuditSink(), options);

    [Fact]
    public async Task CompleteAsync_executa_provider_primario_e_audita_sucesso()
    {
        var primary = new FakeProvider(PrimaryProvider)
        {
            Handler = _ => new AiProviderResult("resposta-modelo", 12, 34),
        };
        var fallback = new FakeProvider(BackupProvider);
        var resilience = new FakeResilience();
        var audit = new FakeAuditSink();
        var gateway = CreateGateway(
            new FakeRouter(WithFallback()), new FakeRegistry(primary, fallback), resilience, audit);

        var completion = await gateway.CompleteAsync(
            new AiCompletionRequest(AiCapability.Copilot, "explique a topologia", TraceId: "trace-1"));

        completion.Text.Should().Be("resposta-modelo");
        completion.ProviderName.Should().Be(PrimaryProvider);
        completion.Model.Should().Be("modelo-primario");
        completion.UsedFallback.Should().BeFalse();
        completion.PromptTokens.Should().Be(12);
        completion.CompletionTokens.Should().Be(34);
        fallback.Calls.Should().BeEmpty();

        primary.Calls.Should().ContainSingle().Which.Model.Should().Be("modelo-primario");
        resilience.Calls.Should().ContainSingle().Which.Policy.Should().Be($"ai/{PrimaryProvider}");

        var entry = audit.Entries.Should().ContainSingle().Subject;
        entry.Success.Should().BeTrue();
        entry.Capability.Should().Be(AiCapability.Copilot);
        entry.ProviderName.Should().Be(PrimaryProvider);
        entry.Model.Should().Be("modelo-primario");
        entry.UsedFallback.Should().BeFalse();
        entry.TraceId.Should().Be("trace-1");
        entry.Error.Should().BeNull();
        entry.Latency.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        entry.PromptTokens.Should().Be(12);
    }

    [Fact]
    public async Task CompleteAsync_quando_primario_falha_usa_fallback_e_marca_a_entrada()
    {
        var primary = new FakeProvider(PrimaryProvider) { Fault = new TimeoutException("primário estourou") };
        var fallback = new FakeProvider(BackupProvider);
        var audit = new FakeAuditSink();
        var gateway = CreateGateway(
            new FakeRouter(WithFallback()), new FakeRegistry(primary, fallback), audit: audit);

        var completion = await gateway.CompleteAsync(
            new AiCompletionRequest(AiCapability.IncidentAnalysis, "analise o incidente"));

        completion.Text.Should().Be($"resposta-{BackupProvider}");
        completion.ProviderName.Should().Be(BackupProvider);
        completion.Model.Should().Be("modelo-backup");
        completion.UsedFallback.Should().BeTrue();
        primary.Calls.Should().HaveCount(1);
        fallback.Calls.Should().HaveCount(1);

        var entry = audit.Entries.Should().ContainSingle().Subject;
        entry.Success.Should().BeTrue();
        entry.UsedFallback.Should().BeTrue();
        entry.ProviderName.Should().Be(BackupProvider);
    }

    [Fact]
    public async Task CompleteAsync_com_fallback_desabilitado_lanca_gateway_exception()
    {
        var primary = new FakeProvider(PrimaryProvider) { Fault = new TimeoutException("primário estourou") };
        var fallback = new FakeProvider(BackupProvider);
        var audit = new FakeAuditSink();
        var gateway = CreateGateway(
            new FakeRouter(WithFallback()),
            new FakeRegistry(primary, fallback),
            audit: audit,
            options: new AiGatewayOptions(FallbackEnabled: false));

        var act = () => gateway.CompleteAsync(
            new AiCompletionRequest(AiCapability.Reporting, "relatório mensal"));

        var exception = await act.Should().ThrowAsync<AiGatewayException>();
        exception.Which.Message.Should().Contain(PrimaryProvider).And.Contain(nameof(AiCapability.Reporting));
        exception.Which.InnerException.Should().BeSameAs(primary.Fault);
        fallback.Calls.Should().BeEmpty();

        var entry = audit.Entries.Should().ContainSingle().Subject;
        entry.Success.Should().BeFalse();
        entry.ProviderName.Should().Be(PrimaryProvider);
        entry.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CompleteAsync_sem_fallback_configurado_lanca_gateway_exception_com_causa()
    {
        var primary = new FakeProvider(PrimaryProvider) { Fault = new InvalidOperationException("deu ruim") };
        var gateway = CreateGateway(
            new FakeRouter(WithoutFallback()), new FakeRegistry(primary));

        var act = () => gateway.CompleteAsync(
            new AiCompletionRequest(AiCapability.Copilot, "pergunta qualquer"));

        var exception = await act.Should().ThrowAsync<AiGatewayException>();
        exception.Which.InnerException.Should().BeSameAs(primary.Fault);
        primary.Calls.Should().HaveCount(1);
    }

    [Fact]
    public async Task CompleteAsync_capacidade_sem_mapeamento_propaga_nao_configurada_e_audita()
    {
        var primary = new FakeProvider(PrimaryProvider);
        var resilience = new FakeResilience();
        var audit = new FakeAuditSink();
        var gateway = CreateGateway(
            new FakeRouter(selection: null), new FakeRegistry(primary), resilience, audit);

        var act = () => gateway.CompleteAsync(
            new AiCompletionRequest(AiCapability.Automation, "rode algo"));

        var exception = await act.Should().ThrowAsync<AiProviderNotConfiguredException>();
        exception.Which.Message.Should().Contain(nameof(AiCapability.Automation));
        resilience.Calls.Should().BeEmpty();
        primary.Calls.Should().BeEmpty();

        var entry = audit.Entries.Should().ContainSingle().Subject;
        entry.Success.Should().BeFalse();
        entry.ProviderName.Should().BeNull();
    }

    [Fact]
    public async Task CompleteAsync_provider_primario_ausente_resgata_pelo_fallback()
    {
        var fallback = new FakeProvider(BackupProvider);
        var gateway = CreateGateway(
            new FakeRouter(WithFallback()), new FakeRegistry(fallback));

        var completion = await gateway.CompleteAsync(
            new AiCompletionRequest(AiCapability.KnowledgeGeneration, "gere um rascunho"));

        completion.UsedFallback.Should().BeTrue();
        completion.ProviderName.Should().Be(BackupProvider);
        completion.Text.Should().Be($"resposta-{BackupProvider}");
    }

    [Fact]
    public async Task CompleteAsync_repassa_opcoes_de_resiliencia_ao_executor()
    {
        var primary = new FakeProvider(PrimaryProvider);
        var resilience = new FakeResilience();
        var gateway = CreateGateway(
            new FakeRouter(WithoutFallback()),
            new FakeRegistry(primary),
            resilience,
            options: new AiGatewayOptions(MaxRetries: 2, Timeout: TimeSpan.FromSeconds(5)));

        await gateway.CompleteAsync(
            new AiCompletionRequest(AiCapability.Copilot, "olá"));

        var call = resilience.Calls.Should().ContainSingle().Subject;
        call.Policy.Should().Be($"ai/{PrimaryProvider}");
        call.Options.Should().NotBeNull();
        call.Options!.MaxRetries.Should().Be(2);
        call.Options.EffectiveTimeout.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CompleteAsync_cancelamento_do_chamador_propaga_sem_fallback_e_audita()
    {
        var primary = new FakeProvider(PrimaryProvider);
        var fallback = new FakeProvider(BackupProvider);
        var audit = new FakeAuditSink();
        var gateway = CreateGateway(
            new FakeRouter(WithFallback()), new FakeRegistry(primary, fallback), audit: audit);

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var act = () => gateway.CompleteAsync(
            new AiCompletionRequest(AiCapability.Copilot, "demorado"),
            cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        fallback.Calls.Should().BeEmpty("cancelamento não é falha do provider");

        var entry = audit.Entries.Should().ContainSingle().Subject;
        entry.Success.Should().BeFalse();
        entry.UsedFallback.Should().BeFalse();
    }

    [Fact]
    public async Task CompleteAsync_falha_no_sink_de_audit_nao_derruba_a_chamada()
    {
        var primary = new FakeProvider(PrimaryProvider);
        var audit = new FakeAuditSink { Fault = new InvalidOperationException("sem espaço na fila") };
        var gateway = CreateGateway(
            new FakeRouter(WithoutFallback()), new FakeRegistry(primary), audit: audit);

        var completion = await gateway.CompleteAsync(
            new AiCompletionRequest(AiCapability.Copilot, "responda"));

        completion.Text.Should().Be($"resposta-{PrimaryProvider}");
        audit.Entries.Should().BeEmpty("o fake falhou antes de gravar — só o não-throw importa");
    }

    [Fact]
    public async Task CompleteAsync_prompt_vazio_lanca_argument_exception_ptbr()
    {
        var primary = new FakeProvider(PrimaryProvider);
        var resilience = new FakeResilience();
        var audit = new FakeAuditSink();
        var gateway = CreateGateway(
            new FakeRouter(WithoutFallback()), new FakeRegistry(primary), resilience, audit);

        var act = () => gateway.CompleteAsync(
            new AiCompletionRequest(AiCapability.Copilot, "   "));

        var exception = await act.Should().ThrowAsync<ArgumentException>();
        exception.Which.Message.Should().Contain("prompt");
        resilience.Calls.Should().BeEmpty("validação acontece antes de qualquer chamada");
        audit.Entries.Should().BeEmpty("request inválido não gera entrada de auditoria");
    }
}
