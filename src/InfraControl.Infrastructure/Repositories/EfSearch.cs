using InfraControl.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.Infrastructure.Repositories;

/// <summary>
/// A parte mecânica de toda busca paginada: contar, saltar e pegar a página.
///
/// F29-T001A — o mesmo corpo estava copiado em ~25 repositórios:
/// <c>CountAsync</c> → <c>OrderBy</c> → <c>Skip((page - 1) * pageSize)</c> →
/// <c>Take(pageSize)</c> → <c>new PagedResult&lt;T&gt;(…)</c>. O que varia de um
/// repositório para outro é o FILTRO e a ORDENAÇÃO, então quem chama passa a
/// <c>IQueryable</c> já filtrado e ordenado; esta classe cuida só do que é igual
/// em todos.
///
/// Normaliza a página sozinha (<see cref="Paging"/>). Antes, o repositório
/// acreditava no que o chamador mandasse: <c>pageSize: 0</c> virava
/// <c>Take(0)</c> — lista vazia sem erro nenhum — e <c>page: 0</c> virava
/// <c>Skip(-pageSize)</c>, que só não explodia por acaso. Um porto de
/// repositório é chamado por handlers, workers e outros serviços: cada um
/// precisava lembrar de normalizar.
/// </summary>
public static class EfSearch
{
    public static async Task<PagedResult<T>> PageAsync<T>(
        IQueryable<T> ordered,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var paging = Paging.Normalize(page, pageSize);

        var totalCount = await ordered.CountAsync(cancellationToken);
        var items = await ordered
            .Skip((paging.Page - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, totalCount, paging.Page, paging.PageSize);
    }
}
