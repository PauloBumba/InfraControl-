namespace InfraControl.Application.Operations.Schema.Ports;

/// <summary>
/// Leitura do modelo EF (tabelas, colunas, FKs) — implementada em
/// Infrastructure sobre o <c>IModel</c> do DbContext.
/// </summary>
public interface ISchemaCatalog
{
    IReadOnlyList<SchemaEntityDto> ListEntities();
}
