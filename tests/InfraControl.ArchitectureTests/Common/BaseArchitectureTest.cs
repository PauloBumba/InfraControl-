using System.Reflection;
using FluentAssertions;

namespace InfraControl.ArchitectureTests.Common;

public abstract class BaseArchitectureTest
{
    protected static Assembly DomainAssembly => typeof(InfraControl.Domain.Common.Entities.Entity).Assembly;
    protected static Assembly ApplicationAssembly => typeof(InfraControl.Application.Common.Interfaces.IUnitOfWork).Assembly;
    protected static Assembly InfrastructureAssembly => typeof(InfraControl.Infrastructure.Persistence.AppDbContext).Assembly;
    protected static Assembly ContractsAssembly => typeof(InfraControl.Contracts.Common.Events.IEvent).Assembly;
}
