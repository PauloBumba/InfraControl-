using FluentAssertions;
using InfraControl.Domain.RemoteAccess;

namespace InfraControl.UnitTests.RemoteAccess;

/// <summary>
/// F11-T001: RemoteSession — registro de sessão de acesso remoto com
/// ciclo de vida auditável (ADR-013, docs/10).
/// </summary>
public sealed class RemoteSessionTests
{
    private static readonly Guid DeviceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Open_ShouldCreateActiveSessionWithAuditFields()
    {
        var session = RemoteSession.Open(DeviceId, UserId, RemoteProtocol.Ssh);

        session.DeviceId.Should().Be(DeviceId);
        session.UserId.Should().Be(UserId);
        session.Protocol.Should().Be(RemoteProtocol.Ssh);
        session.Status.Should().Be(RemoteSessionStatus.Active);
        session.StartedAt.Should().NotBe(default(DateTime), "StartedAt é definido na abertura");
        session.EndedAt.Should().BeNull();
        session.FailureReason.Should().BeNull();
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void Open_WithEmptyDeviceId_ShouldThrowPtBr(string emptyId)
    {
        var action = () => RemoteSession.Open(Guid.Parse(emptyId), UserId, RemoteProtocol.Ssh);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*Device*");
    }

    [Fact]
    public void Open_WithEmptyUserId_ShouldThrowPtBr()
    {
        var action = () => RemoteSession.Open(DeviceId, Guid.Empty, RemoteProtocol.Rdp);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*Usuário*");
    }

    [Fact]
    public void Open_WithUndefinedProtocol_ShouldThrowPtBr()
    {
        var action = () => RemoteSession.Open(DeviceId, UserId, (RemoteProtocol)99);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Protocolo*");
    }

    [Fact]
    public void Close_FromActive_ShouldEndSession()
    {
        var session = RemoteSession.Open(DeviceId, UserId, RemoteProtocol.Rdp);

        session.Close();

        session.Status.Should().Be(RemoteSessionStatus.Closed);
        session.EndedAt.Should().NotBeNull();
        session.FailureReason.Should().BeNull();
    }

    [Fact]
    public void Close_WhenAlreadyFinalized_ShouldThrowPtBr()
    {
        var session = RemoteSession.Open(DeviceId, UserId, RemoteProtocol.Ssh);
        session.Close();

        var action = () => session.Close();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*finalizada*");
    }

    [Fact]
    public void Fail_FromActive_ShouldRecordReason()
    {
        var session = RemoteSession.Open(DeviceId, UserId, RemoteProtocol.Ssh);

        session.Fail("  Falha na autenticação SSH  ");

        session.Status.Should().Be(RemoteSessionStatus.Failed);
        session.FailureReason.Should().Be("Falha na autenticação SSH");
        session.EndedAt.Should().NotBeNull();
    }

    [Fact]
    public void Fail_WithoutReason_ShouldThrowPtBr()
    {
        var session = RemoteSession.Open(DeviceId, UserId, RemoteProtocol.Ssh);

        var action = () => session.Fail("   ");

        action.Should().Throw<ArgumentException>()
            .WithMessage("*motivo*");
    }

    [Fact]
    public void Fail_WithReasonAboveMaxLength_ShouldThrowPtBr()
    {
        var session = RemoteSession.Open(DeviceId, UserId, RemoteProtocol.Ssh);
        var tooLong = new string('x', RemoteSession.FailureReasonMaxLength + 1);

        var action = () => session.Fail(tooLong);

        action.Should().Throw<ArgumentException>()
            .WithMessage($"*{RemoteSession.FailureReasonMaxLength}*");
    }

    [Fact]
    public void Fail_WhenAlreadyFinalized_ShouldThrowPtBr()
    {
        var session = RemoteSession.Open(DeviceId, UserId, RemoteProtocol.Ssh);
        session.Fail("timeout");

        var action = () => session.Fail("outro motivo");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*finalizada*");
    }
}
