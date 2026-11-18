using InfraControl.Domain.Entities;

namespace InfraControl.Application.Operations.Retention.Ports;

/// <summary>
/// Políticas de retenção genéricas por tabela (metodologia 2026-11-15):
/// o app descobre as entidades do EF model; o admin atribui dias por
/// entidade sem criar um novo <see cref="Enums.RetentionDataType"/>.
/// </summary>
public interface ITableRetentionStore
{
    Task<IReadOnlyList<TableRetentionPolicy>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<TableRetentionPolicy?> GetAsync(
        string entityName, CancellationToken cancellationToken = default);

    Task UpsertAsync(
        TableRetentionPolicy policy, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        string entityName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Catálogo das entidades "limpáveis" descobertas do EF model — só entram
/// tipos mapeados com coluna <c>CreateDateTime</c> (a poda usa essa data).
/// </summary>
public interface IRetentionTableCatalog
{
    IReadOnlyList<RetentionTableTarget> ListTargets();
}

public sealed record RetentionTableTarget(string EntityName, string TableName);
