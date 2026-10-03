using FluentAssertions;
using InfraControl.Application.Incidents.Queries;
using InfraControl.Application.Integrations.Zabbix;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;
using InfraControl.Infrastructure.Persistence;
using InfraControl.IntegrationTests.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

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
            "evt-1", null, "host-zbx", "192.0.2.88", "CPU load alta", 4, "PROBLEM", null));
        created.Outcome.Should().Be("created");
        created.IncidentId.Should().NotBeNull();

        var resolved = await mediator.Send(new IngestZabbixProblemCommand(
            "evt-1", "evt-1-r", "host-zbx", "192.0.2.88", "CPU load alta", 4, "OK", null));
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
            "evt-2", null, "host-zbx", "192.0.2.88", "Disco cheio", 3, "PROBLEM", null);
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
            "evt-3", null, "host-fora-do-inventario", "203.0.113.50", "Backup falhou", 2, "PROBLEM", null));
        created.Outcome.Should().Be("created");

        var incident = await mediator.Send(
            new GetIncidentQuery(created.IncidentId!.Value));
        incident.DeviceId.Should().BeNull();
        incident.Description.Should()
            .Contain("host-fora-do-inventario").And.Contain("203.0.113.50");
    }

    /// <summary>
    /// F16-T002: o PROBLEM do webhook cria o incidente e grava
    /// IncidentCreatedDomainEvent no outbox (mesma transação, ADR-005) — o
    /// consumer de notificações sai daqui.
    /// </summary>
    [Fact]
    public async Task Problem_ShouldWriteIncidentCreatedEventToOutbox()
    {
        using var scope = CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var created = await mediator.Send(new IngestZabbixProblemCommand(
            "evt-ob-1", null, "host-zbx", "192.0.2.89", "Memória no limite", 3, "PROBLEM", null));

        created.Outcome.Should().Be("created");
        var outboxEvents = await context.OutboxMessages.AsNoTracking()
            .Where(m => m.EventType == nameof(IncidentCreatedDomainEvent))
            .ToListAsync();
        // Payload é JSON com escape Unicode (JavaScriptEncoder default):
        // compara o Title parseado, não o texto cru.
        outboxEvents.Should().ContainSingle();
        var payloadTitle = JsonDocument
            .Parse(outboxEvents[0].Payload).RootElement.GetProperty("Title").GetString();
        payloadTitle.Should().Be("Memória no limite");
    }
}
