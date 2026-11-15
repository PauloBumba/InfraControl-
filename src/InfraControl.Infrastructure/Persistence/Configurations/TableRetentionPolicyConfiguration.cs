using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class TableRetentionPolicyConfiguration
    : IEntityTypeConfiguration<TableRetentionPolicy>
{
    public void Configure(EntityTypeBuilder<TableRetentionPolicy> builder)
    {
        builder.ToTable("table_retention_policies");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.EntityName)
            .HasColumnName("entity_name")
            .HasMaxLength(TableRetentionPolicy.EntityNameMaxLength)
            .IsRequired();

        builder.Property(x => x.Days)
            .HasColumnName("days")
            .IsRequired();

        builder.HasIndex(x => x.EntityName)
            .IsUnique()
            .HasDatabaseName("IX_table_retention_policies_entity_name");

        builder.Property(x => x.CreateDateTime)
            .HasColumnName("create_datetime")
            .IsRequired();

        builder.Property(x => x.UpdateDateTime)
            .HasColumnName("update_datetime")
            .IsRequired();
    }
}
