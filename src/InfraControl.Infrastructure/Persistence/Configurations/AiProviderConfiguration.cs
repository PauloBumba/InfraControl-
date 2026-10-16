using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

/// <summary>
/// ADR-057 (FAI-T011a): cadastro de provider de IA — <c>models</c> é
/// coleção primitiva (EF → coluna <c>text[]</c> no Npgsql, JSON no
/// Sqlite de teste), mesmo molde do <c>tags</c> do artigo; backing
/// field explícito porque a propriedade é só-leitura.
/// </summary>
public sealed class AiProviderConfiguration : IEntityTypeConfiguration<AiProvider>
{
    public void Configure(EntityTypeBuilder<AiProvider> builder)
    {
        builder.ToTable("ai_providers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(AiProvider.MaxNameLength)
            .IsRequired();

        builder.HasIndex(x => x.Name)
            .IsUnique();

        builder.Property(x => x.BaseUrl)
            .HasColumnName("base_url")
            .HasMaxLength(AiProvider.MaxBaseUrlLength)
            .IsRequired();

        builder.Property(x => x.ApiKeyEncrypted)
            .HasColumnName("api_key_encrypted")
            .HasMaxLength(AiProvider.MaxApiKeyEncryptedLength);

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(x => x.Models)
            .HasColumnName("models")
            .HasField("_models");

        builder.Property(x => x.CreateDateTime)
            .HasColumnName("create_date_time");

        builder.Property(x => x.UpdateDateTime)
            .HasColumnName("update_date_time");
    }
}
