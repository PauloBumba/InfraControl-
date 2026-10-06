using InfraControl.Application.Pdi.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Pdi.Commands;

// Catálogo de competências (docs/30 §23 entidade de apoio, §4, §5;
// Fase 2, ADR-032). Escrita exclusiva do Admin (catálogo compartilhado
// — "Admin gerencia todo o CRUD"); leitura = Operate no controller.
// Nome único case-insensitive → 409; em uso por objetivo → 409.

public sealed record CreatePdiCompetencyCommand(
    string Name,
    string? Description = null) : BaseCommand<PdiCompetencyDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;
}

public sealed class CreatePdiCompetencyCommandHandler(
    IPdiStructureRepository repository,
    ILogger<CreatePdiCompetencyCommandHandler> logger)
    : IRequestHandler<CreatePdiCompetencyCommand, PdiCompetencyDto>
{
    public async Task<PdiCompetencyDto> Handle(
        CreatePdiCompetencyCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (await repository.CompetencyNameExistsAsync(name, null, cancellationToken))
            throw new InvalidOperationException(
                $"Já existe uma competência chamada '{name}'.");

        var competency = PdiCompetency.Create(name, request.Description);
        repository.AddCompetency(competency);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "PDI competency '{Name}' ({Id}) created.",
            competency.Name, competency.Id);

        return new PdiCompetencyDto(competency.Id, competency.Name, competency.Description);
    }
}

public sealed record UpdatePdiCompetencyCommand(
    Guid Id,
    string Name,
    string? Description = null) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;
}

public sealed class UpdatePdiCompetencyCommandHandler(
    IPdiStructureRepository repository,
    ILogger<UpdatePdiCompetencyCommandHandler> logger)
    : IRequestHandler<UpdatePdiCompetencyCommand>
{
    public async Task Handle(
        UpdatePdiCompetencyCommand request, CancellationToken cancellationToken)
    {
        var competency = await repository.GetCompetencyAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Competência '{request.Id}' não encontrada.");

        var name = request.Name?.Trim() ?? string.Empty;
        if (await repository.CompetencyNameExistsAsync(name, request.Id, cancellationToken))
            throw new InvalidOperationException(
                $"Já existe uma competência chamada '{name}'.");

        competency.Update(name, request.Description);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "PDI competency '{Name}' ({Id}) updated.",
            competency.Name, competency.Id);
    }
}

public sealed record DeletePdiCompetencyCommand(Guid Id) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;
}

public sealed class DeletePdiCompetencyCommandHandler(
    IPdiStructureRepository repository,
    ILogger<DeletePdiCompetencyCommandHandler> logger)
    : IRequestHandler<DeletePdiCompetencyCommand>
{
    public async Task Handle(
        DeletePdiCompetencyCommand request, CancellationToken cancellationToken)
    {
        var competency = await repository.GetCompetencyAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Competência '{request.Id}' não encontrada.");

        if (await repository.CompetencyInUseAsync(competency.Id, cancellationToken))
            throw new InvalidOperationException(
                "Não é possível excluir a competência: existem objetivos vinculados.");

        repository.RemoveCompetency(competency);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "PDI competency '{Name}' ({Id}) deleted.",
            competency.Name, competency.Id);
    }
}
