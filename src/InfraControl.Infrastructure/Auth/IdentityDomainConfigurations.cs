using InfraControl.Domain.Enums;
using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

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
