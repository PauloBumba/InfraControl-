using InfraControl.Application.Sla.Policies.Ports;
using InfraControl.Application.Sla.Services.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Sla.Policies.Commands;

// F8-T002: CRUD de SlaPolicy — erros pt-BR no envelope via
// GlobalExceptionHandler (ArgumentException → 400, KeyNotFound → 404,
// InvalidOperationException → 409).

public sealed record CreateSlaPolicyCommand(
    string Name,
    int ResponseMinutes,
    int ResolutionMinutes,
    Guid? ServiceId = null) : BaseCommand<SlaPolicyDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class CreateSlaPolicyCommandHandler(
    ISlaPolicyRepository repository,
    IServiceRepository serviceRepository,
    ILogger<CreateSlaPolicyCommandHandler> logger)
    : IRequestHandler<CreateSlaPolicyCommand, SlaPolicyDto>
{
    public async Task<SlaPolicyDto> Handle(
        CreateSlaPolicyCommand request, CancellationToken cancellationToken)
    {
        await EnsureServiceExistsAsync(request.ServiceId, cancellationToken);

        var policy = SlaPolicy.Create(
            request.Name,
            request.ResponseMinutes,
            request.ResolutionMinutes,
            request.ServiceId);

        await repository.AddAsync(policy, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "SlaPolicy {SlaPolicyId} created ({Name}: {Response}/{Resolution} min).",
            policy.Id, policy.Name, policy.ResponseMinutes, policy.ResolutionMinutes);

        return ToDto(policy);
    }

    internal static async Task EnsureServiceExistsAsync(
        IServiceRepository serviceRepository, Guid? serviceId, CancellationToken cancellationToken)
    {
        if (serviceId is null)
            return;

        _ = await serviceRepository.GetByIdAsync(serviceId.Value, cancellationToken)
            ?? throw new KeyNotFoundException($"Serviço '{serviceId}' não encontrado.");
    }

    internal static SlaPolicyDto ToDto(SlaPolicy policy) =>
        new(policy.Id, policy.Name, policy.ResponseMinutes, policy.ResolutionMinutes, policy.ServiceId);

    private async Task EnsureServiceExistsAsync(Guid? serviceId, CancellationToken cancellationToken)
        => await EnsureServiceExistsAsync(serviceRepository, serviceId, cancellationToken);
}

public sealed record UpdateSlaPolicyCommand(
    Guid Id,
    string Name,
    int ResponseMinutes,
    int ResolutionMinutes,
    Guid? ServiceId = null) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UpdateSlaPolicyCommandHandler(
    ISlaPolicyRepository repository,
    IServiceRepository serviceRepository,
    ILogger<UpdateSlaPolicyCommandHandler> logger)
    : IRequestHandler<UpdateSlaPolicyCommand>
{
    public async Task Handle(
        UpdateSlaPolicyCommand request, CancellationToken cancellationToken)
    {
        var policy = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Política de SLA '{request.Id}' não encontrada.");

        await CreateSlaPolicyCommandHandler.EnsureServiceExistsAsync(
            serviceRepository, request.ServiceId, cancellationToken);

        policy.Update(
            request.Name, request.ResponseMinutes, request.ResolutionMinutes, request.ServiceId);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "SlaPolicy {SlaPolicyId} updated ({Name}).", policy.Id, policy.Name);
    }
}

public sealed record DeleteSlaPolicyCommand(Guid Id) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class DeleteSlaPolicyCommandHandler(
    ISlaPolicyRepository repository,
    Sla.TaskSlas.Ports.ITaskSlaRepository taskSlaRepository,
    ILogger<DeleteSlaPolicyCommandHandler> logger)
    : IRequestHandler<DeleteSlaPolicyCommand>
{
    public async Task Handle(
        DeleteSlaPolicyCommand request, CancellationToken cancellationToken)
    {
        var policy = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Política de SLA '{request.Id}' não encontrada.");

        // F8-T003: prazos históricos preservados (docs/04) — não apagar a
        // política enquanto tasks a usam; FK Restrict é o backstop.
        if (await taskSlaRepository.HasPolicySlasAsync(policy.Id, cancellationToken))
            throw new InvalidOperationException(
                "Não é possível excluir a política de SLA: existem tasks com este SLA aplicado.");

        policy.Delete(); // ADR-030 §5 — evento na mesma transação
        repository.Remove(policy);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "SlaPolicy {SlaPolicyId} deleted ({Name}).", policy.Id, policy.Name);
    }
}
