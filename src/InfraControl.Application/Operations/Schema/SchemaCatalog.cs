using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Operations.Schema.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Operations.Schema;

/// <summary>Propriedade de uma entidade do modelo.</summary>
public sealed record SchemaPropertyDto(
    string Name,
    string ClrType,
    bool IsNullable,
    bool IsPrimaryKey,
    string? ForeignKeyTarget);

/// <summary>Entidade mapeada: tabela física, colunas e FKs.</summary>
public sealed record SchemaEntityDto(
    string EntityName,
    string TableName,
    IReadOnlyList<SchemaPropertyDto> Properties);

/// <summary>Catálogo dinâmico do modelo EF — dicionário de dados vivo.</summary>
public sealed record GetSchemaCatalogQuery
    : IQuery<IReadOnlyList<SchemaEntityDto>>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;

    public string AuditResource => "SchemaCatalog";
}

public sealed class GetSchemaCatalogQueryHandler(ISchemaCatalog catalog)
    : IRequestHandler<GetSchemaCatalogQuery, IReadOnlyList<SchemaEntityDto>>
{
    public Task<IReadOnlyList<SchemaEntityDto>> Handle(
        GetSchemaCatalogQuery request, CancellationToken cancellationToken)
        => Task.FromResult(catalog.ListEntities());
}
