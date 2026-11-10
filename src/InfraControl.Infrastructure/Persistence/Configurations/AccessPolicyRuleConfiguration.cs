using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class AccessPolicyRuleConfiguration : IEntityTypeConfiguration<AccessPolicyRule>
{
    public void Configure(EntityTypeBuilder<AccessPolicyRule> builder)
    {
        builder.ToTable("access_policy_rules");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.RoleKey)
            .HasColumnName("role_key")
            .HasMaxLength(AccessPolicyRule.RoleKeyMaxLength)
            .IsRequired();

        builder.Property(x => x.ModuleKey)
            .HasColumnName("module_key")
            .HasMaxLength(AccessPolicyRule.ModuleKeyMaxLength)
            .IsRequired();

        builder.Property(x => x.Level)
            .HasColumnName("level")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.CreateDateTime).HasColumnName("create_date_time");
        builder.Property(x => x.UpdateDateTime).HasColumnName("update_date_time");

        builder.HasIndex(x => new { x.RoleKey, x.ModuleKey }).IsUnique();
    }
}
