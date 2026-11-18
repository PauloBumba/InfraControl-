using InfraControl.Application.Monitoring.Checks.Ports;
using InfraControl.Application.Sla.Services.Ports;
using InfraControl.Application.Sla.Slos.Bindings;
using InfraControl.Application.Sla.Slos.Bindings.Ports;
using InfraControl.Application.Sla.Slos.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Sla.Slos.Commands;

// F8-T005: CRUD de Slo — 400 (alvo/janela/serviço), 404 (SLO/serviço),
// 409 (exclusão de serviço com SLOs).

public sealed record CreateSloCommand(
    string Name,
    decimal TargetPercent,
    int WindowDays,
    Guid ServiceId,
    /// <summary>ADR-068: checks medidos por este SLO (null/vazio = só manual).</summary>
    IReadOnlyCollection<Guid>? MonitoringCheckIds = null) : BaseCommand<SloDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class CreateSloCommandHandler(
    ISloRepository repository,
    IServiceRepository serviceRepository,
    IMonitoringCheckRepository checkRepository,
    ISloCheckBindingRepository bindingRepository,
    ILogger<CreateSloCommandHandler> logger)
    : IRequestHandler<CreateSloCommand, SloDto>
{
    public async Task<SloDto> Handle(
        CreateSloCommand request, CancellationToken cancellationToken)
    {
        await EnsureServiceExistsAsync(
            serviceRepository, request.ServiceId, cancellationToken);

        var slo = Slo.Create(
            request.Name, request.TargetPercent, request.WindowDays, request.ServiceId);

        await repository.AddAsync(slo, cancellationToken);

        // ADR-068: vínculos depois do Add (SloId já existe; mesma transação).
        await Bindings.SloCheckBindingSync.SyncAsync(
            slo.Id, request.MonitoringCheckIds,
            bindingRepository, checkRepository, cancellationToken);
        await bindingRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Slo {SloId} created ({Name}: {Target}% em {Window} dias).",
            slo.Id, slo.Name, slo.TargetPercent, slo.WindowDays);

        return new SloDto(
            slo.Id, slo.Name, slo.TargetPercent, slo.WindowDays, slo.ServiceId,
            MonitoringCheckIds: request.MonitoringCheckIds?.ToArray());
    }

    internal static async Task EnsureServiceExistsAsync(
        IServiceRepository serviceRepository, Guid serviceId, CancellationToken cancellationToken)
    {
        _ = await serviceRepository.GetByIdAsync(serviceId, cancellationToken)
            ?? throw new KeyNotFoundException($"Serviço '{serviceId}' não encontrado.");
    }
}

public sealed record UpdateSloCommand(
    Guid Id,
    string Name,
    decimal TargetPercent,
    int WindowDays,
    Guid ServiceId,
    /// <summary>ADR-068: substitui o conjunto de checks medidos (null = mantém? não — replace explícito; use a lista atual se quiser preservar).</summary>
    IReadOnlyCollection<Guid>? MonitoringCheckIds = null) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UpdateSloCommandHandler(
    ISloRepository repository,
    IServiceRepository serviceRepository,
    IMonitoringCheckRepository checkRepository,
    ISloCheckBindingRepository bindingRepository,
    ILogger<UpdateSloCommandHandler> logger)
    : IRequestHandler<UpdateSloCommand>
{
    public async Task Handle(
        UpdateSloCommand request, CancellationToken cancellationToken)
    {
        var slo = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"SLO '{request.Id}' não encontrado.");

        await CreateSloCommandHandler.EnsureServiceExistsAsync(
            serviceRepository, request.ServiceId, cancellationToken);

        slo.Update(
            request.Name, request.TargetPercent, request.WindowDays, request.ServiceId);

        // ADR-068: null = não mexe nos vínculos (compat); lista = replace total.
        if (request.MonitoringCheckIds is not null)
        {
            await Bindings.SloCheckBindingSync.SyncAsync(
                slo.Id, request.MonitoringCheckIds,
                bindingRepository, checkRepository, cancellationToken);
        }

        await repository.SaveChangesAsync(cancellationToken);
        await bindingRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Slo {SloId} updated ({Name}).", slo.Id, slo.Name);
    }
}

public sealed record DeleteSloCommand(Guid Id) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class DeleteSloCommandHandler(
    ISloRepository repository,
    Measurements.Ports.ISloMeasurementRepository measurementRepository,
    Budgets.Ports.ISloBudgetRepository budgetRepository,
    ISloCheckBindingRepository bindingRepository,
    ILogger<DeleteSloCommandHandler> logger)
    : IRequestHandler<DeleteSloCommand>
{
    public async Task Handle(
        DeleteSloCommand request, CancellationToken cancellationToken)
    {
        var slo = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"SLO '{request.Id}' não encontrado.");

        // Fase 8 + ADR-068: medições, orçamentos e vínculos de checks
        // bloqueiam a exclusão; FK Restrict é o backstop.
        if (await measurementRepository.HasSloMeasurementsAsync(slo.Id, cancellationToken))
            throw new InvalidOperationException(
                "Não é possível excluir o SLO: existem medições vinculadas.");

        if (await budgetRepository.HasSloBudgetsAsync(slo.Id, cancellationToken))
            throw new InvalidOperationException(
                "Não é possível excluir o SLO: existem orçamentos vinculados.");

        if ((await bindingRepository.GetBySloIdAsync(slo.Id, cancellationToken)).Count > 0)
            throw new InvalidOperationException(
                "Não é possível excluir o SLO: existem checks de monitoramento vinculados.");

        slo.Delete(); // ADR-030 §5 — evento na mesma transação
        repository.Remove(slo);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Slo {SloId} deleted ({Name}).", slo.Id, slo.Name);
    }
}
