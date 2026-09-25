using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Discovery;

// F4-T001: DiscoveryRun — uma execução de discovery contra UM device alvo
// (fluxo decidido pelo usuário: gatilho manual por device; T001 = estrutura +
// leitura, Complete/Fail serão chamados pelos providers das T003/T004).
public sealed class DiscoveryRunTests
{
    [Fact]
    public void Start_Valid_ShouldSetRunningState()
    {
        var deviceId = Guid.NewGuid();
        var startedAt = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

        var run = DiscoveryRun.Start(deviceId, startedAt);

        run.Id.Should().NotBe(Guid.Empty);
        run.DeviceId.Should().Be(deviceId);
        run.Status.Should().Be(DiscoveryRunStatus.Running);
        run.StartedAt.Should().Be(startedAt);
        run.FinishedAt.Should().BeNull();
        run.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Start_EmptyDeviceId_ShouldThrowPtBrMessage()
    {
        var act = () => DiscoveryRun.Start(Guid.Empty);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O id do device é obrigatório.*");
    }

    [Fact]
    public void Complete_Valid_ShouldFinalizeAsCompleted()
    {
        var run = DiscoveryRun.Start(Guid.NewGuid());

        run.Complete();

        run.Status.Should().Be(DiscoveryRunStatus.Completed);
        run.FinishedAt.Should().NotBeNull();
        run.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Complete_WhenAlreadyFinalized_ShouldThrowConflictMessage()
    {
        var run = DiscoveryRun.Start(Guid.NewGuid());
        run.Complete();

        var act = () => run.Complete();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("A execução de discovery já foi finalizada.");
    }

    [Fact]
    public void Fail_Valid_ShouldFinalizeAsFailedWithMessage()
    {
        var run = DiscoveryRun.Start(Guid.NewGuid());

        run.Fail("  timeout SNMP 161  ");

        run.Status.Should().Be(DiscoveryRunStatus.Failed);
        run.FinishedAt.Should().NotBeNull();
        run.ErrorMessage.Should().Be("timeout SNMP 161");
    }

    [Fact]
    public void Fail_EmptyMessage_ShouldThrowPtBrMessage()
    {
        var run = DiscoveryRun.Start(Guid.NewGuid());

        var act = () => run.Fail("   ");

        act.Should().Throw<ArgumentException>()
            .WithMessage("A mensagem de erro é obrigatória.*");
    }

    [Fact]
    public void Fail_MessageTooLong_ShouldThrowPtBrMessage()
    {
        var run = DiscoveryRun.Start(Guid.NewGuid());

        var act = () => run.Fail(new string('a', DiscoveryRun.ErrorMessageMaxLength + 1));

        act.Should().Throw<ArgumentException>()
            .WithMessage($"A mensagem de erro pode ter no máximo {DiscoveryRun.ErrorMessageMaxLength} caracteres.*");
    }

    [Fact]
    public void Fail_WhenAlreadyFinalized_ShouldThrowConflictMessage()
    {
        var run = DiscoveryRun.Start(Guid.NewGuid());
        run.Complete();

        var act = () => run.Fail("timeout SNMP");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("A execução de discovery já foi finalizada.");
    }
}
