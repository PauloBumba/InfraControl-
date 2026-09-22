using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Infrastructure;

public sealed class PlatformFoundationTests
{
    [Fact]
    public async Task ResilienceExecutor_ShouldRetryAndEventuallySucceed()
    {
        var executor = new ResilienceExecutor(NullLogger<ResilienceExecutor>.Instance);
        var attempts = 0;

        var result = await executor.ExecuteAsync(
            "test-provider",
            _ =>
            {
                attempts++;
                return attempts < 2
                    ? Task.FromException<string>(new InvalidOperationException("temporary"))
                    : Task.FromResult("ok");
            },
            new ResilienceOptions(MaxRetries: 1, Timeout: TimeSpan.FromSeconds(1), FailureThreshold: 3),
            CancellationToken.None);

        result.Should().Be("ok");
        attempts.Should().Be(2);
    }

    [Fact]
    public async Task ResilienceExecutor_ShouldOpenCircuitAfterThreshold()
    {
        var executor = new ResilienceExecutor(NullLogger<ResilienceExecutor>.Instance);
        var options = new ResilienceOptions(MaxRetries: 0, FailureThreshold: 1, BreakDuration: TimeSpan.FromMinutes(1));

        await executor.Invoking(instance => instance.ExecuteAsync(
                "test-provider",
                _ => Task.FromException<string>(new InvalidOperationException("down")),
                options))
            .Should().ThrowAsync<InvalidOperationException>();

        await executor.Invoking(instance => instance.ExecuteAsync(
                "test-provider",
                _ => Task.FromResult("should-not-run"),
                options))
            .Should().ThrowAsync<CircuitOpenException>();
    }

    [Fact]
    public void ProviderRegistry_ShouldResolveByProviderKey()
    {
        var provider = new FakeProvider("snmp");
        var registry = new ProviderRegistry<IInfrastructureProvider>([provider]);

        registry.Resolve("snmp").Should().BeSameAs(provider);
        registry.GetAll().Should().ContainSingle();
    }

    [Fact]
    public void AuditEntry_ShouldRejectEmptyMetadata()
    {
        var action = () => new AuditEntry("", "ApplicationRequest", "Succeeded");

        action.Should().Throw<ArgumentException>();
    }

    private sealed class FakeProvider(string name) : IInfrastructureProvider
    {
        public string Name { get; } = name;
        public bool CanHandle(string providerKey) => string.Equals(Name, providerKey, StringComparison.OrdinalIgnoreCase);
    }
}
