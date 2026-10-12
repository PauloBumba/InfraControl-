using FluentAssertions;
using InfraControl.Application.Common.Interfaces;

namespace InfraControl.UnitTests.Common;

/// <summary>
/// F29-T001A — o parâmetro de paginação não tinha contrato: cada query
/// redeclarava `int Page = 1, int PageSize = 20` (59 ocorrências em 27
/// arquivos) e cada handler repedia as MESMAS duas linhas
/// (`Math.Max(1, request.Page)` + `Math.Clamp(request.PageSize, 1, 100)`)
/// 28 vezes. Aqui fica travado o que "página válida" significa — em um lugar só.
/// </summary>
public sealed class PagingTests
{
    private sealed record Sample(int Page, int PageSize) : IPagedRequest;

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-7, 1)]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    public void Normalize_ShouldNeverFallBackBeforeTheFirstPage(int page, int expected)
        => Paging.Normalize(page, 20).Page.Should().Be(expected);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(20, 20)]
    public void Normalize_ShouldClampPageSizeToASingleItemAtLeast(int pageSize, int expected)
        => Paging.Normalize(1, pageSize).PageSize.Should().Be(expected);

    [Fact]
    public void Normalize_ShouldEnforceTheCapSoOneRequestCannotAskForEverything()
        => Paging.Normalize(1, 5_000).PageSize.Should().Be(Paging.MaxPageSize);

    [Fact]
    public void Normalize_ShouldKeepTheNumbersTheApiAlreadyUsed()
    {
        // Não é gosto: são os limites que as 28 queries já aplicavam. Mudar
        // aqui é mudar a resposta da API — e isso não é o papel de uma task
        // de refatoração.
        Paging.DefaultPageSize.Should().Be(20);
        Paging.MaxPageSize.Should().Be(100);
    }

    [Fact]
    public void Normalize_ShouldAcceptTheRequestItself()
        => Paging.Normalize(new Sample(Page: 0, PageSize: 999))
            .Should().Be(new Paging.Normalized(1, Paging.MaxPageSize));
}
