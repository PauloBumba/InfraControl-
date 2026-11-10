using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class CompanyDepartmentConfiguration : IEntityTypeConfiguration<CompanyDepartment>
{
    public void Configure(EntityTypeBuilder<CompanyDepartment> builder)
    {
        builder.ToTable("company_departments");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.CompanySettingsId)
            .HasColumnName("company_settings_id")
            .IsRequired();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(CompanyDepartment.NameMaxLength)
            .IsRequired();

        builder.Property(x => x.Responsible)
            .HasColumnName("responsible")
            .HasMaxLength(CompanyDepartment.ResponsibleMaxLength);

        builder.Property(x => x.CreateDateTime).HasColumnName("create_date_time");
        builder.Property(x => x.UpdateDateTime).HasColumnName("update_date_time");

        builder.HasIndex(x => x.CompanySettingsId);
    }
}
