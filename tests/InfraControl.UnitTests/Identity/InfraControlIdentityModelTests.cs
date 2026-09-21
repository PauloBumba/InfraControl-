using FluentAssertions;
using InfraControl.Infrastructure.Common.Persistence;
using InfraControl.Infrastructure.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Identity;

public sealed class InfraControlIdentityModelTests
{
    [Fact]
    public void AppDbContext_ShouldExposeIdentityUserInAspNetUsersTable()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        var entity = context.Model.FindEntityType(typeof(InfraControlUser));

        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("AspNetUsers");
    }
}
