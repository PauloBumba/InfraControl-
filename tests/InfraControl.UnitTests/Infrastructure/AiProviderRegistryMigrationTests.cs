using System.Reflection;
using InfraControl.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace InfraControl.UnitTests.Infrastructure;

public sealed class AiProviderRegistryMigrationTests
{
    [Fact]
    public void Up_Should_Create_Only_Ai_Provider_Tables()
    {
        var operations = GetOperations("Up");

        operations.OfType<CreateTableOperation>()
            .Select(operation => operation.Name)
            .Should()
            .Equal("ai_providers", "ai_provider_capabilities");
        operations.OfType<DropTableOperation>().Should().BeEmpty();
    }

    [Fact]
    public void Up_Should_Create_Unique_Indexes_And_Models_TextArray()
    {
        var operations = GetOperations("Up");

        var providers = operations.OfType<CreateTableOperation>()
            .Single(operation => operation.Name == "ai_providers");
        providers.Columns.Single(column => column.Name == "models")
            .ClrType.Should().Be(typeof(string[]));

        var uniqueIndexes = operations.OfType<CreateIndexOperation>()
            .Where(operation => operation.IsUnique)
            .Select(operation => $"{operation.Table}:{string.Join(",", operation.Columns)}")
            .ToList();
        uniqueIndexes.Should().BeEquivalentTo(
        [
            "ai_providers:name",
            "ai_provider_capabilities:capability"
        ]);
    }

    [Fact]
    public void Down_Should_Drop_Only_Ai_Provider_Tables()
    {
        var operations = GetOperations("Down");

        operations.OfType<DropTableOperation>()
            .Select(operation => operation.Name)
            .Should()
            .BeEquivalentTo("ai_providers", "ai_provider_capabilities");
        operations.OfType<CreateTableOperation>().Should().BeEmpty();
    }

    private static IReadOnlyList<MigrationOperation> GetOperations(string direction)
    {
        var builder = new MigrationBuilder("Npgsql");
        var method = typeof(AiProviderRegistry).GetMethod(
            direction,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Migration sem {direction}().");

        method.Invoke(new AiProviderRegistry(), [builder]);
        return builder.Operations;
    }
}
