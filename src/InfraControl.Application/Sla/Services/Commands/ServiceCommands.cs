using InfraControl.Application.Sla.Services.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Authorization;
using InfraControl.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Sla.Services.Commands;

// F8-T001: CRUD de Service — erros pt-BR sobem no envelope via
// GlobalExceptionHandler (ArgumentException → 400,
// KeyNotFoundException → 404, InvalidOperationException → 409).

public sealed record CreateServiceCommand(
    string Name,
    string? Description = null) : BaseCommand<ServiceDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.SlaManage.ToString()];
}

public sealed class CreateServiceCommandHandler(
    IServiceRepository repository,
    ILogger<CreateServiceCommandHandler> logger)
    : IRequestHandler<CreateServiceCommand, ServiceDto>
{
    public async Task<ServiceDto> Handle(
        CreateServiceCommand request, CancellationToken cancellationToken)
    {
        var service = Service.Create(request.Name, request.Description);

        var existing = await repository.GetByNameAsync(service.Name, cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException("Já existe um serviço com este nome.");

        await repository.AddAsync(service, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Service {ServiceId} created ({Name}).", service.Id, service.Name);

        return new ServiceDto(service.Id, service.Name, service.Description);
    }
}

public sealed record UpdateServiceCommand(
    Guid Id,
    string Name,
    string? Description = null) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.SlaManage.ToString()];
}

public sealed class UpdateServiceCommandHandler(
    IServiceRepository repository,
    ILogger<UpdateServiceCommandHandler> logger)
    : IRequestHandler<UpdateServiceCommand>
{
    public async Task Handle(
        UpdateServiceCommand request, CancellationToken cancellationToken)
    {
        var service = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Serviço '{request.Id}' não encontrado.");

        service.Update(request.Name, request.Description);

        var existing = await repository.GetByNameAsync(service.Name, cancellationToken);
        if (existing is not null && existing.Id != service.Id)
            throw new InvalidOperationException("Já existe um serviço com este nome.");

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Service {ServiceId} updated ({Name}).", service.Id, service.Name);
    }
}

public sealed record DeleteServiceCommand(Guid Id) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.SlaManage.ToString()];
}

public sealed class DeleteServiceCommandHandler(
    IServiceRepository repository,
    InfraControl.Application.Sla.Policies.Ports.ISlaPolicyRepository slaPolicyRepository,
    InfraControl.Application.Sla.Slos.Ports.ISloRepository sloRepository,
    ILogger<DeleteServiceCommandHandler> logger)
    : IRequestHandler<DeleteServiceCommand>
{
    public async Task Handle(
        DeleteServiceCommand request, CancellationToken cancellationToken)
    {
        var service = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Serviço '{request.Id}' não encontrado.");

        // F8-T002: bloqueia exclusão com políticas de SLA; FK Restrict é o
        // backstop (padrão Locations/HasRacks).
        if (await slaPolicyRepository.HasServicePoliciesAsync(service.Id, cancellationToken))
            throw new InvalidOperationException(
                "Não é possível excluir o serviço: existem políticas de SLA vinculadas.");

        // F8-T005: bloqueia exclusão com SLOs vinculados.
        if (await sloRepository.HasServiceSlosAsync(service.Id, cancellationToken))
            throw new InvalidOperationException(
                "Não é possível excluir o serviço: existem SLOs vinculados.");

        repository.Remove(service);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Service {ServiceId} deleted ({Name}).", service.Id, service.Name);
    }
}
