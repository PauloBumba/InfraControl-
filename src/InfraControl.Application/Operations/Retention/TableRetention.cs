using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Operations.Retention.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Operations.Retention;

/// <summary>
/// Tabela descoberta + política efetiva: <c>PolicyDays</c> nulo = sem
/// política (a tabela só é podada se o admin atribuir dias).
/// </summary>
public sealed record RetentionTableDto(
    string EntityName, string TableName, int? PolicyDays);

/// <summary>
/// Lista as entidades limpáveis do EF model com o estado da política de
/// tabela (Administer — mesma régua da tela de retenção).
/// </summary>
public sealed record GetRetentionTablesQuery
    : IQuery<IReadOnlyList<RetentionTableDto>>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;

    public string AuditResource => "RetentionPolicy";
}

public sealed class GetRetentionTablesQueryHandler(
    IRetentionTableCatalog catalog,
    ITableRetentionStore store)
    : IRequestHandler<GetRetentionTablesQuery, IReadOnlyList<RetentionTableDto>>
{
    public async Task<IReadOnlyList<RetentionTableDto>> Handle(
        GetRetentionTablesQuery request, CancellationToken cancellationToken)
    {
        var policies = (await store.ListAsync(cancellationToken))
            .ToDictionary(p => p.EntityName, p => p.Days);

        return catalog.ListTargets()
            .Select(t => new RetentionTableDto(
                t.EntityName, t.TableName,
                policies.TryGetValue(t.EntityName, out var d) ? d : (int?)null))
            .OrderBy(d => d.EntityName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

/// <summary>Atribui/atualiza os dias de retenção de UMA tabela (Administer).</summary>
public sealed record UpsertTableRetentionCommand(string EntityName, int Days)
    : BaseCommand<RetentionTableDto>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;

    public string AuditResource => "RetentionPolicy";
}

public sealed class UpsertTableRetentionCommandHandler(
    IRetentionTableCatalog catalog,
    ITableRetentionStore store)
    : IRequestHandler<UpsertTableRetentionCommand, RetentionTableDto>
{
    public async Task<RetentionTableDto> Handle(
        UpsertTableRetentionCommand request, CancellationToken cancellationToken)
    {
        var target = catalog.ListTargets()
            .FirstOrDefault(t => string.Equals(
                t.EntityName, request.EntityName, StringComparison.OrdinalIgnoreCase));
        if (target is null)
            throw new KeyNotFoundException(
                $"Entidade '{request.EntityName}' não é uma tabela limpável.");

        var existing = await store.GetAsync(target.EntityName, cancellationToken);
        if (existing is null)
        {
            var created = Domain.Entities.TableRetentionPolicy.Create(
                target.EntityName, request.Days);
            await store.UpsertAsync(created, cancellationToken);
        }
        else
        {
            existing.Update(request.Days);
            await store.UpsertAsync(existing, cancellationToken);
        }

        return new RetentionTableDto(target.EntityName, target.TableName, request.Days);
    }
}

/// <summary>Remove a política de tabela — a entidade volta a não ser podada.</summary>
public sealed record DeleteTableRetentionCommand(string EntityName)
    : BaseCommand, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;

    public string AuditResource => "RetentionPolicy";
}

public sealed class DeleteTableRetentionCommandHandler(
    ITableRetentionStore store)
    : IRequestHandler<DeleteTableRetentionCommand>
{
    public async Task Handle(
        DeleteTableRetentionCommand request, CancellationToken cancellationToken)
        => await store.DeleteAsync(request.EntityName, cancellationToken);
}
