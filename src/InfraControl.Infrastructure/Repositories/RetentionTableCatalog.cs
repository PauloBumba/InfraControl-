using InfraControl.Application.Operations.Retention.Ports;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// Descobre as entidades "limpáveis" do EF model: tipos mapeados com
/// tabela física e propriedade <c>CreateDateTime</c> (a poda genérica
/// filtra por ela). O admin escolhe explicitamente — listar ≠ apagar.
/// </summary>
public sealed class RetentionTableCatalog(AppDbContext dbContext)
    : IRetentionTableCatalog
{
    public IReadOnlyList<RetentionTableTarget> ListTargets()
        => dbContext.Model.GetEntityTypes()
            .Where(et => et.GetTableName() is not null
                && et.FindProperty("CreateDateTime") is not null
                && et.ClrType != typeof(object))
            .Select(et => new RetentionTableTarget(
                et.ClrType.Name,
                et.GetTableName()!))
            .OrderBy(t => t.EntityName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
