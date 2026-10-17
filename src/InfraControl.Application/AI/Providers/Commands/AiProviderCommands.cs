using InfraControl.Application.AI.Providers.Ports;
using InfraControl.Application.RemoteAccess.Credentials.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.AI.Providers.Commands;

// FAI-T011b (ADR-057): CRUD Administer do cadastro de providers.
// Nome do provider = nome do adaptador registrado em código (ADR-050 §1) —
// criar cadastro sem adaptador correspondente seria linha inútil (o router
// validaria com §44 em todo resolve). Chave write-only: payload vazio
// preserva a cifrada (molde SaveZabbixSettingsCommand).

public sealed record CreateAiProviderCommand(
    string Name,
    string BaseUrl,
    string? ApiKey,
    IReadOnlyList<string>? Models)
    : BaseCommand<AiProviderDto>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;

    public string AuditResource => "AiProvider";
}

public sealed record UpdateAiProviderCommand(
    Guid Id,
    string BaseUrl,
    string? ApiKey,
    IReadOnlyList<string>? Models)
    : BaseCommand<AiProviderDto>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;

    public string AuditResource => "AiProvider";
}

public sealed record SetAiProviderActiveCommand(Guid Id, bool Active)
    : BaseCommand<AiProviderDto>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;

    public string AuditResource => "AiProvider";
}

public sealed record UpsertAiProviderCapabilityRouteCommand(
    string Capability,
    Guid ProviderId,
    string DefaultModel,
    Guid? FallbackProviderId = null,
    string? FallbackModel = null)
    : BaseCommand<AiProviderCapabilityRouteDto>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;

    public string AuditResource => "AiProviderCapability";
}

public sealed record DeleteAiProviderCapabilityRouteCommand(string Capability)
    : BaseCommand, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;

    public string AuditResource => "AiProviderCapability";
}

/// <summary>Teste de conexão (ADR-037 §13) reaproveitando o adaptador — nunca lança por falha de rede.</summary>
public sealed record TestAiProviderConnectionCommand(Guid Id)
    : BaseCommand<AiProviderConnectionTestDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;
}

/// <summary>Listagem de modelos do provider remoto (ADR-037 §14).</summary>
public sealed record ListAiProviderModelsCommand(Guid Id)
    : BaseCommand<AiProviderModelsDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;
}

internal static class AiProviderAdminValidation
{
    internal static string CanonicalCapability(string capability)
    {
        if (!Enum.TryParse<AiCapability>(capability, ignoreCase: true, out var parsed))
            throw new ArgumentException(
                $"Capacidade inválida. Use: {string.Join(", ", Enum.GetNames<AiCapability>())}.",
                nameof(capability));

        return parsed.ToString();
    }

    internal static IAiProvider ResolveAdapter(
        string providerName, IEnumerable<IAiProvider> adapters)
        => adapters.FirstOrDefault(adapter => string.Equals(
                adapter.Name, providerName, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException(
                $"Adaptador '{providerName}' não está registrado nesta build. " +
                $"Adaptadores disponíveis: {AdapterNames(adapters)}.",
                nameof(providerName));

    internal static string AdapterNames(IEnumerable<IAiProvider> adapters)
        => string.Join(", ", adapters
            .Select(adapter => adapter.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.Ordinal));
}

public sealed class CreateAiProviderCommandHandler(
    IAiProviderRepository repository,
    IEnumerable<IAiProvider> adapters,
    ICredentialProtector protector)
    : IRequestHandler<CreateAiProviderCommand, AiProviderDto>
{
    public async Task<AiProviderDto> Handle(
        CreateAiProviderCommand request, CancellationToken cancellationToken)
    {
        var adapter = AiProviderAdminValidation.ResolveAdapter(request.Name, adapters);
        var existing = await repository.GetByNameAsync(adapter.Name, cancellationToken);
        if (existing is not null)
            throw new ArgumentException(
                $"Provider '{adapter.Name}' já está cadastrado.", nameof(request));

        var provider = AiProvider.Create(
            adapter.Name,
            request.BaseUrl,
            Protect(request.ApiKey),
            request.Models);

        await repository.AddAsync(provider, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return provider.ToDto(protector);
    }

    private string? Protect(string? apiKey)
        => string.IsNullOrWhiteSpace(apiKey) ? null : protector.Protect(apiKey.Trim());
}

public sealed class UpdateAiProviderCommandHandler(
    IAiProviderRepository repository,
    ICredentialProtector protector)
    : IRequestHandler<UpdateAiProviderCommand, AiProviderDto>
{
    public async Task<AiProviderDto> Handle(
        UpdateAiProviderCommand request, CancellationToken cancellationToken)
    {
        var provider = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Provider '{request.Id}' não encontrado.");

        provider.Update(
            provider.Name,
            request.BaseUrl,
            provider.ApiKeyEncrypted,
            request.Models,
            provider.IsActive);

        // campo vazio = preserva a chave já cifrada (write-only)
        if (!string.IsNullOrWhiteSpace(request.ApiKey))
            provider.UpdateApiKey(protector.Protect(request.ApiKey.Trim()));

        await repository.SaveChangesAsync(cancellationToken);

        return provider.ToDto(protector);
    }
}

public sealed class SetAiProviderActiveCommandHandler(IAiProviderRepository repository)
    : IRequestHandler<SetAiProviderActiveCommand, AiProviderDto>
{
    public async Task<AiProviderDto> Handle(
        SetAiProviderActiveCommand request, CancellationToken cancellationToken)
    {
        var provider = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Provider '{request.Id}' não encontrado.");

        if (request.Active)
            provider.Activate();
        else
            provider.Deactivate();

        await repository.SaveChangesAsync(cancellationToken);

        return new AiProviderDto(
            provider.Id,
            provider.Name,
            provider.BaseUrl,
            provider.IsActive,
            !string.IsNullOrEmpty(provider.ApiKeyEncrypted),
            null,
            provider.Models);
    }
}

public sealed class UpsertAiProviderCapabilityRouteCommandHandler(
    IAiProviderRepository repository)
    : IRequestHandler<UpsertAiProviderCapabilityRouteCommand, AiProviderCapabilityRouteDto>
{
    public async Task<AiProviderCapabilityRouteDto> Handle(
        UpsertAiProviderCapabilityRouteCommand request, CancellationToken cancellationToken)
    {
        var capability = AiProviderAdminValidation.CanonicalCapability(request.Capability);

        var provider = await repository.GetByIdAsync(request.ProviderId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Provider '{request.ProviderId}' não encontrado.");

        if (request.FallbackProviderId is { } fallbackId)
        {
            if (fallbackId == provider.Id)
                throw new ArgumentException(
                    "O provider de fallback deve ser diferente do principal.",
                    nameof(request));

            _ = await repository.GetByIdAsync(fallbackId, cancellationToken)
                ?? throw new KeyNotFoundException(
                    $"Provider de fallback '{fallbackId}' não encontrado.");
        }

        // UpsertCapabilityRouteAsync carrega a linha rastreada (índice
        // único em capability case-insensitive) e reusa o Id — o route
        // passado é cópia de trabalho, não precisa de tracking à parte.
        await repository.UpsertCapabilityRouteAsync(
            AiProviderCapability.Create(
                capability,
                provider.Id,
                request.DefaultModel,
                request.FallbackProviderId,
                request.FallbackModel),
            cancellationToken);

        await repository.SaveChangesAsync(cancellationToken);

        var saved = await repository.GetCapabilityRouteAsync(
            capability, cancellationToken);
        var providers = await repository.ListAsync(cancellationToken);
        var byId = providers.ToDictionary(item => item.Id);
        return AiProviderAdminMapper.ToRouteDto(capability, saved, byId);
    }
}

public sealed class DeleteAiProviderCapabilityRouteCommandHandler(
    IAiProviderRepository repository)
    : IRequestHandler<DeleteAiProviderCapabilityRouteCommand>
{
    public async Task Handle(
        DeleteAiProviderCapabilityRouteCommand request, CancellationToken cancellationToken)
    {
        var capability = AiProviderAdminValidation.CanonicalCapability(request.Capability);
        await repository.RemoveCapabilityRouteAsync(capability, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }
}

public sealed class TestAiProviderConnectionCommandHandler(
    IAiProviderRepository repository,
    IEnumerable<IAiProvider> adapters)
    : IRequestHandler<TestAiProviderConnectionCommand, AiProviderConnectionTestDto>
{
    public async Task<AiProviderConnectionTestDto> Handle(
        TestAiProviderConnectionCommand request, CancellationToken cancellationToken)
    {
        var provider = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Provider '{request.Id}' não encontrado.");

        var adapter = adapters.FirstOrDefault(item => string.Equals(
            item.Name, provider.Name, StringComparison.OrdinalIgnoreCase));
        if (adapter is null)
            return new AiProviderConnectionTestDto(
                Ok: false,
                Error: $"Adaptador '{provider.Name}' não está registrado nesta build — teste indisponível.");

        try
        {
            var ok = await adapter.TestConnectionAsync(cancellationToken);
            return new AiProviderConnectionTestDto(
                ok,
                ok ? null : "O endpoint não respondeu (teste falhou).");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new AiProviderConnectionTestDto(Ok: false, Error: ex.Message);
        }
    }
}

public sealed class ListAiProviderModelsCommandHandler(
    IAiProviderRepository repository,
    IEnumerable<IAiProvider> adapters)
    : IRequestHandler<ListAiProviderModelsCommand, AiProviderModelsDto>
{
    public async Task<AiProviderModelsDto> Handle(
        ListAiProviderModelsCommand request, CancellationToken cancellationToken)
    {
        var provider = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Provider '{request.Id}' não encontrado.");

        var adapter = adapters.FirstOrDefault(item => string.Equals(
            item.Name, provider.Name, StringComparison.OrdinalIgnoreCase));
        if (adapter is null)
            return new AiProviderModelsDto(
                Models: [],
                Error: $"Adaptador '{provider.Name}' não está registrado nesta build — listagem indisponível.");

        try
        {
            var models = await adapter.ListModelsAsync(cancellationToken);
            return new AiProviderModelsDto(models, Error: null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new AiProviderModelsDto(Models: [], Error: ex.Message);
        }
    }
}
