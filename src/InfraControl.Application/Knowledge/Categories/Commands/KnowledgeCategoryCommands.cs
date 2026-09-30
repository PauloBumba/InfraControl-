using InfraControl.Application.Knowledge.Categories.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Knowledge.Categories.Commands;

// F10-T001: CRUD de KnowledgeCategory — 400 (nome), 404 (categoria),
// 409 (nome duplicado case-insensitive).

public sealed record CreateKnowledgeCategoryCommand(
    string Name,
    string? Description) : BaseCommand<KnowledgeCategoryDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class CreateKnowledgeCategoryCommandHandler(
    IKnowledgeCategoryRepository repository,
    ILogger<CreateKnowledgeCategoryCommandHandler> logger)
    : IRequestHandler<CreateKnowledgeCategoryCommand, KnowledgeCategoryDto>
{
    public async Task<KnowledgeCategoryDto> Handle(
        CreateKnowledgeCategoryCommand request, CancellationToken cancellationToken)
    {
        await EnsureNameAvailableAsync(
            repository, request.Name, excludeId: default, cancellationToken);

        var category = KnowledgeCategory.Create(request.Name, request.Description);

        await repository.AddAsync(category, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "KnowledgeCategory {CategoryId} created ({Name}).", category.Id, category.Name);

        return new KnowledgeCategoryDto(category.Id, category.Name, category.Description);
    }

    internal static async Task EnsureNameAvailableAsync(
        IKnowledgeCategoryRepository repository,
        string name,
        Guid excludeId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        var trimmed = name.Trim();
        if (await repository.HasNameAsync(trimmed, excludeId, cancellationToken))
            throw new InvalidOperationException(
                $"Já existe uma categoria com o nome '{trimmed}'.");
    }
}

public sealed record UpdateKnowledgeCategoryCommand(
    Guid Id,
    string Name,
    string? Description) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UpdateKnowledgeCategoryCommandHandler(
    IKnowledgeCategoryRepository repository,
    ILogger<UpdateKnowledgeCategoryCommandHandler> logger)
    : IRequestHandler<UpdateKnowledgeCategoryCommand>
{
    public async Task Handle(
        UpdateKnowledgeCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Categoria '{request.Id}' não encontrada.");

        await CreateKnowledgeCategoryCommandHandler.EnsureNameAvailableAsync(
            repository, request.Name, request.Id, cancellationToken);

        category.Update(request.Name, request.Description);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "KnowledgeCategory {CategoryId} updated ({Name}).", category.Id, category.Name);
    }
}

public sealed record DeleteKnowledgeCategoryCommand(Guid Id) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class DeleteKnowledgeCategoryCommandHandler(
    IKnowledgeCategoryRepository repository,
    ILogger<DeleteKnowledgeCategoryCommandHandler> logger)
    : IRequestHandler<DeleteKnowledgeCategoryCommand>
{
    public async Task Handle(
        DeleteKnowledgeCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Categoria '{request.Id}' não encontrada.");

        repository.Remove(category);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "KnowledgeCategory {CategoryId} deleted ({Name}).", category.Id, category.Name);
    }
}
