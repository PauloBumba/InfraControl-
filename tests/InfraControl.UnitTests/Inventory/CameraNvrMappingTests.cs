using FluentAssertions;
using InfraControl.Domain.Entities;

namespace InfraControl.UnitTests.Inventory;

/// <summary>
/// F30-T005c: invariantes da entidade CameraNvrMapping — mensagens nascem em
/// pt-BR no domínio (docs/46 §Modelo de domínio).
/// </summary>
public sealed class CameraNvrMappingTests
{
    private static readonly Guid CameraId = Guid.NewGuid();
    private static readonly Guid NvrId = Guid.NewGuid();

    [Fact]
    public void Create_ShouldSetIdsAndChannel()
    {
        var mapping = CameraNvrMapping.Create(CameraId, NvrId, 7);

        mapping.CameraId.Should().Be(CameraId);
        mapping.NvrId.Should().Be(NvrId);
        mapping.ChannelNumber.Should().Be(7);
    }

    [Fact]
    public void Create_ShouldGenerateIdAndTimestamps()
    {
        var mapping = CameraNvrMapping.Create(CameraId, NvrId, 1);

        mapping.Id.Should().NotBe(Guid.Empty);
        mapping.CreateDateTime.Should().NotBe(default(DateTime));
        mapping.UpdateDateTime.Should().NotBe(default(DateTime));
    }

    [Fact]
    public void Create_WithoutCameraId_ShouldThrowPtBrMessage()
    {
        var action = () => CameraNvrMapping.Create(Guid.Empty, NvrId, 1);

        action.Should().Throw<ArgumentException>()
            .WithMessage("O id da câmera é obrigatório.*");
    }

    [Fact]
    public void Create_WithoutNvrId_ShouldThrowPtBrMessage()
    {
        var action = () => CameraNvrMapping.Create(CameraId, Guid.Empty, 1);

        action.Should().Throw<ArgumentException>()
            .WithMessage("O id do NVR é obrigatório.*");
    }

    [Fact]
    public void Create_WithZeroChannel_ShouldThrowPtBrMessage()
    {
        var action = () => CameraNvrMapping.Create(CameraId, NvrId, 0);

        action.Should().Throw<ArgumentException>()
            .WithMessage("O número do canal deve ser maior ou igual a 1.*");
    }

    [Fact]
    public void Create_WithNegativeChannel_ShouldThrow()
    {
        var action = () => CameraNvrMapping.Create(CameraId, NvrId, -2);

        action.Should().Throw<ArgumentException>()
            .WithMessage("O número do canal deve ser maior ou igual a 1.*");
    }

    [Fact]
    public void Update_ShouldRevalidateAndReplaceAllFields()
    {
        var mapping = CameraNvrMapping.Create(CameraId, NvrId, 1);
        var newCameraId = Guid.NewGuid();
        var newNvrId = Guid.NewGuid();

        mapping.Update(newCameraId, newNvrId, 9);

        mapping.CameraId.Should().Be(newCameraId);
        mapping.NvrId.Should().Be(newNvrId);
        mapping.ChannelNumber.Should().Be(9);
    }

    [Fact]
    public void Update_WithInvalidChannel_ShouldThrowAndKeepState()
    {
        var mapping = CameraNvrMapping.Create(CameraId, NvrId, 3);

        var action = () => mapping.Update(CameraId, NvrId, 0);

        action.Should().Throw<ArgumentException>();
        mapping.ChannelNumber.Should().Be(3);
        mapping.CameraId.Should().Be(CameraId);
    }
}
