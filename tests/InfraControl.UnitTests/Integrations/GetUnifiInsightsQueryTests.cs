using FluentAssertions;
using InfraControl.Application.Integrations.Unifi;
using InfraControl.Application.Integrations.Unifi.Insights;
using InfraControl.Application.Integrations.Unifi.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using Moq;

namespace InfraControl.UnitTests.Integrations;

/// <summary>
/// F18-T007: query dos painéis de Insights — ordem por bytes (top do
/// Controller), teto <see cref="GetUnifiInsightsQueryHandler.MaxEntries"/>,
/// filtro de apps sem bytes, captura do horário da sondagem e
/// leitura de dashboard = qualquer papel autenticado (RoleAccess.Read).
/// </summary>
public sealed class GetUnifiInsightsQueryTests
{
    [Fact]
    public async Task Handle_ShouldOrderTrafficDescendingAndCapAtMaxEntries()
    {
        var total = GetUnifiInsightsQueryHandler.MaxEntries + 10;
        var clients = new List<UnifiClientTrafficDto>();
        for (var i = 0; i < total; i++)
            clients.Add(new UnifiClientTrafficDto(
                $"aa:bb:cc:dd:ee:{i:D2}", $"host-{i}", "10.0.0.1", i, 0, null));

        var client = new Mock<IUnifiApiClient>();
        client.Setup(c => c.ListClientTrafficAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(clients);
        client.Setup(c => c.ListDpiStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UnifiDpiAppDto>());

        var handler = new GetUnifiInsightsQueryHandler(client.Object);
        var result = await handler.Handle(
            new GetUnifiInsightsQuery(), CancellationToken.None);

        result.TrafficPerClient.Should()
            .HaveCount(GetUnifiInsightsQueryHandler.MaxEntries, "o teto evita payload gigante na UI");
        result.TrafficPerClient.Should().BeInDescendingOrder(c => c.TotalBytes);
        result.TrafficPerClient[0].Hostname.Should().Be($"host-{total - 1}");
        result.DpiPerApplication.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldFilterZeroByteAppsAndFillCapturedAtUtc()
    {
        var dpi = new[]
        {
            new UnifiDpiAppDto("App pequena", "cat", 10),
            new UnifiDpiAppDto("App zerada", "cat", 0),
            new UnifiDpiAppDto("App grande", "cat", 100),
        };

        var client = new Mock<IUnifiApiClient>();
        client.Setup(c => c.ListClientTrafficAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UnifiClientTrafficDto>());
        client.Setup(c => c.ListDpiStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(dpi);

        var handler = new GetUnifiInsightsQueryHandler(client.Object);
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var result = await handler.Handle(
            new GetUnifiInsightsQuery(), CancellationToken.None);
        var after = DateTimeOffset.UtcNow.AddSeconds(1);

        // zerada fica de fora e o restante ordena por bytes desc
        result.DpiPerApplication.Select(app => app.App).Should()
            .Equal("App grande", "App pequena");
        result.TrafficPerClient.Should().BeEmpty();
        (result.CapturedAtUtc >= before && result.CapturedAtUtc <= after)
            .Should().BeTrue("CapturedAtUtc é o instante da sondagem");
    }

    [Fact]
    public void Query_ShouldAllowAnyAuthenticatedRoleToRead()
    {
        var query = new GetUnifiInsightsQuery();

        query.Should().BeAssignableTo<IAuthorizedRequest>();
        // leitura de dashboard: qualquer papel autenticado (espelho do /health)
        query.RequiredRoles.Should().BeEquivalentTo(RoleAccess.Read);
    }
}
