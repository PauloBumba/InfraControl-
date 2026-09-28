using FluentAssertions;
using InfraControl.Application.Sla.Slos.Commands;
using InfraControl.Application.Sla.Slos.Measurements.Commands;
using InfraControl.Application.Sla.Slos.Measurements.Queries;
using InfraControl.Application.Sla.Services.Commands;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using InfraControl.IntegrationTests.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.Sla;

/// <summary>
/// F8-T006: medições de SLO ponta-a-ponta — registro, busca cronológica,
/// 400 (valor), 404, 409 (delete do SLO) e 403.
/// </summary>
public sealed class SloMeasurementIntegrationTests : BaseIntegrationTest
{
    private static async Task<Guid> SeedSloAsync(IMediator mediator, string service)
    {
        var serviceId = (await mediator.Send(new CreateServiceCommand(service))).Id;
        return (await mediator.Send(
            new CreateSloCommand($"Disponibilidade {service}", 99.95m, 30, serviceId))).Id;
    }

    [Fact]
    public async Task RecordAndSearchMeasurements_ShouldPersistChronologically()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var sloId = await SeedSloAsync(mediator, "ERPX");

        var first = await mediator.Send(new RecordSloMeasurementCommand(
            sloId, new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc), 99.9m));
        var second = await mediator.Send(new RecordSloMeasurementCommand(
            sloId, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), 99.8m));

        var page = await mediator.Send(new SearchSloMeasurementsQuery(sloId));
        page.TotalCount.Should().Be(2);
        page.Items.Select(m => m.MeasuredAt).Should().BeInDescendingOrder();

        var fetched = await mediator.Send(new GetSloMeasurementQuery(first.Id));
        fetched.ValuePercent.Should().Be(99.9m);
        fetched.Id.Should().Be(first.Id);

        await mediator.Send(new DeleteSloMeasurementCommand(second.Id));
        (await mediator.Send(new SearchSloMeasurementsQuery(sloId))).TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Record_ValueOver100_ShouldThrowArgumentExceptionPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var sloId = await SeedSloAsync(mediator, "ERPY");

        var action = () => mediator.Send(new RecordSloMeasurementCommand(
            sloId, DateTime.UtcNow, 100.5m));

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O valor medido deve estar entre 0 e 100.*");
    }

    [Fact]
    public async Task Record_UnknownSlo_ShouldFailWith404PtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var unknown = Guid.NewGuid();

        var action = () => mediator.Send(new RecordSloMeasurementCommand(
            unknown, DateTime.UtcNow, 99.9m));

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"SLO '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task DeleteSlo_WithMeasurements_ShouldThrowConflictPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseRole(UserRole.Admin);
        var sloId = await SeedSloAsync(mediator, "ERPZ");
        await mediator.Send(new RecordSloMeasurementCommand(
            sloId, DateTime.UtcNow, 99.9m));

        var action = () => mediator.Send(new DeleteSloCommand(sloId));

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Não é possível excluir o SLO: existem medições vinculadas.");
    }

    [Fact]
    public async Task Record_WithoutSlaManage_ShouldFailPtBr()
    {
        using var scope = CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        UseNoPermissions();

        var action = () => mediator.Send(new RecordSloMeasurementCommand(
            Guid.NewGuid(), DateTime.UtcNow, 99.9m));

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Permissões ausentes: SlaManage");
    }
}
