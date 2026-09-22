using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("invitations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.Email)
            .HasColumnName("email")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .IsRequired();

        builder.Property(x => x.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .HasColumnName("created_by")
            .IsRequired();

        builder.Property(x => x.DecidedAt)
            .HasColumnName("decided_at");

        builder.HasMany(x => x.RoleAssignments)
            .WithOne()
            .HasForeignKey(x => x.InvitationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.Email, x.Status })
            .HasDatabaseName("ix_invitations_email_status");

        builder.HasIndex(x => x.ExpiresAt)
            .HasDatabaseName("ix_invitations_expires_at");
    }
}

public class InvitationRoleConfiguration : IEntityTypeConfiguration<InvitationRole>
{
    public void Configure(EntityTypeBuilder<InvitationRole> builder)
    {
        builder.ToTable("invitation_roles");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.InvitationId)
            .HasColumnName("invitation_id")
            .IsRequired();

        builder.Property(x => x.Role)
            .HasColumnName("role")
            .IsRequired();
    }
}
