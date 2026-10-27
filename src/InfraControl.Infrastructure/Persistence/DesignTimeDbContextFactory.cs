using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InfraControl.Infrastructure.Persistence;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        optionsBuilder.UseNpgsql(
            Environment.GetEnvironmentVariable("INFRA_DESIGN_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=infracontrol;Username=infracontrol;Password=infracontrol_dev_2026");

        return new AppDbContext(optionsBuilder.Options);
    }
}
