using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class CompanySettingsConfiguration : IEntityTypeConfiguration<CompanySettings>
{
    public void Configure(EntityTypeBuilder<CompanySettings> builder)
    {
        builder.ToTable("company_settings");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(CompanySettings.NameMaxLength)
            .IsRequired();

        builder.Property(x => x.LegalName)
            .HasColumnName("legal_name")
            .HasMaxLength(CompanySettings.LegalNameMaxLength);

        builder.Property(x => x.Cnpj)
            .HasColumnName("cnpj")
            .HasMaxLength(CompanySettings.DocumentMaxLength);

        builder.Property(x => x.LogoPath)
            .HasColumnName("logo_path")
            .HasMaxLength(CompanySettings.LogoPathMaxLength);

        builder.Property(x => x.Street).HasColumnName("street").HasMaxLength(CompanySettings.AddressMaxLength);
        builder.Property(x => x.Number).HasColumnName("number").HasMaxLength(20);
        builder.Property(x => x.Complement).HasColumnName("complement").HasMaxLength(CompanySettings.AddressMaxLength);
        builder.Property(x => x.District).HasColumnName("district").HasMaxLength(CompanySettings.AddressMaxLength);
        builder.Property(x => x.City).HasColumnName("city").HasMaxLength(CompanySettings.AddressMaxLength);
        builder.Property(x => x.State).HasColumnName("state").HasMaxLength(40);
        builder.Property(x => x.ZipCode).HasColumnName("zip_code").HasMaxLength(CompanySettings.ZipCodeMaxLength);
        builder.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(CompanySettings.ContactMaxLength);
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(CompanySettings.ContactMaxLength);
        builder.Property(x => x.Website).HasColumnName("website").HasMaxLength(CompanySettings.ContactMaxLength);

        builder.Property(x => x.CreateDateTime).HasColumnName("create_date_time");
        builder.Property(x => x.UpdateDateTime).HasColumnName("update_date_time");

        builder.HasMany(x => x.Departments)
            .WithOne()
            .HasForeignKey(x => x.CompanySettingsId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
