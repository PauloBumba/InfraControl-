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
            "InfraControl.Application.Common.Audit.Queries.SearchAuditEntriesQuery"
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
    public void Controllers_Should_Return_ApiResponse_Envelope()
    {
        // Seleciona por reflexão os controllers concretos (exclui o BaseController,
        // que é apenas base de transporte e não produz respostas).
        var controllerNames = ApiAssembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(type))
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        controllerNames.Should().NotBeEmpty("deve haver controllers concretos no assembly da Api");

        var offenders = controllerNames
            .Where(name => !Types
                .InAssembly(ApiAssembly)
                .That()
                .HaveName(name)
                .Should()
                .HaveDependencyOn("InfraControl.BuildingBlocks.Application.Response")
                .GetResult()
                .IsSuccessful)
            .ToArray();

        offenders.Should().BeEmpty(
            "todo controller concreto deve responder com o envelope ApiResponse do BuildingBlocks; " +
            $"fora do padrão: {string.Join(", ", offenders)}");
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
