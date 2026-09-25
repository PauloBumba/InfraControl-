using FluentAssertions;
using InfraControl.Domain.RemoteAccess;

namespace InfraControl.UnitTests.RemoteAccess;

/// <summary>
/// F11-T006: DeviceCredential — credencial por device com validações pt-BR.
/// A senha chega já cifrada (ICredentialProtector cuida do texto puro);
/// a entidade só valida presença/comprimento.
/// </summary>
public sealed class DeviceCredentialTests
{
    private static readonly Guid DeviceId = Guid.NewGuid();
    private const string Protected = "v1:c2lkZW50LWNpZmVy";

    [Fact]
    public void Set_ValidCredential_ShouldPersistWithoutRotation()
    {
        var credential = DeviceCredential.Set(DeviceId, "svc-backup", Protected);

        credential.DeviceId.Should().Be(DeviceId);
        credential.Username.Should().Be("svc-backup");
        credential.ProtectedPassword.Should().Be(Protected);
        credential.RotatedAt.Should().BeNull("a primeira gravação não é rotação");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Set_EmptyUsername_ShouldThrowPtBr(string username)
    {
        var action = () => DeviceCredential.Set(DeviceId, username, Protected);

        action.Should().Throw<ArgumentException>().WithMessage("*usuário*");
    }

    [Fact]
    public void Set_UsernameLongerThanMax_ShouldThrowPtBr()
    {
        var action = () => DeviceCredential.Set(
            DeviceId, new string('u', DeviceCredential.UsernameMaxLength + 1), Protected);

        action.Should().Throw<ArgumentException>().WithMessage("*máximo 256*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Set_EmptyPassword_ShouldThrowPtBr(string password)
    {
        var action = () => DeviceCredential.Set(DeviceId, "svc-backup", password);

        action.Should().Throw<ArgumentException>().WithMessage("*senha*");
    }

    [Fact]
    public void Set_EmptyDeviceId_ShouldThrowPtBr()
    {
        var action = () => DeviceCredential.Set(Guid.Empty, "svc-backup", Protected);

        action.Should().Throw<ArgumentException>().WithMessage("*device*");
    }

    [Fact]
    public void Rotate_ShouldReplaceValuesAndStampRotatedAt()
    {
        var credential = DeviceCredential.Set(DeviceId, "svc-backup", Protected);

        credential.Rotate("svc-novo", "v1:c2lkZW5kby1uZXZv");

        credential.Username.Should().Be("svc-novo");
        credential.ProtectedPassword.Should().Be("v1:c2lkZW5kby1uZXZv");
        credential.RotatedAt.Should().NotBeNull("rotação registra o instante");
    }

    [Fact]
    public void Rotate_EmptyUsername_ShouldThrowPtBr()
    {
        var credential = DeviceCredential.Set(DeviceId, "svc-backup", Protected);

        var action = () => credential.Rotate(" ", Protected);

        action.Should().Throw<ArgumentException>().WithMessage("*usuário*");
    }

    [Fact]
    public void Rotate_EmptyPassword_ShouldThrowPtBr()
    {
        var credential = DeviceCredential.Set(DeviceId, "svc-backup", Protected);

        var action = () => credential.Rotate("svc-backup", "");

        action.Should().Throw<ArgumentException>().WithMessage("*senha*");
    }
}
