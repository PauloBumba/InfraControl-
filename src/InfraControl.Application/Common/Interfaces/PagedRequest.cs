namespace InfraControl.Application.Common.Interfaces;

/// <summary>
/// O que uma query paginada precisa expor para ser normalizada em um lugar só.
///
/// F29-T001A: não é um record com propriedades embutidas de propósito. As 27
/// queries existentes declaram <c>int Page = 1, int PageSize = 20</c> como
/// propriedades próprias, e o ASP.NET liga esses valores direto da query
/// string (<c>?page=1&amp;pageSize=20</c>). Um objeto aninhado mudaria o formato
/// da URL e quebraria o front sem ninguém pedir. A interface entrega o
/// contrato compartilhado mantendo a forma que a API já expõe.
/// </summary>
public interface IPagedRequest
{
    int Page { get; }

    int PageSize { get; }
}

/// <summary>
/// A definição única de "página válida".
///
/// Antes disto, <c>Math.Max(1, request.Page)</c> e
/// <c>Math.Clamp(request.PageSize, 1, 100)</c> estavam escritos 28 vezes, uma
/// por handler de busca — cópia que é literal até alguém mudar o teto em um
/// lugar só e deixar as outras 27 paginando diferente.
/// </summary>
public static class Paging
{
    /// <summary>Tamanho de página usado quando a query não manda outro (o padrão dos records).</summary>
    public const int DefaultPageSize = 20;

    /// <summary>Teto de página. Existe para que uma requisição não peça a tabela inteira de uma vez.</summary>
    public const int MaxPageSize = 100;

    /// <summary>Página e tamanho já saneados.</summary>
    public readonly record struct Normalized(int Page, int PageSize);

    public static Normalized Normalize(int page, int pageSize) =>
        new(Math.Max(1, page), Math.Clamp(pageSize, 1, MaxPageSize));

    public static Normalized Normalize(IPagedRequest request) =>
        Normalize(request.Page, request.PageSize);
}
