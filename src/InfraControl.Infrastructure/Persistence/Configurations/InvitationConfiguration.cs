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

        builder.Property(x => x.ProjectId)
            .HasColumnName("project_id");

        builder.Property(x => x.ProjectCode)
            .HasColumnName("project_code")
            .HasMaxLength(Project.CodeMaxLength);

        builder.Property(x => x.ProjectTitle)
            .HasColumnName("project_title")
            .HasMaxLength(Project.TitleMaxLength);

        builder.Property(x => x.ProjectRole)
            .HasColumnName("project_role")
            .HasMaxLength(30)
            .HasConversion<string>();

        builder.Property(x => x.AcceptedUserId)
            .HasColumnName("accepted_user_id");

        builder.HasMany(x => x.RoleAssignments)
            .WithOne()
            .HasForeignKey(x => x.InvitationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.Email, x.Status })
            .HasDatabaseName("ix_invitations_email_status");

        builder.HasIndex(x => x.ExpiresAt)
            .HasDatabaseName("ix_invitations_expires_at");

        builder.HasIndex(x => x.ProjectId)
            .HasDatabaseName("ix_invitations_project_id");
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
