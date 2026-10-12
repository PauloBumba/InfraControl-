using System.Reflection;
using InfraControl.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace InfraControl.UnitTests.Infrastructure;

public sealed class AddUniFiIntegrationSettingMigrationTests
{
    [Fact]
    public void Up_Should_Create_Only_UniFi_Settings_Table()
    {
        var operations = GetOperations("Up");

        operations.OfType<CreateTableOperation>()
            .Select(operation => operation.Name)
            .Should()
            .Equal("unifi_integration_settings");
        operations.OfType<DropTableOperation>().Should().BeEmpty();
    }

    [Fact]
    public void Down_Should_Drop_Only_UniFi_Settings_Table()
    {
        var operations = GetOperations("Down");

        operations.OfType<DropTableOperation>()
            .Select(operation => operation.Name)
            .Should()
            .Equal("unifi_integration_settings");
        operations.OfType<CreateTableOperation>().Should().BeEmpty();
    }

    private static IReadOnlyList<MigrationOperation> GetOperations(string direction)
    {
        var builder = new MigrationBuilder("Npgsql");
        var method = typeof(AddUniFiIntegrationSetting).GetMethod(
            direction,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Migration sem {direction}().");

        method.Invoke(new AddUniFiIntegrationSetting(), [builder]);
        return builder.Operations;
    }
}
