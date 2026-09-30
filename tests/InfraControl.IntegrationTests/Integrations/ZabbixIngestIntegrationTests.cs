using FluentAssertions;
using InfraControl.Application.Incidents.Queries;
using InfraControl.Application.Integrations.Zabbix;
using InfraControl.Domain.Enums;
using InfraControl.IntegrationTests.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.Integrations;

/// <summary>
/// F16-T001/ADR-023: webhook Zabbix ponta-a-ponta pelo pipeline MediatR —
/// criação, dedup idempotente, fechamento por OK e host desconhecido sem
/// device. O guard do token mora no controller; aqui o comando entra sem
/// role (não implementa IAuthorizedRequest) — a query de leitura usa Admin.
/// </summary>
public sealed class ZabbixIngestIntegrationTests : BaseIntegrationTest
{
    [Fact]
    public async Task ProblemThenOk_ShouldCreateAndResolveIncident()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);

        var created = await mediator.Send(new IngestZabbixProblemCommand(
            "evt-1", "host-zbx", "192.0.2.88", "CPU load alta", 4, "PROBLEM"));
        created.Outcome.Should().Be("created");
        created.IncidentId.Should().NotBeNull();

        var resolved = await mediator.Send(new IngestZabbixProblemCommand(
            "evt-1", "host-zbx", "192.0.2.88", "CPU load alta", 4, "OK"));
        resolved.Outcome.Should().Be("resolved");
        resolved.IncidentId.Should().Be(created.IncidentId);

        var incident = await mediator.Send(
            new GetIncidentQuery(created.IncidentId!.Value));
        incident.Status.Should().Be(IncidentStatus.Resolved.ToString());
        incident.Severity.Should().Be(IncidentSeverity.Critical.ToString());
    }

    [Fact]
    public async Task DuplicateProblem_ShouldBeIdempotent()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var command = new IngestZabbixProblemCommand(
            "evt-2", "host-zbx", "192.0.2.88", "Disco cheio", 3, "PROBLEM");
        var first = await mediator.Send(command);
        var second = await mediator.Send(command);

        first.Outcome.Should().Be("created");
        second.Outcome.Should().Be("duplicate");
        second.IncidentId.Should().Be(first.IncidentId);
    }

    [Fact]
    public async Task UnknownHost_ShouldCreateIncidentWithoutDevice()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);

        var created = await mediator.Send(new IngestZabbixProblemCommand(
            "evt-3", "host-fora-do-inventario", "203.0.113.50", "Backup falhou", 2, "PROBLEM"));
        created.Outcome.Should().Be("created");

        var incident = await mediator.Send(
            new GetIncidentQuery(created.IncidentId!.Value));
        incident.DeviceId.Should().BeNull();
        incident.Description.Should()
            .Contain("host-fora-do-inventario").And.Contain("203.0.113.50");
    }
}
