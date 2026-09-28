using InfraControl.Application.Sla.Services.Ports;
using InfraControl.Application.Sla.Slos.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Authorization;
using InfraControl.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Sla.Slos.Commands;

// F8-T005: CRUD de Slo — 400 (alvo/janela/serviço), 404 (SLO/serviço),
// 409 (exclusão de serviço com SLOs).

public sealed record CreateSloCommand(
    string Name,
    decimal TargetPercent,
    int WindowDays,
    Guid ServiceId) : BaseCommand<SloDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.SlaManage.ToString()];
}

public sealed class CreateSloCommandHandler(
    ISloRepository repository,
    IServiceRepository serviceRepository,
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
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Slo {SloId} created ({Name}: {Target}% em {Window} dias).",
            slo.Id, slo.Name, slo.TargetPercent, slo.WindowDays);

        return new SloDto(
            slo.Id, slo.Name, slo.TargetPercent, slo.WindowDays, slo.ServiceId);
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
    Guid ServiceId) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.SlaManage.ToString()];
}

public sealed class UpdateSloCommandHandler(
    ISloRepository repository,
    IServiceRepository serviceRepository,
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
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Slo {SloId} updated ({Name}).", slo.Id, slo.Name);
    }
}

public sealed record DeleteSloCommand(Guid Id) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.SlaManage.ToString()];
}

public sealed class DeleteSloCommandHandler(
    ISloRepository repository,
    ILogger<DeleteSloCommandHandler> logger)
    : IRequestHandler<DeleteSloCommand>
{
    public async Task Handle(
        DeleteSloCommand request, CancellationToken cancellationToken)
    {
        var slo = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"SLO '{request.Id}' não encontrado.");

        // SloMeasurement (F8-T006) / SloBudget (F8-T007) passam a bloquear
        // aqui (pré-check); FK Restrict é o backstop.
        repository.Remove(slo);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Slo {SloId} deleted ({Name}).", slo.Id, slo.Name);
    }
}
