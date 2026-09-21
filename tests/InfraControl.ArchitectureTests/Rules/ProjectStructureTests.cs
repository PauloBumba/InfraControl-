using FluentAssertions;
using InfraControl.ArchitectureTests.Common;
using NetArchTest.Rules;

namespace InfraControl.ArchitectureTests.Rules;

public class ProjectStructureTests : BaseArchitectureTest
{
    [Fact]
    public void Domain_Should_Not_Depend_On_Infrastructure()
    {
        var result = Types
            .InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("InfraControl.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Domain não deve depender de Infrastructure");
    }

    [Fact]
    public void Domain_Should_Not_Depend_On_Application()
    {
        var result = Types
            .InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("InfraControl.Application")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Domain não deve depender de Application");
    }

    [Fact]
    public void Application_Should_Not_Depend_On_Infrastructure()
    {
        var result = Types
            .InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn("InfraControl.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Application não deve depender de Infrastructure");
    }

    [Fact]
    public void Contracts_Should_Not_Depend_On_Infrastructure()
    {
        var result = Types
            .InAssembly(ContractsAssembly)
            .ShouldNot()
            .HaveDependencyOn("InfraControl.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Contracts não deve depender de Infrastructure");
    }

    [Fact]
    public void Contracts_Should_Not_Depend_On_Application()
    {
        var result = Types
            .InAssembly(ContractsAssembly)
            .ShouldNot()
            .HaveDependencyOn("InfraControl.Application")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Contracts não deve depender de Application");
    }
}
