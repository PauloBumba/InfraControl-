namespace InfraControl.Application.Inventory.GlobalSearch;

/// <summary>
/// Item da busca global (F2-T007): um resultado tipado de qualquer fonte do
/// inventário. Subtitle carrega dado secundário de exibição (ex.: IP do device).
/// </summary>
public sealed record GlobalSearchItemDto(
    string Type,
    Guid Id,
    string Name,
    string? Subtitle,
    /// <summary>
    /// Rota de destino quando o item não é uma entidade do inventário
    /// (ex.: "network" → Network Explorer). Null = navegação padrão por tipo.
    /// </summary>
    string? Target = null);

/// <summary>Resultado agregado da busca global — sucesso vai em data do envelope.</summary>
public sealed record GlobalSearchResultDto(IReadOnlyCollection<GlobalSearchItemDto> Items, int TotalCount);
