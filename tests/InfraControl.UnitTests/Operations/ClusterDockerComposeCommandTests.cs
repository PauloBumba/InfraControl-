using FluentAssertions;
using InfraControl.Application.Operations.Cluster;
using InfraControl.Application.Operations.Cluster.Ports;

namespace InfraControl.UnitTests.Operations;

/// <summary>
/// docs/65 Fase 3 — escala Compose via compose-agent, remoção de container
/// parado (dupla confirmação + motivo) e limites de recursos em runtime.
/// </summary>
public sealed class ClusterDockerComposeCommandTests
{
    // ---------- ScaleComposeServiceCommand ----------

    [Fact]
    public async Task Scale_RejectsInvalidServiceName()
    {
        var handler = new ScaleComposeServiceCommandHandler(new FakeComposeAgent());
        var act = () => handler.Handle(
            new ScaleComposeServiceCommand("api; rm -rf", 2), CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(17)]
    public async Task Scale_RejectsReplicasOutOfRange(int replicas)
    {
        var handler = new ScaleComposeServiceCommandHandler(new FakeComposeAgent());
        var act = () => handler.Handle(
            new ScaleComposeServiceCommand("api", replicas), CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Scale_AgentUnavailable_Throws()
    {
        var handler = new ScaleComposeServiceCommandHandler(new FakeComposeAgent(available: false));
        var act = () => handler.Handle(
            new ScaleComposeServiceCommand("api", 2), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*compose-agent*");
    }

    [Fact]
    public async Task Scale_ComposeRefuses_ReturnsFailure()
    {
        var agent = new FakeComposeAgent(scaleResult: new ComposeScaleResult("api", 2, false, "no such service"));
        var handler = new ScaleComposeServiceCommandHandler(agent);
        var act = () => handler.Handle(
            new ScaleComposeServiceCommand("api", 2), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no such service*");
    }

    [Fact]
    public async Task Scale_Success_ReturnsResult()
    {
        var agent = new FakeComposeAgent(scaleResult: new ComposeScaleResult("api", 3, true, "ok"));
        var handler = new ScaleComposeServiceCommandHandler(agent);
        var result = await handler.Handle(
            new ScaleComposeServiceCommand("api", 3), CancellationToken.None);
        result.Success.Should().BeTrue();
        result.Replicas.Should().Be(3);
        agent.LastScaleService.Should().Be("api");
        agent.LastScaleReplicas.Should().Be(3);
    }

    // ---------- RemoveDockerContainerCommand ----------

    [Fact]
    public async Task Remove_ConfirmMismatch_Throws()
    {
        var handler = new RemoveDockerContainerCommandHandler(FakeDocker.WithContainer("api-1", "exited"));
        var act = () => handler.Handle(
            new RemoveDockerContainerCommand("api-1", "motivo válido", "outro-nome"),
            CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Remove_ReasonTooShort_Throws()
    {
        var handler = new RemoveDockerContainerCommandHandler(FakeDocker.WithContainer("api-1", "exited"));
        var act = () => handler.Handle(
            new RemoveDockerContainerCommand("api-1", "abc", "api-1"),
            CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Remove_ContainerNotInProject_Throws()
    {
        var docker = FakeDocker.WithContainer("api-1", "exited");
        var handler = new RemoveDockerContainerCommandHandler(docker);
        var act = () => handler.Handle(
            new RemoveDockerContainerCommand("container-alheio", "motivo válido", "container-alheio"),
            CancellationToken.None);
        await act.Should().ThrowAsync<KeyNotFoundException>();
        docker.RemoveCalls.Should().Be(0);
    }

    [Fact]
    public async Task Remove_RunningContainer_Throws()
    {
        var docker = FakeDocker.WithContainer("api-1", "running");
        var handler = new RemoveDockerContainerCommandHandler(docker);
        var act = () => handler.Handle(
            new RemoveDockerContainerCommand("api-1", "motivo válido", "api-1"),
            CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*execução*");
        docker.RemoveCalls.Should().Be(0);
    }

    [Fact]
    public async Task Remove_StoppedContainer_Removes()
    {
        var docker = FakeDocker.WithContainer("api-1", "exited");
        var handler = new RemoveDockerContainerCommandHandler(docker);
        await handler.Handle(
            new RemoveDockerContainerCommand("api-1", "motivo válido", "api-1"),
            CancellationToken.None);
        docker.RemoveCalls.Should().Be(1);
        docker.LastRemoved.Should().Be("api-1");
    }

    // ---------- UpdateDockerResourceLimitsCommand ----------

    [Fact]
    public async Task Limits_NoField_Throws()
    {
        var handler = new UpdateDockerResourceLimitsCommandHandler(FakeDocker.WithContainer("postgres", "running"));
        var act = () => handler.Handle(
            new UpdateDockerResourceLimitsCommand("postgres", null, null), CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Limits_MemoryTooSmall_Throws()
    {
        var handler = new UpdateDockerResourceLimitsCommandHandler(FakeDocker.WithContainer("postgres", "running"));
        var act = () => handler.Handle(
            new UpdateDockerResourceLimitsCommand("postgres", 1024, null), CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Limits_AppliesAndReturns()
    {
        var docker = FakeDocker.WithContainer("postgres", "running");
        var handler = new UpdateDockerResourceLimitsCommandHandler(docker);
        var result = await handler.Handle(
            new UpdateDockerResourceLimitsCommand("postgres", 512L * 1024 * 1024, 2.0),
            CancellationToken.None);
        result.MemoryBytes.Should().Be(512L * 1024 * 1024);
        result.CpuCores.Should().Be(2.0);
        docker.LastMemoryBytes.Should().Be(512L * 1024 * 1024);
        docker.LastCpuCores.Should().Be(2.0);
    }

    // ---------- Fakes ----------

    private sealed class FakeComposeAgent(
        bool available = true,
        ComposeScaleResult? scaleResult = null) : IComposeAgent
    {
        public string? LastScaleService { get; private set; }
        public int? LastScaleReplicas { get; private set; }

        public Task<IReadOnlyList<ComposeServiceDto>?> ListServicesAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ComposeServiceDto>?>([]);

        public Task<ComposeScaleResult?> ScaleAsync(
            string service, int replicas, CancellationToken cancellationToken = default)
        {
            if (!available) return Task.FromResult<ComposeScaleResult?>(null);
            LastScaleService = service;
            LastScaleReplicas = replicas;
            return Task.FromResult<ComposeScaleResult?>(
                scaleResult ?? new ComposeScaleResult(service, replicas, true, "ok"));
        }
    }

    private sealed class FakeDocker : IDockerControl
    {
        private readonly List<DockerContainerDto> _containers = [];

        public int RemoveCalls { get; private set; }
        public string? LastRemoved { get; private set; }
        public long? LastMemoryBytes { get; private set; }
        public double? LastCpuCores { get; private set; }

        public static FakeDocker WithContainer(string name, string state)
        {
            var fake = new FakeDocker();
            fake._containers.Add(new DockerContainerDto("id-" + name, name, "img", state, "status", null, null));
            return fake;
        }

        public Task<IReadOnlyList<DockerContainerDto>?> ListContainersAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DockerContainerDto>?>(_containers);

        public Task<DockerContainerStats?> GetContainerStatsAsync(
            string containerIdOrName, CancellationToken cancellationToken = default)
            => Task.FromResult<DockerContainerStats?>(null);

        public Task<DockerContainerInspectDto?> GetContainerInspectAsync(
            string containerIdOrName, CancellationToken cancellationToken = default)
            => Task.FromResult<DockerContainerInspectDto?>(null);

        public Task<DockerTopologyDto?> GetTopologyAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<DockerTopologyDto?>(null);

        public Task<string?> GetContainerLogsAsync(
            string containerIdOrName, int tailLines, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<bool> ControlAsync(
            string containerIdOrName, DockerContainerAction action,
            CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<DockerContainerUpdateStatus> RemoveContainerAsync(
            string containerIdOrName, CancellationToken cancellationToken = default)
        {
            RemoveCalls++;
            LastRemoved = containerIdOrName;
            return Task.FromResult(DockerContainerUpdateStatus.Done);
        }

        public Task<DockerContainerUpdateStatus> UpdateResourceLimitsAsync(
            string containerIdOrName, long? memoryBytes, double? cpuCores,
            CancellationToken cancellationToken = default)
        {
            LastMemoryBytes = memoryBytes;
            LastCpuCores = cpuCores;
            return Task.FromResult(DockerContainerUpdateStatus.Done);
        }
    }
}
