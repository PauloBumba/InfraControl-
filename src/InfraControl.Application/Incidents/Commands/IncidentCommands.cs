using InfraControl.Application.Incidents.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Incidents.Commands;

// F6-T003: Incident — criação manual, acknowledge (docs/15) e troca de
// status (5 estados da UI, transições livres ≠ atual). Erros pt-BR:
// ArgumentException → 400, KeyNotFoundException → 404.

public sealed record CreateIncidentCommand(
    string Title,
    IncidentSeverity Severity,
    string? Description = null,
    Guid? DeviceId = null) : BaseCommand<IncidentDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class CreateIncidentCommandHandler(
    IIncidentRepository repository,
    ILogger<CreateIncidentCommandHandler> logger)
    : IRequestHandler<CreateIncidentCommand, IncidentDto>
{
    public async Task<IncidentDto> Handle(
        CreateIncidentCommand request, CancellationToken cancellationToken)
    {
        var incident = Incident.Create(
            request.Title, request.Severity, request.DeviceId, request.Description);

        await repository.AddAsync(incident, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Incident {IncidentId} created ({Title}).", incident.Id, incident.Title);

        return IncidentDto.From(incident);
    }
}

/// <summary>Assume o incidente (Open → Investigating — docs/15).</summary>
public sealed record AcknowledgeIncidentCommand(Guid Id)
    : BaseCommand<IncidentDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class AcknowledgeIncidentCommandHandler(
    IIncidentRepository repository)
    : IRequestHandler<AcknowledgeIncidentCommand, IncidentDto>
{
    public async Task<IncidentDto> Handle(
        AcknowledgeIncidentCommand request, CancellationToken cancellationToken)
    {
        var incident = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Incidente '{request.Id}' não encontrado.");

        incident.Acknowledge();
        await repository.SaveChangesAsync(cancellationToken);

        return IncidentDto.From(incident);
    }
}

/// <summary>Troca o status (open|investigating|identified|monitoring|resolved).</summary>
public sealed record UpdateIncidentStatusCommand(Guid Id, string Status)
    : BaseCommand<IncidentDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UpdateIncidentStatusCommandHandler(
    IIncidentRepository repository)
    : IRequestHandler<UpdateIncidentStatusCommand, IncidentDto>
{
    public async Task<IncidentDto> Handle(
        UpdateIncidentStatusCommand request, CancellationToken cancellationToken)
    {
        var status = ParseStatus(request.Status);

        var incident = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Incidente '{request.Id}' não encontrado.");

        incident.ChangeStatus(status);
        await repository.SaveChangesAsync(cancellationToken);

        return IncidentDto.From(incident);
    }

    internal static IncidentStatus ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)
            || !Enum.TryParse(status.Trim(), ignoreCase: true, out IncidentStatus parsed))
            throw new ArgumentException(
                $"Status inválido: '{status}'. Use open, investigating, "
                + "identified, monitoring ou resolved.",
                nameof(status));

        return parsed;
    }
}
