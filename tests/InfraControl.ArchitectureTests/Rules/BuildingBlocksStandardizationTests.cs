using System.Runtime.CompilerServices;
using FluentAssertions;
using InfraControl.ArchitectureTests.Common;
using InfraControl.BuildingBlocks.Application;
using MediatR;
using NetArchTest.Rules;

namespace InfraControl.ArchitectureTests.Rules;

/// <summary>
/// Regras de padronização do shared kernel InfraControl.BuildingBlocks (F0-T022):
/// nenhum slice CQRS pode usar IRequest/IAuthorizedRequest "cru", os casos
/// administrativos declaram permissões no pipeline MediatR e os controllers
/// respondem com o envelope ApiResponse.
/// </summary>
public class BuildingBlocksStandardizationTests : BaseArchitectureTest
{
    [Fact]
    public void Application_Requests_Should_Use_Commands_Or_Queries_From_BuildingBlocks()
    {
        var rawRequests = ApplicationAssembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && !type.IsGenericTypeDefinition)
            .Where(IsMediatRRequest)
            .Where(type => !IsBuildingBlocksRequest(type))
            .Select(type => type.FullName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        rawRequests.Should().BeEmpty(
            "todo request do Application deve implementar ICommand/IQuery do BuildingBlocks, nunca IRequest cru");
    }

    [Fact]
    public void Admin_Slices_Should_Declare_Required_Permissions_Via_IAuthorizedRequest()
    {
        var adminSlices = new[]
        {
            "InfraControl.Application.Identity.Invitations.Commands.CreateInvitationCommand",
            "InfraControl.Application.Identity.Invitations.Commands.CancelInvitationCommand",
            "InfraControl.Application.Identity.Roles.Commands.AssignUserRoleCommand",
            "InfraControl.Application.Identity.Roles.Commands.RemoveUserRoleCommand",
            "InfraControl.Application.Common.Audit.Queries.SearchAuditEntriesQuery",
            "InfraControl.Application.Inventory.Locations.Commands.CreateLocationCommand",
            "InfraControl.Application.Inventory.Locations.Commands.UpdateLocationCommand",
            "InfraControl.Application.Inventory.Locations.Commands.DeleteLocationCommand",
            "InfraControl.Application.Inventory.Locations.Queries.SearchLocationsQuery",
            "InfraControl.Application.Inventory.Locations.Queries.GetLocationQuery",
            "InfraControl.Application.Inventory.Racks.Commands.CreateRackCommand",
            "InfraControl.Application.Inventory.Racks.Commands.UpdateRackCommand",
            "InfraControl.Application.Inventory.Racks.Commands.DeleteRackCommand",
            "InfraControl.Application.Inventory.Racks.Queries.SearchRacksQuery",
            "InfraControl.Application.Inventory.Racks.Queries.GetRackQuery",
            "InfraControl.Application.Inventory.Devices.Commands.CreateDeviceCommand",
            "InfraControl.Application.Inventory.Devices.Commands.UpdateDeviceCommand",
            "InfraControl.Application.Inventory.Devices.Commands.DeleteDeviceCommand",
            "InfraControl.Application.Inventory.Devices.Commands.UpdateDeviceProfileCommand",
            "InfraControl.Application.Inventory.Devices.Queries.SearchDevicesQuery",
            "InfraControl.Application.Inventory.Devices.Queries.GetDeviceQuery",
            "InfraControl.Application.Inventory.NetworkInterfaces.Commands.CreateNetworkInterfaceCommand",
            "InfraControl.Application.Inventory.NetworkInterfaces.Commands.UpdateNetworkInterfaceCommand",
            "InfraControl.Application.Inventory.NetworkInterfaces.Commands.DeleteNetworkInterfaceCommand",
            "InfraControl.Application.Inventory.NetworkInterfaces.Queries.SearchNetworkInterfacesQuery",
            "InfraControl.Application.Inventory.NetworkInterfaces.Queries.GetNetworkInterfaceQuery",
            "InfraControl.Application.Monitoring.Ping.PingDeviceQuery",
            "InfraControl.Application.Monitoring.Ping.GetPingStatusQuery",
            "InfraControl.Application.Monitoring.PrinterSupplies.GetPrinterSuppliesQuery",
            "InfraControl.Application.Monitoring.Checks.GetDeviceChecksQuery",
            "InfraControl.Application.Inventory.MacAddresses.Commands.CreateMacAddressCommand",
            "InfraControl.Application.Inventory.MacAddresses.Commands.UpdateMacAddressCommand",
            "InfraControl.Application.Inventory.MacAddresses.Commands.DeleteMacAddressCommand",
            "InfraControl.Application.Inventory.MacAddresses.Queries.SearchMacAddressesQuery",
            "InfraControl.Application.Inventory.MacAddresses.Queries.GetMacAddressQuery",
            "InfraControl.Application.Inventory.IpAddresses.Commands.CreateIpAddressCommand",
            "InfraControl.Application.Inventory.IpAddresses.Commands.UpdateIpAddressCommand",
            "InfraControl.Application.Inventory.IpAddresses.Commands.DeleteIpAddressCommand",
            "InfraControl.Application.Inventory.IpAddresses.Queries.SearchIpAddressesQuery",
            "InfraControl.Application.Inventory.IpAddresses.Queries.GetIpAddressQuery",
            "InfraControl.Application.Inventory.GlobalSearch.Queries.GlobalSearchQuery",
            "InfraControl.Application.Network.Switches.Queries.SearchSwitchesQuery",
            "InfraControl.Application.Network.Switches.Queries.GetSwitchQuery",
            "InfraControl.Application.Network.SwitchPorts.Commands.CreateSwitchPortCommand",
            "InfraControl.Application.Network.SwitchPorts.Commands.UpdateSwitchPortCommand",
            "InfraControl.Application.Network.SwitchPorts.Commands.DeleteSwitchPortCommand",
            "InfraControl.Application.Network.SwitchPorts.Queries.SearchSwitchPortsQuery",
            "InfraControl.Application.Network.SwitchPorts.Queries.GetSwitchPortQuery",
            "InfraControl.Application.Network.Vlans.Commands.CreateVlanCommand",
            "InfraControl.Application.Network.Vlans.Commands.UpdateVlanCommand",
            "InfraControl.Application.Network.Vlans.Commands.DeleteVlanCommand",
            "InfraControl.Application.Network.Vlans.Queries.SearchVlansQuery",
            "InfraControl.Application.Network.Vlans.Queries.GetVlanQuery",
            "InfraControl.Application.Network.SwitchPortVlans.Commands.AssignVlanToSwitchPortCommand",
            "InfraControl.Application.Network.SwitchPortVlans.Commands.UnassignVlanFromSwitchPortCommand",
            "InfraControl.Application.Network.SwitchPortVlans.Queries.GetSwitchPortVlansQuery",
            "InfraControl.Application.Network.SwitchPortVlans.Queries.GetVlanSwitchPortsQuery"
        };

        foreach (var typeName in adminSlices)
        {
            var type = ApplicationAssembly.GetType(typeName, throwOnError: false);
            type.Should().NotBeNull($"o slice {typeName} deve existir no assembly Application");
            type!.Should().BeAssignableTo<IAuthorizedRequest>(
                $"o slice {typeName} deve declarar permissões exigidas no pipeline MediatR");

            var instance = (IAuthorizedRequest)RuntimeHelpers.GetUninitializedObject(type!);
            instance.RequiredPermissions.Should().NotBeEmpty(
                $"o slice {typeName} deve exigir ao menos uma permissão");
        }
    }

    [Fact]
    public void Exception_Handler_Should_Return_ApiResponse_Envelope()
    {
        var handlerType = ApiAssembly.GetType("InfraControl.Api.Middleware.GlobalExceptionHandler");
        handlerType.Should().NotBeNull(
            "o tratador global de exceções deve existir na Api (é o ponto único de erro)");

        var result = Types
            .InAssembly(ApiAssembly)
            .That()
            .HaveName("GlobalExceptionHandler")
            .Should()
            .HaveDependencyOn("InfraControl.BuildingBlocks.Application.Response")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "o tratador global de exceções é o ponto único de erro e deve usar o envelope ApiResponse");
    }

    private static bool IsMediatRRequest(Type type)
        => typeof(IRequest).IsAssignableFrom(type)
           || type.GetInterfaces().Any(candidate =>
               candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IRequest<>));

    private static bool IsBuildingBlocksRequest(Type type)
        => typeof(ICommand).IsAssignableFrom(type)
           || type.GetInterfaces().Any(candidate =>
               candidate.IsGenericType &&
               (candidate.GetGenericTypeDefinition() == typeof(ICommand<>)
                || candidate.GetGenericTypeDefinition() == typeof(IQuery<>)));
}
