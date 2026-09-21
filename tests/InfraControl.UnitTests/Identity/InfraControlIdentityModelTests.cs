using FluentAssertions;
using InfraControl.Infrastructure.Common.Persistence;
using InfraControl.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Identity;

public sealed class InfraControlIdentityModelTests
{
    [Fact]
    public void AppDbContext_ShouldExposeIdentityUserInAspNetUsersTable()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"identity-model-{Guid.NewGuid()}")
            .Options;

        using var context = new AppDbContext(options);
        var entity = context.Model.FindEntityType(typeof(InfraControlUser));

        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("AspNetUsers");
    }
}
