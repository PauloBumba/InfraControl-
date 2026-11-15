using FluentAssertions;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.UnitTests.Infrastructure;

public sealed class SchemaCatalogTests
{
    [Fact]
    public async Task ListEntities_ShouldExposeTablesPropertiesAndForeignKeys()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(connection));
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.EnsureCreatedAsync();

        var catalog = new SchemaCatalog(context);
        var entities = catalog.ListEntities();

        entities.Should().NotBeEmpty();
        entities.Should().OnlyContain(e =>
            !string.IsNullOrWhiteSpace(e.TableName) && e.Properties.Count > 0);

        // Toda entidade tem PK marcada; FKs apontam para Entidade.Propriedade.
        entities.Should().OnlyContain(e =>
            e.Properties.Any(p => p.IsPrimaryKey));
        var withFk = entities.SelectMany(e => e.Properties)
            .Where(p => p.ForeignKeyTarget is not null).ToArray();
        withFk.Should().NotBeEmpty("o modelo tem relacionamentos");
        withFk.Should().OnlyContain(p =>
            p.ForeignKeyTarget!.Contains('.'));
    }
}
