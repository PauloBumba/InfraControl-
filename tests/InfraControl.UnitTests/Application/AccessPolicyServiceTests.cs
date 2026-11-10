using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Common.Security;
using InfraControl.Application.Common.Security.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Security;

/// <summary>
/// ADR-066: matriz role×módulo. Tabela vazia = defaults do ADR-016;
/// overrides concedem/revogam níveis por módulo; Admin nunca é rebaixado
/// no módulo Administração.
/// </summary>
public sealed class AccessPolicyServiceTests
{
    private sealed class FakeRepo : IAccessPolicyRepository
    {
        public readonly List<AccessPolicyRule> Rules = [];

        public Task<IReadOnlyList<AccessPolicyRule>> ListAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AccessPolicyRule>>(Rules);

        public Task<AccessPolicyRule?> FindAsync(
            string roleKey, string moduleKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(Rules.FirstOrDefault(r =>
                r.RoleKey.Equals(roleKey, StringComparison.OrdinalIgnoreCase) &&
                r.ModuleKey.Equals(moduleKey, StringComparison.OrdinalIgnoreCase)));

        public Task AddAsync(AccessPolicyRule entity, CancellationToken cancellationToken = default)
        {
            Rules.Add(entity);
            return Task.CompletedTask;
        }

        public void Remove(AccessPolicyRule entity) => Rules.Remove(entity);

        public Task<AccessPolicyRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Rules.FirstOrDefault(r => r.Id == id));

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeCache : ICacheService
    {
        private readonly Dictionary<string, object?> _store = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_store.TryGetValue(key, out var v) ? (T?)v : default);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            _store[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _store.Remove(key);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_store.ContainsKey(key));

        public Task<bool> TryAcquireAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task TagAsync(string tag, string key, TimeSpan? duration, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task InvalidateTagAsync(string tag, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeRoles : IAccessRoleRepository
    {
        public readonly List<AccessRole> Items = [];
        public Task<IReadOnlyList<AccessRole>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AccessRole>>(Items);
        public Task<AccessRole?> FindByKeyAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(r => r.Key == key));
        public Task<AccessRole?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(r => r.Id == id));
        public Task AddAsync(AccessRole entity, CancellationToken cancellationToken = default)
        { Items.Add(entity); return Task.CompletedTask; }
        public void Remove(AccessRole entity) => Items.Remove(entity);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static AccessPolicyService CreateService(FakeRepo repo, FakeCache? cache = null, FakeRoles? roles = null) =>
        new(repo, roles ?? new FakeRoles(), cache ?? new FakeCache(), NullLogger<AccessPolicyService>.Instance);

    [Fact]
    public async Task Empty_Table_Should_Use_ADR016_Defaults()
    {
        var service = CreateService(new FakeRepo());

        (await service.GetEffectiveLevelAsync("Viewer", "projects"))
            .Should().Be(AccessLevel.Read);
        (await service.GetEffectiveLevelAsync("Operator", "projects"))
            .Should().Be(AccessLevel.Operate);
        (await service.GetEffectiveLevelAsync("Admin", "projects"))
            .Should().Be(AccessLevel.Administer);
    }

    [Fact]
    public async Task Override_Should_Deny_Viewer_Read_On_Module()
    {
        var repo = new FakeRepo();
        await repo.AddAsync(AccessPolicyRule.Create(
            UserRole.Viewer, "projects", AccessLevel.None));
        var service = CreateService(repo);

        (await service.HasAccessAsync(["Viewer"], AccessLevel.Read, "projects"))
            .Should().BeFalse();
        (await service.HasAccessAsync(["Operator"], AccessLevel.Read, "projects"))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Override_Should_Elevate_Viewer_To_Administer()
    {
        var repo = new FakeRepo();
        await repo.AddAsync(AccessPolicyRule.Create(
            UserRole.Viewer, "knowledge", AccessLevel.Administer));
        var service = CreateService(repo);

        (await service.HasAccessAsync(["Viewer"], AccessLevel.Administer, "knowledge"))
            .Should().BeTrue();
        // Outro módulo continua no default.
        (await service.HasAccessAsync(["Viewer"], AccessLevel.Operate, "projects"))
            .Should().BeFalse();
    }

    [Fact]
    public async Task ReplaceGrants_Should_Persist_Overrides_And_Clear_On_Null()
    {
        var repo = new FakeRepo();
        var service = CreateService(repo);

        await service.ReplaceGrantsAsync(
            [new AccessPolicyGrantInput("Viewer", "projects", 2)], CancellationToken.None);
        repo.Rules.Should().ContainSingle();
        repo.Rules[0].Level.Should().Be(AccessLevel.Operate);

        // level null = remove override → volta ao default.
        await service.ReplaceGrantsAsync(
            [new AccessPolicyGrantInput("Viewer", "projects", null)], CancellationToken.None);
        repo.Rules.Should().BeEmpty();
        (await service.GetEffectiveLevelAsync("Viewer", "projects"))
            .Should().Be(AccessLevel.Read);
    }

    [Fact]
    public async Task ReplaceGrants_Should_Reject_Admin_Demotion_On_Admin_Module()
    {
        var service = CreateService(new FakeRepo());

        var act = () => service.ReplaceGrantsAsync(
            [new AccessPolicyGrantInput("Admin", "admin", 1)], CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Admin*");
    }

    [Fact]
    public async Task ReplaceGrants_Should_Reject_Unknown_Module()
    {
        var service = CreateService(new FakeRepo());

        var act = () => service.ReplaceGrantsAsync(
            [new AccessPolicyGrantInput("Viewer", "modulo-inexistente", 1)],
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Matrix_Should_List_All_Catalog_Modules_With_Defaults()
    {
        var service = CreateService(new FakeRepo());
        var matrix = await service.GetMatrixAsync();

        matrix.Modules.Should().NotBeEmpty();
        matrix.Modules.Should().OnlyContain(m => m.Roles.Count == 3);
        var projects = matrix.Modules.Single(m => m.Key == "projects");
        projects.Roles.Single(r => r.Role == "Admin").Level
            .Should().Be((int)AccessLevel.Administer);
    }
}
