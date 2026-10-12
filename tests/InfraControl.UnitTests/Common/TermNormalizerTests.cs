using FluentAssertions;
using InfraControl.Application.Common.Interfaces;

namespace InfraControl.UnitTests.Common;

/// <summary>
/// F29-T001A — `var term = string.IsNullOrWhiteSpace(request.Term) ? null :
/// request.Term.Trim();` estava escrito 14 vezes, byte a byte igual, em 14
/// handlers. É uma linha, mas é uma linha que decide o que a busca significa:
/// vazio vira "sem filtro" (nunca "filtrar por nada"), e o espaço sobrando na
/// ponta não pode anular o resultado.
/// </summary>
public sealed class TermNormalizerTests
{
    [Theory]
    [InlineData((string?)null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   \t  ")]
    [InlineData("\r\n")]
    public void Normalize_ShouldTurnBlankIntoNoFilter(string? term)
        => TermNormalizer.Normalize(term).Should().BeNull();

    [Theory]
    [InlineData("rack", "rack")]
    [InlineData("  rack  ", "rack")]
    [InlineData("\trouter\n", "router")]
    [InlineData("rack12 ", "rack12")]
    public void Normalize_ShouldTrimTheEnds(string input, string expected)
        => TermNormalizer.Normalize(input).Should().Be(expected);

    [Fact]
    public void Normalize_ShouldNotLowercase_TheFoldingIsTheRepositorysJob()
    {
        // A normalização de caixa acontece na consulta (ToLower/Contains), não
        // aqui: trazer "RACK" de volta mudaria silenciosamente o que 14
        // repositórios procuram.
        TermNormalizer.Normalize("RACK A").Should().Be("RACK A");
    }
}
