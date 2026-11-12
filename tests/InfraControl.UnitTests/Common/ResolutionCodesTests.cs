using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Common;

/// <summary>
/// F52-T000: parse do código de baixa (padrão ServiceNow) — string no
/// wire (ADR-061), ignoreCase, 400 pt-BR apontando os valores válidos
/// (molde <c>ChecklistOwnerTypes</c>).
/// </summary>
public sealed class ResolutionCodesTests
{
    [Theory]
    [InlineData("Fixed", ResolutionCode.Fixed)]
    [InlineData("fixed", ResolutionCode.Fixed)]
    [InlineData("WORKAROUND", ResolutionCode.Workaround)]
    [InlineData("Duplicate", ResolutionCode.Duplicate)]
    [InlineData("NoAction", ResolutionCode.NoAction)]
    [InlineData("Other", ResolutionCode.Other)]
    public void Parse_ValidValue_ShouldReturnEnum(string raw, ResolutionCode expected)
        => ResolutionCodes.Parse(raw).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_EmptyOrNull_ShouldThrowPtBr(string? raw)
        => FluentActions.Invoking(() => ResolutionCodes.Parse(raw))
            .Should().Throw<ArgumentException>()
            .WithMessage("*Código de baixa inválido*Fixed*Workaround*Duplicate*NoAction*Other*");

    [Fact]
    public void Parse_UnknownValue_ShouldThrowPtBrWithRawValue()
        => FluentActions.Invoking(() => ResolutionCodes.Parse("banana"))
            .Should().Throw<ArgumentException>()
            .WithMessage("*banana*Fixed*Workaround*Duplicate*NoAction*Other*");
}
