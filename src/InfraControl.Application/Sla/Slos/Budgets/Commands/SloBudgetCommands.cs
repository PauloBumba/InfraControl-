using InfraControl.Application.Sla.Slos.Budgets.Ports;
using InfraControl.Application.Sla.Slos.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Sla.Slos.Budgets.Commands;

// F8-T007: orçamento (erro budget) do SLO — 400 (percentual/período),
// 404 (orçamento/SLO), 409 (exclusão de SLO com orçamentos).

public sealed record RecordSloBudgetCommand(
    Guid SloId,
    DateTime PeriodStart,
    DateTime? PeriodEnd,
    decimal ConsumedPercent) : BaseCommand<SloBudgetDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class RecordSloBudgetCommandHandler(
    ISloBudgetRepository repository,
    ISloRepository sloRepository,
    ILogger<RecordSloBudgetCommandHandler> logger)
    : IRequestHandler<RecordSloBudgetCommand, SloBudgetDto>
{
    public async Task<SloBudgetDto> Handle(
        RecordSloBudgetCommand request, CancellationToken cancellationToken)
    {
        _ = await sloRepository.GetByIdAsync(request.SloId, cancellationToken)
            ?? throw new KeyNotFoundException($"SLO '{request.SloId}' não encontrado.");

        var budget = SloBudget.Create(
            request.SloId, request.PeriodStart, request.PeriodEnd,
            request.ConsumedPercent);

        await repository.AddAsync(budget, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "SloBudget {SloBudgetId} recorded for SLO {SloId} ({Consumed}% consumed).",
            budget.Id, budget.SloId, budget.ConsumedPercent);

        return new SloBudgetDto(
            budget.Id, budget.SloId, budget.PeriodStart,
            budget.PeriodEnd, budget.ConsumedPercent);
    }
}

public sealed record DeleteSloBudgetCommand(Guid Id) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class DeleteSloBudgetCommandHandler(
    ISloBudgetRepository repository,
    ILogger<DeleteSloBudgetCommandHandler> logger)
    : IRequestHandler<DeleteSloBudgetCommand>
{
    public async Task Handle(
        DeleteSloBudgetCommand request, CancellationToken cancellationToken)
    {
        var budget = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Orçamento de SLO '{request.Id}' não encontrado.");

        repository.Remove(budget);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "SloBudget {SloBudgetId} deleted (SLO {SloId}).",
            budget.Id, budget.SloId);
    }
}
