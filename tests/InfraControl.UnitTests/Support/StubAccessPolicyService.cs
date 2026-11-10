using InfraControl.Application.Common.Security;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.UnitTests.Support;

/// <summary>
/// Stub do IAccessPolicyService para testes que não exercitam a matriz
/// ADR-066: requests de teste vivem em namespaces que não resolvem módulo,
/// então o fallback legado é usado e o serviço nunca é consultado.
/// </summary>
public sealed class StubAccessPolicyService : IAccessPolicyService
{
    public static readonly StubAccessPolicyService Instance = new();

    public Task<AccessLevel> GetEffectiveLevelAsync(
        string roleKey, string moduleKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(AccessPolicyService.DefaultLevel(roleKey));

    public Task<bool> HasAccessAsync(
        IReadOnlyCollection<string> roleNames,
        AccessLevel required,
        string moduleKey,
        CancellationToken cancellationToken = default)
    {
        foreach (var name in roleNames)
        {
            if ((int)AccessPolicyService.DefaultLevel(name) >= (int)required)
                return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<AccessPolicyMatrixDto> GetMatrixAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new AccessPolicyMatrixDto([], []));

    public Task<AccessPolicyMatrixDto> ReplaceGrantsAsync(
        IReadOnlyList<AccessPolicyGrantInput> grants,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new AccessPolicyMatrixDto([], []));

    public Task InvalidateRolesCacheAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
