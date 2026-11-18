using InfraControl.Application.Operations.Schema;
using InfraControl.Application.Operations.Schema.Ports;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// Dicionário de dados dinâmico: projeta o <see cref="IModel"/> do EF em
/// DTOs — toda entidade nova aparece sozinha, sem código adicional.
/// </summary>
public sealed class SchemaCatalog(AppDbContext dbContext) : ISchemaCatalog
{
    public IReadOnlyList<SchemaEntityDto> ListEntities()
        => dbContext.Model.GetEntityTypes()
            .Where(et => et.GetTableName() is not null && et.ClrType != typeof(object))
            .Select(et =>
            {
                var fkTargets = et.GetForeignKeys()
                    .SelectMany(fk => fk.Properties.Select(p =>
                        (Property: p.Name,
                         Target: $"{fk.PrincipalEntityType.ClrType.Name}.{string.Join("+", fk.PrincipalKey.Properties.Select(k => k.Name))}")))
                    .ToDictionary(x => x.Property, x => x.Target, StringComparer.Ordinal);

                var properties = et.GetProperties()
                    .Where(p => p.PropertyInfo is not null || p.FieldInfo is not null)
                    .Select(p => new SchemaPropertyDto(
                        p.Name,
                        FriendlyType(p.ClrType),
                        p.IsNullable,
                        et.FindPrimaryKey()?.Properties.Contains(p) == true,
                        fkTargets.TryGetValue(p.Name, out var target) ? target : null))
                    .OrderBy(p => p.IsPrimaryKey ? 0 : 1)
                    .ThenBy(p => p.Name, StringComparer.Ordinal)
                    .ToArray();

                return new SchemaEntityDto(
                    et.ClrType.Name,
                    et.GetTableName()!,
                    properties);
            })
            .OrderBy(e => e.EntityName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string FriendlyType(Type type)
        => Nullable.GetUnderlyingType(type) is { } inner
            ? $"{FriendlyType(inner)}?"
            : type.Name;
}
