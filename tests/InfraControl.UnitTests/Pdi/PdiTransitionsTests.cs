using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Pdi;

/// <summary>
/// Tabela de transições do ciclo de vida do PDI — setas exatas do
/// diagrama docs/30 §3 (ADR-029): 9 estados, sem voltar de
/// Completed/Cancelled, Cancelled só a partir de Active.
/// </summary>
public sealed class PdiTransitionsTests
{
    public static TheoryData<PdiStatus, PdiStatus> AllowedMoves => new()
    {
        { PdiStatus.Draft, PdiStatus.Planned },
        { PdiStatus.Planned, PdiStatus.AwaitingAcceptance },
        { PdiStatus.AwaitingAcceptance, PdiStatus.Active },
        { PdiStatus.Active, PdiStatus.InProgress },
        { PdiStatus.Active, PdiStatus.Replanned },
        { PdiStatus.Active, PdiStatus.Cancelled },
        { PdiStatus.InProgress, PdiStatus.UnderEvaluation },
        { PdiStatus.UnderEvaluation, PdiStatus.Completed },
        // ADR-036 (Fase 6): resultado "Replanejado" devolve o plano ao ciclo.
        { PdiStatus.UnderEvaluation, PdiStatus.Replanned },
        { PdiStatus.Replanned, PdiStatus.Active }
    };

    [Theory]
    [MemberData(nameof(AllowedMoves))]
    public void AllowedMoves_ShouldBePermitted(PdiStatus from, PdiStatus to)
        => PdiTransitions.CanMove(from, to).Should().BeTrue();

    [Theory]
    [InlineData(PdiStatus.Draft, PdiStatus.Active)]
    [InlineData(PdiStatus.Draft, PdiStatus.Cancelled)]
    [InlineData(PdiStatus.Planned, PdiStatus.Active)]
    [InlineData(PdiStatus.Active, PdiStatus.Completed)]
    [InlineData(PdiStatus.InProgress, PdiStatus.Completed)]
    [InlineData(PdiStatus.Completed, PdiStatus.Active)]
    [InlineData(PdiStatus.Cancelled, PdiStatus.Active)]
    [InlineData(PdiStatus.Replanned, PdiStatus.InProgress)]
    public void ForbiddenMoves_ShouldBeRejected(PdiStatus from, PdiStatus to)
        => PdiTransitions.CanMove(from, to).Should().BeFalse();

    [Fact]
    public void SameStatus_ShouldAlwaysBeAllowed()
    {
        foreach (PdiStatus status in Enum.GetValues<PdiStatus>())
            PdiTransitions.CanMove(status, status).Should().BeTrue(
                $"{status} → {status} é edição sem transição");
    }

    [Fact]
    public void TerminalStates_ShouldHaveNoNext()
    {
        PdiTransitions.Next(PdiStatus.Completed).Should().BeEmpty();
        PdiTransitions.Next(PdiStatus.Cancelled).Should().BeEmpty();
    }

    [Fact]
    public void EnsureCanMove_WhenInvalid_ShouldThrowPortuguese400()
    {
        var act = () => PdiTransitions.EnsureCanMove(PdiStatus.Draft, PdiStatus.Completed);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Transição de status inválida*");
    }
}
