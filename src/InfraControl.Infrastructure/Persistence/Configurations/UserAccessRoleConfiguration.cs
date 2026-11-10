using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class UserAccessRoleConfiguration : IEntityTypeConfiguration<UserAccessRole>
{
    public void Configure(EntityTypeBuilder<UserAccessRole> builder)
    {
        builder.ToTable("user_access_roles");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(x => x.RoleKey)
            .HasColumnName("role_key")
            .HasMaxLength(AccessRole.KeyMaxLength)
            .IsRequired();

        builder.Property(x => x.CreateDateTime).HasColumnName("create_date_time");
        builder.Property(x => x.UpdateDateTime).HasColumnName("update_date_time");

        builder.HasIndex(x => new { x.UserId, x.RoleKey }).IsUnique();
    }
}
