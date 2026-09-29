using FluentAssertions;
using InfraControl.Domain.Entities;

namespace InfraControl.UnitTests.Identity;

/// <summary>
/// F1-T016a (ADR-019): estado MFA por usuário — linha nasce DESLIGADA e só
/// vira obrigatória no Enable (confirm com TOTP válido acontece na camada de
/// serviço; aqui está só o domínio). Recovery codes são hashes SHA-256 de uso
/// único — o domínio guarda o hash e o usedAt, nunca o código em claro.
/// </summary>
public sealed class UserMfaTests
{
    [Fact]
    public void Create_Valid_ShouldStartDisabled()
    {
        var userId = Guid.NewGuid();

        var mfa = UserMfa.Create(userId, "v1:secret-cifrado");

        mfa.UserId.Should().Be(userId);
        mfa.SecretEncrypted.Should().Be("v1:secret-cifrado");
        mfa.Enabled.Should().BeFalse("o setup sozinho não deve destravar o MFA");
        mfa.ConfirmedAt.Should().BeNull();
        mfa.RecoveryCodes.Should().BeEmpty();
    }

    [Fact]
    public void Create_EmptyUserId_ShouldThrowPtBrMessage()
    {
        var act = () => UserMfa.Create(Guid.Empty, "v1:secret");

        act.Should().Throw<ArgumentException>()
            .WithMessage("O id do usuário é obrigatório.*");
    }

    [Fact]
    public void Create_EmptySecret_ShouldThrowPtBrMessage()
    {
        var act = () => UserMfa.Create(Guid.NewGuid(), "  ");

        act.Should().Throw<ArgumentException>()
            .WithMessage("O secret do MFA é obrigatório.*");
    }

    [Fact]
    public void Enable_Valid_ShouldEnableWithConfirmedAtAndCodes()
    {
        var mfa = UserMfa.Create(Guid.NewGuid(), "v1:secret");
        var hashes = Enumerable.Range(0, 10).Select(_ => $"hash-{Guid.NewGuid()}").ToArray();

        mfa.Enable(hashes);

        mfa.Enabled.Should().BeTrue();
        mfa.ConfirmedAt.Should().NotBeNull();
        mfa.RecoveryCodes.Should().HaveCount(10);
        mfa.RecoveryCodes.Should().OnlyContain(code => code.UsedAt == null);
    }

    [Fact]
    public void Enable_WithoutRecoveryCodes_ShouldThrowPtBrMessage()
    {
        var mfa = UserMfa.Create(Guid.NewGuid(), "v1:secret");

        var act = () => mfa.Enable([]);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Os códigos de recuperação são obrigatórios.*");
    }

    [Fact]
    public void UseRecoveryCode_Unknown_ShouldReturnFalse()
    {
        var mfa = UserMfa.Create(Guid.NewGuid(), "v1:secret");
        mfa.Enable(["hash-1"]);

        mfa.UseRecoveryCode("hash-inexistente").Should().BeFalse();
    }

    [Fact]
    public void UseRecoveryCode_FirstUse_ShouldSucceedAndSecondShouldFail()
    {
        var mfa = UserMfa.Create(Guid.NewGuid(), "v1:secret");
        mfa.Enable(["hash-unico", "hash-outro"]);

        mfa.UseRecoveryCode("hash-unico").Should().BeTrue(
            "a primeira conferência deve consumir o código");
        mfa.UseRecoveryCode("hash-unico").Should().BeFalse(
            "código de recuperação é de uso único");

        mfa.RecoveryCodes.Single(code => code.Hash == "hash-unico")
            .UsedAt.Should().NotBeNull("o consumo fica registrado para a auditoria (T016c)");
        mfa.UseRecoveryCode("hash-outro").Should().BeTrue(
            "o consumo de um código não pode invalidar os demais");
    }
}
