using InfraControl.Domain.Common;
using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Recovery code de uso único (F1-T016a, ADR-019): o domínio guarda apenas o
/// hash SHA-256 do código normalizado — nunca o código em claro. O
/// <see cref="UsedAt"/> fica registrado (não apaga) para a auditoria da
/// F1-T016c saber quantos códigos foram gastos.
/// </summary>
public sealed record UserMfaRecoveryCode(string Hash, DateTime? UsedAt);

/// <summary>
/// MFA TOTP por usuário (F1-T016a, ADR-019). Linha 1:1 com o usuário — a
/// ausência da linha significa "sem MFA" (opt-in). O secret fica cifrado com
/// AES-256-GCM (ICredentialProtector, mesmo precedente do F11-T006); o estado
/// nasce DESLIGADO (<see cref="Enabled"/>=false) e só vira obrigatório no
/// <see cref="Enable"/>, chamado após o confirm com TOTP válido.
/// </summary>
public sealed class UserMfa : AggregateRoot
{
    public Guid UserId { get; private set; }
    public string SecretEncrypted { get; private set; } = string.Empty;
    public bool Enabled { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public IReadOnlyCollection<UserMfaRecoveryCode> RecoveryCodes { get; private set; } = [];

    private UserMfa() { }

    private UserMfa(Guid id, Guid userId, string secretEncrypted) : base(id)
    {
        if (userId == Guid.Empty) throw new ArgumentException("O id do usuário é obrigatório.", nameof(userId));
        if (string.IsNullOrWhiteSpace(secretEncrypted)) throw new ArgumentException("O secret do MFA é obrigatório.", nameof(secretEncrypted));

        UserId = userId;
        SecretEncrypted = secretEncrypted;
    }

    public static UserMfa Create(Guid userId, string secretEncrypted)
        => new(Guid.NewGuid(), userId, secretEncrypted);

    /// <summary>
    /// Reemite o secret de um setup PENDENTE (nunca de uma conta já
    /// habilitada — isso é barrado antes, no serviço). Descarta o secret
    /// anterior sem tocar em <see cref="Enabled"/> (que é false aqui).
    /// </summary>
    public void ResetSecret(string secretEncrypted)
    {
        if (string.IsNullOrWhiteSpace(secretEncrypted)) throw new ArgumentException("O secret do MFA é obrigatório.", nameof(secretEncrypted));
        if (Enabled) throw new InvalidOperationException("MFA já está habilitado. Desative antes de reconfigurar.");

        SecretEncrypted = secretEncrypted;
        ConfirmedAt = null;
    }

    /// <summary>
    /// Liga o MFA após o confirm. Os recovery codes chegam já normalizados e
    /// hasheados pela camada de serviço (o domínio não conhece Base32/SHA-256).
    /// </summary>
    public void Enable(IReadOnlyCollection<string> recoveryCodeHashes)
    {
        if (recoveryCodeHashes is null || recoveryCodeHashes.Count == 0)
            throw new ArgumentException("Os códigos de recuperação são obrigatórios.", nameof(recoveryCodeHashes));

        RecoveryCodes = recoveryCodeHashes
            .Select(hash => new UserMfaRecoveryCode(hash, null))
            .ToList();
        Enabled = true;
        ConfirmedAt = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
    }

    /// <summary>
    /// Consome um recovery code: só o primeiro uso de um hash específico
    /// retorna true; o consumo fica gravado em <see cref="UserMfaRecoveryCode.UsedAt"/>.
    /// </summary>
    public bool UseRecoveryCode(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash)) return false;

        var codes = RecoveryCodes.ToList();
        var index = codes.FindIndex(code => code.Hash == hash && code.UsedAt is null);
        if (index < 0) return false;

        codes[index] = codes[index] with { UsedAt = AppTimeProvider.Current.GetUtcNow().UtcDateTime };
        RecoveryCodes = codes;
        return true;
    }
}
