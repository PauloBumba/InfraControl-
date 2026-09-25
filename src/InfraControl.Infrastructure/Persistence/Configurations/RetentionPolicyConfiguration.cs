using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class RetentionPolicyConfiguration : IEntityTypeConfiguration<RetentionPolicy>
{
    public void Configure(EntityTypeBuilder<RetentionPolicy> builder)
    {
        builder.ToTable("retention_policies");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.DataType)
            .HasColumnName("data_type")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(x => x.Days)
            .HasColumnName("days")
            .IsRequired();

        builder.HasIndex(x => x.DataType)
            .IsUnique()
            .HasDatabaseName("IX_retention_policies_data_type");

        builder.Property(x => x.CreateDateTime)
            .HasColumnName("create_datetime")
            .IsRequired();

        builder.Property(x => x.UpdateDateTime)
            .HasColumnName("update_datetime")
            .IsRequired();
    }
}
