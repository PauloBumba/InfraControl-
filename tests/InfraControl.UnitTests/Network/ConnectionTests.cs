using InfraControl.Domain.Entities;

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
}
