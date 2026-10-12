namespace InfraControl.Application.Common.Interfaces;

/// <summary>
/// Página de resultado devolvida por toda busca paginada do produto.
///
/// F29-T001A: o tipo saiu do arquivo de <see cref="IAuditReader"/> para virar
/// contrato de verdade. O namespace ficou o MESMO de propósito — 25 portas de
/// repositório e 29 queries já o usam, e mudar namespace para arrumar
/// endereço seria 25 edições sem ganho nenhum.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyCollection<T> Items,
    int TotalCount,
    int Page,
    int PageSize);
