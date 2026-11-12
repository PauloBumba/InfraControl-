using FluentAssertions;
using InfraControl.Application.Operations.Cluster;
using InfraControl.Application.Operations.Cluster.Ports;
using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Operations;

/// <summary>
/// Tela de status (F50): listagem de containers em nível de leitura
/// (qualquer role autenticada) — o admin continua com o endpoint
/// enriquecido do ClusterController.
/// </summary>
public sealed class StatusContainersQueryTests
{
    [Fact]
    public async Task Query_ReturnsContainers_WhenDockerAvailable()
    {
        var docker = new FakeDockerControl(
        [
            new DockerContainerDto(
                "abc123", "infracontrol-api-1", "infracontrol-api:dev",
                "running", "Up 2 hours", null, null),
            new DockerContainerDto(
                "def456", "postgres-1", "pgvector/pgvector:pg17",
                "running", "Up 3 days", null, null),
        ]);
        var handler = new GetDockerStatusQueryHandler(docker);

        var result = await handler.Handle(
            new GetDockerStatusQuery(), CancellationToken.None);

        result.Available.Should().BeTrue();
        result.Containers.Should().HaveCount(2);
        result.Containers[0].Name.Should().Be("infracontrol-api-1");
        result.Containers[1].Image.Should().Be("pgvector/pgvector:pg17");
    }

    [Fact]
    public async Task Query_UnavailableDocker_ReturnsAvailableFalse()
    {
        var handler = new GetDockerStatusQueryHandler(new FakeDockerControl(null));

        var result = await handler.Handle(
            new GetDockerStatusQuery(), CancellationToken.None);

        result.Available.Should().BeFalse();
        result.Containers.Should().BeEmpty();
    }

    [Fact]
    public void Query_RequiresReadRole()
    {
        var query = new GetDockerStatusQuery();

        query.RequiredRoles.Should().Equal(RoleAccess.Read);
    }

    private sealed class FakeDockerControl(
        IReadOnlyList<DockerContainerDto>? containers) : IDockerControl
    {
        public Task<IReadOnlyList<DockerContainerDto>?> ListContainersAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(containers);

        public Task<DockerContainerStats?> GetContainerStatsAsync(
            string containerIdOrName,
            CancellationToken cancellationToken = default)
            => Task.FromResult<DockerContainerStats?>(null);

        public Task<DockerContainerInspectDto?> GetContainerInspectAsync(
            string containerIdOrName,
            CancellationToken cancellationToken = default)
            => Task.FromResult<DockerContainerInspectDto?>(null);

        public Task<DockerTopologyDto?> GetTopologyAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<DockerTopologyDto?>(null);

        public Task<string?> GetContainerLogsAsync(
            string containerIdOrName,
            int tailLines,
            CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<bool> ControlAsync(
            string containerIdOrName,
            DockerContainerAction action,
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<DockerContainerUpdateStatus> RemoveContainerAsync(
            string containerIdOrName,
            CancellationToken cancellationToken = default)
            => Task.FromResult(DockerContainerUpdateStatus.NotFound);

        public Task<DockerContainerUpdateStatus> UpdateResourceLimitsAsync(
            string containerIdOrName,
            long? memoryBytes,
            double? cpuCores,
            CancellationToken cancellationToken = default)
            => Task.FromResult(DockerContainerUpdateStatus.NotFound);
    }
}
