using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Network;

// F3-T005: Connection dirigida (docs/03 — Interface N:N Interface via
// Connection; decisão do usuário: par DIRIGIDO, só self-loop proibido).
public sealed class ConnectionTests
{
    [Fact]
    public void Create_Valid_ShouldSetDirectedPair()
    {
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var connection = Connection.Create(sourceId, targetId);

        connection.SourceInterfaceId.Should().Be(sourceId);
        connection.TargetInterfaceId.Should().Be(targetId);
        connection.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_EmptySource_ShouldThrowPtBrMessage()
    {
        var act = () => Connection.Create(Guid.Empty, Guid.NewGuid());

        act.Should().Throw<ArgumentException>()
            .WithMessage("O id da interface de origem é obrigatório.*");
    }

    [Fact]
    public void Create_EmptyTarget_ShouldThrowPtBrMessage()
    {
        var act = () => Connection.Create(Guid.NewGuid(), Guid.Empty);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O id da interface de destino é obrigatório.*");
    }

    [Fact]
    public void Create_SelfLoop_ShouldThrowPtBrMessage()
    {
        var interfaceId = Guid.NewGuid();

        var act = () => Connection.Create(interfaceId, interfaceId);

        act.Should().Throw<ArgumentException>()
            .WithMessage("A conexão não pode ligar a interface a ela mesma.*");
    }

    [Fact]
    public void Repoint_ShouldMoveTheDirectedPair()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var connection = Connection.Create(a, b);

        connection.Repoint(a, c);

        connection.SourceInterfaceId.Should().Be(a);
        connection.TargetInterfaceId.Should().Be(c);
        connection.Id.Should().Be(connection.Id, "reapontar não troca a identidade");
    }

    [Fact]
    public void Repoint_SelfLoop_ShouldThrowPtBrMessage()
    {
        var connection = Connection.Create(Guid.NewGuid(), Guid.NewGuid());
        var same = Guid.NewGuid();

        var act = () => connection.Repoint(same, same);

        act.Should().Throw<ArgumentException>()
            .WithMessage("A conexão não pode ligar a interface a ela mesma.*");
        connection.SourceInterfaceId.Should().NotBe(same, "estado preservado após falha");
    }

    [Fact]
    public void Create_ReversePair_IsAllowed_Directed()
    {
        // (a,b) e (b,a) são conexões distintas — par dirigido.
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var forward = Connection.Create(a, b);
        var backward = Connection.Create(b, a);

        forward.TargetInterfaceId.Should().Be(b);
        backward.TargetInterfaceId.Should().Be(a);
    }

    // ── F53-T001: origem/confiança da conexão (spec 11.7) ────────────────

    [Fact]
    public void Create_WithoutOrigin_ShouldDefaultToManualConfirmed()
    {
        var connection = Connection.Create(Guid.NewGuid(), Guid.NewGuid());

        connection.Source.Should().Be(ConnectionSource.Manual);
        connection.Confidence.Should().Be(ConnectionConfidence.Confirmed);
    }

    [Fact]
    public void Create_WithSourceOnly_ShouldDefaultConfidenceToConfirmed()
    {
        var connection = Connection.Create(
            Guid.NewGuid(), Guid.NewGuid(), ConnectionSource.Lldp);

        connection.Source.Should().Be(ConnectionSource.Lldp);
        connection.Confidence.Should().Be(ConnectionConfidence.Confirmed);
    }

    [Fact]
    public void Create_WithExplicitOriginAndConfidence_ShouldPersistBoth()
    {
        var connection = Connection.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            ConnectionSource.Unifi, ConnectionConfidence.Estimated);

        connection.Source.Should().Be(ConnectionSource.Unifi);
        connection.Confidence.Should().Be(ConnectionConfidence.Estimated);
    }

    [Fact]
    public void Repoint_ShouldMarkAsManualConfiguration()
    {
        // Spec 11.3 — ajuste manual identifica a conexão como configuração
        // manual (e não como descoberta automática).
        var connection = Connection.Create(
            Guid.NewGuid(), Guid.NewGuid(), ConnectionSource.Lldp);

        connection.Repoint(Guid.NewGuid(), Guid.NewGuid());

        connection.Source.Should().Be(ConnectionSource.Manual);
        connection.Confidence.Should().Be(ConnectionConfidence.Confirmed);
    }
}
