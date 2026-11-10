using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class RequestMetricBucketConfiguration : IEntityTypeConfiguration<RequestMetricBucket>
{
    public void Configure(EntityTypeBuilder<RequestMetricBucket> builder)
    {
        builder.ToTable("request_metric_buckets");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.MinuteUtc)
            .HasColumnName("minute_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.Route)
            .HasColumnName("route")
            .HasMaxLength(RequestMetricBucket.RouteMaxLength)
            .IsRequired();

        builder.Property(x => x.StatusClass)
            .HasColumnName("status_class")
            .HasMaxLength(RequestMetricBucket.StatusClassMaxLength)
            .IsRequired();

        builder.Property(x => x.Count).HasColumnName("count").IsRequired();
        builder.Property(x => x.Errors).HasColumnName("errors").IsRequired();
        builder.Property(x => x.LatencySumMs).HasColumnName("latency_sum_ms").IsRequired();
        builder.Property(x => x.LatLe50).HasColumnName("lat_le_50").IsRequired();
        builder.Property(x => x.LatLe200).HasColumnName("lat_le_200").IsRequired();
        builder.Property(x => x.LatLe500).HasColumnName("lat_le_500").IsRequired();
        builder.Property(x => x.LatLe1500).HasColumnName("lat_le_1500").IsRequired();
        builder.Property(x => x.LatGt1500).HasColumnName("lat_gt_1500").IsRequired();

        builder.Property(x => x.Instance)
            .HasColumnName("instance")
            .HasMaxLength(RequestMetricBucket.InstanceMaxLength)
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.HasIndex(x => new { x.MinuteUtc, x.Route, x.StatusClass, x.Instance })
            .IsUnique()
            .HasDatabaseName("ux_request_metric_buckets_minute_route_status_instance");

        builder.HasIndex(x => x.MinuteUtc)
            .HasDatabaseName("ix_request_metric_buckets_minute");
    }
}
