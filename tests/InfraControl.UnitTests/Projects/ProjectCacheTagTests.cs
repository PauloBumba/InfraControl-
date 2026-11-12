using FluentAssertions;
using InfraControl.Application.Projects.Commands;
using InfraControl.Application.Projects.Queries;
using InfraControl.BuildingBlocks.Application;

namespace InfraControl.UnitTests.Projects;

/// <summary>
/// Cache de projetos: a lista e o detalhe cacheiam por tag "projects"
/// (TTL 5 min do BaseQuery) e toda mutation que muda o que a lista
/// exibe invalida a MESMA tag — sem isso um projeto criado só aparece
/// após o TTL (ou nunca, se o front não refetchar).
/// </summary>
public class ProjectCacheTagTests
{
    [Fact]
    public void ProjectQueries_ShouldTagProjectsCache()
    {
        new SearchProjectsQuery().CacheTags.Should().Contain("projects");
        new GetProjectQuery(Guid.NewGuid()).CacheTags.Should().Contain("projects");
    }

    [Fact]
    public void ListAffectingCommands_ShouldInvalidateProjectsTag()
    {
        var commands = new ICacheInvalidatingCommand[]
        {
            new CreateProjectCommand(),
            new UpdateProjectCommand(Guid.NewGuid()),
            new DeleteProjectCommand(Guid.NewGuid()),
            new TransitionProjectStatusCommand(Guid.NewGuid(), "Planned"),
            new SubmitProjectForApprovalCommand(Guid.NewGuid()),
            new ApproveProjectCommand(Guid.NewGuid()),
            new SubmitProjectConclusionCommand(Guid.NewGuid()),
            new ApproveProjectConclusionCommand(Guid.NewGuid()),
            new ReturnProjectConclusionCommand(Guid.NewGuid()),
            new AddProjectMemberCommand(Guid.NewGuid(), Guid.NewGuid(), "Member"),
            new RemoveProjectMemberCommand(Guid.NewGuid(), Guid.NewGuid()),
        };

        commands.Should().OnlyContain(
            command => command.CacheKeysToInvalidate.Contains("projects"),
            because: "toda mutation de projeto deve invalidar a tag da lista/detalhe");
    }
}
