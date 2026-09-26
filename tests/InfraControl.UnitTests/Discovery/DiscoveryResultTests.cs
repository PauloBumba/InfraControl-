using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Discovery;

/// <summary>
/// F4-T002: achado de discovery — modelo genérico aprovado pelo usuário
/// (Kind + Summary + JSON) para servir todos os providers (T003–T009)
/// sem nova migração; DeviceId? opcional liga ao T010 New Device Detection.
/// Escopo: somente leitura — escrita vem com os providers.
/// </summary>
public sealed class DiscoveryResultTests
{
    [Fact]
    public void Create_Valid_ShouldSetProperties()
    {
        var runId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
        var values = new Dictionary<string, string>
        {
            ["neighbor"] = "sw-core-01",
            ["local_port"] = "Gi0/1"
        };

        var result = DiscoveryResult.Create(
            runId, DiscoveryResultKind.Neighbor, "  sw-core-01 via Gi0/1  ",
            values, deviceId, createdAt);

        result.DiscoveryRunId.Should().Be(runId);
        result.Kind.Should().Be(DiscoveryResultKind.Neighbor);
        result.Summary.Should().Be("sw-core-01 via Gi0/1");
        result.Values.Should().BeEquivalentTo(values);
        result.DeviceId.Should().Be(deviceId);
        result.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void Create_WithoutOptionalValuesAndDevice_ShouldDefaultToNull()
    {
        var result = DiscoveryResult.Create(
            Guid.NewGuid(), DiscoveryResultKind.Host, "host up");

        result.Values.Should().BeNull();
        result.DeviceId.Should().BeNull();
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Create_EmptyRunId_ShouldThrowPtBr()
    {
        var act = () => DiscoveryResult.Create(
            Guid.Empty, DiscoveryResultKind.Host, "host up");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*execução de discovery*");
    }

    [Fact]
    public void Create_EmptySummary_ShouldThrowPtBr()
    {
        var act = () => DiscoveryResult.Create(
            Guid.NewGuid(), DiscoveryResultKind.Host, "   ");

        act.Should().Throw<ArgumentException>().WithMessage("*resumo*");
    }

    [Fact]
    public void Create_SummaryAboveMaxLength_ShouldThrowPtBr()
    {
        var act = () => DiscoveryResult.Create(
            Guid.NewGuid(),
            DiscoveryResultKind.Host,
            new string('a', DiscoveryResult.SummaryMaxLength + 1));

        act.Should().Throw<ArgumentException>().WithMessage("*500*");
    }

    [Fact]
    public void Create_UndefinedKind_ShouldThrowPtBr()
    {
        var act = () => DiscoveryResult.Create(
            Guid.NewGuid(), (DiscoveryResultKind)99, "x");

        act.Should().Throw<ArgumentException>().WithMessage("*tipo*");
    }

    [Fact]
    public void Create_EmptyDeviceId_ShouldThrowPtBr()
    {
        var act = () => DiscoveryResult.Create(
            Guid.NewGuid(), DiscoveryResultKind.Host, "host up", deviceId: Guid.Empty);

        act.Should().Throw<ArgumentException>().WithMessage("*device*");
    }
}
