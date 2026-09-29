using System.Text.Json;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace InfraControl.Infrastructure.Auth;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("DomainUsers");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Email).HasMaxLength(320).IsRequired();
        builder.HasIndex(user => user.Email).IsUnique();
        builder.Property(user => user.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(user => user.IsActive).IsRequired();
        builder.Ignore(user => user.DomainEvents);

        builder.HasMany(user => user.RoleAssignments)
            .WithOne()
            .HasForeignKey(assignment => assignment.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class UserRoleAssignmentConfiguration : IEntityTypeConfiguration<UserRoleAssignment>
{
    public void Configure(EntityTypeBuilder<UserRoleAssignment> builder)
    {
        builder.ToTable("DomainUserRoles");
        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.HasIndex(assignment => new { assignment.UserId, assignment.Role }).IsUnique();
    }
}

public sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("UserSessions");
        builder.HasKey(session => session.Id);
        builder.Property(session => session.RefreshTokenHash).HasMaxLength(256).IsRequired();
        builder.HasIndex(session => session.RefreshTokenHash).IsUnique();
        builder.HasIndex(session => new { session.UserId, session.RevokedAt, session.ExpiresAt });
        builder.Ignore(session => session.IsActive);
    }
}

/// <summary>
/// MFA TOTP (F1-T016a, ADR-019): 1:1 com o usuário por opção — quem não tem
/// linha não tem MFA. Recovery codes viram JSON (hash SHA-256 + usedAt), no
/// mesmo padrão de conversor/comparador do MonitoringResult.
/// </summary>
public sealed class UserMfaConfiguration : IEntityTypeConfiguration<UserMfa>
{
    public void Configure(EntityTypeBuilder<UserMfa> builder)
    {
        builder.ToTable("user_mfa");
        builder.HasKey(mfa => mfa.Id);
        builder.Property(mfa => mfa.Id).HasColumnName("id");
        builder.Property(mfa => mfa.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(mfa => mfa.SecretEncrypted).HasColumnName("secret_encrypted").HasMaxLength(512).IsRequired();
        builder.Property(mfa => mfa.Enabled).HasColumnName("enabled").IsRequired();
        builder.Property(mfa => mfa.ConfirmedAt).HasColumnName("confirmed_at");

        var codesConverter = new ValueConverter<IReadOnlyCollection<UserMfaRecoveryCode>, string>(
            codes => JsonSerializer.Serialize(codes),
            json => JsonSerializer.Deserialize<List<UserMfaRecoveryCode>>(json) ?? new List<UserMfaRecoveryCode>());

        var codesComparer = new ValueComparer<IReadOnlyCollection<UserMfaRecoveryCode>>(
            (a, b) => a == null && b == null
                || a != null && b != null && a.SequenceEqual(b),
            codes => codes == null
                ? 0
                : codes.Aggregate(
                    0,
                    (hash, code) => HashCode.Combine(hash, code.Hash, code.UsedAt)),
            codes => codes.ToList());

        builder.Property(mfa => mfa.RecoveryCodes)
            .HasColumnName("recovery_codes")
            .HasConversion(codesConverter);
        builder.Property(mfa => mfa.RecoveryCodes)
            .Metadata.SetValueComparer(codesComparer);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(mfa => mfa.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(mfa => mfa.UserId).IsUnique();
    }
}
