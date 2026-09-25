using InfraControl.Domain.Entities;
using InfraControl.Domain.RemoteAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class DeviceCredentialConfiguration : IEntityTypeConfiguration<DeviceCredential>
{
    public void Configure(EntityTypeBuilder<DeviceCredential> builder)
    {
        builder.ToTable("device_credentials");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.DeviceId)
            .HasColumnName("device_id")
            .IsRequired();

        builder.Property(x => x.Username)
            .HasColumnName("username")
            .HasMaxLength(DeviceCredential.UsernameMaxLength)
            .IsRequired();

        builder.Property(x => x.ProtectedPassword)
            .HasColumnName("protected_password")
            .HasMaxLength(DeviceCredential.ProtectedPasswordMaxLength)
            .IsRequired();

        builder.Property(x => x.RotatedAt)
            .HasColumnName("rotated_at");

        builder.Property(x => x.CreateDateTime)
            .HasColumnName("create_datetime");

        builder.Property(x => x.UpdateDateTime)
            .HasColumnName("update_datetime");

        // Uma credencial por device — upsert é a única forma de escrita.
        builder.HasIndex(x => x.DeviceId)
            .IsUnique()
            .HasDatabaseName("IX_device_credentials_device_id");

        // Exclusão do device apaga a credencial junto (mesmo comportamento
        // das sessões remote — cascade).
        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
