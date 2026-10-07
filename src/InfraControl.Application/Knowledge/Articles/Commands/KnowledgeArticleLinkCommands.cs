using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Incidents.Ports;
using InfraControl.Application.Knowledge.Articles.Ports;
using InfraControl.Application.Sla.Services.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Knowledge.Articles.Commands;

// F10-T004: vínculo Artigo ↔ Device/Incident/Service — artigo, alvo ou
// vínculo inexistente → 404 pt-BR; duplicidade → 409 pt-BR. Escrita com
// RoleAccess.Operate (ADR-016); o estado atual vai no dto do GET.

public sealed record LinkKnowledgeArticleDeviceCommand(
    Guid ArticleId,
    Guid DeviceId) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class LinkKnowledgeArticleDeviceCommandHandler(
    IKnowledgeArticleRepository articleRepository,
    IDeviceRepository deviceRepository,
    IKnowledgeArticleDeviceRepository linkRepository,
    ILogger<LinkKnowledgeArticleDeviceCommandHandler> logger)
    : IRequestHandler<LinkKnowledgeArticleDeviceCommand>
{
    public async Task Handle(
        LinkKnowledgeArticleDeviceCommand request, CancellationToken cancellationToken)
    {
        _ = await articleRepository.GetByIdAsync(request.ArticleId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Artigo '{request.ArticleId}' não encontrado.");

        _ = await deviceRepository.GetByIdAsync(request.DeviceId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Device '{request.DeviceId}' não encontrado.");

        if (await linkRepository.ExistsAsync(
                request.ArticleId, request.DeviceId, cancellationToken))
            throw new InvalidOperationException(
                "O device já está vinculado ao artigo.");

        await linkRepository.AddAsync(
            KnowledgeArticleDevice.Create(request.ArticleId, request.DeviceId),
            cancellationToken);
        await linkRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "KnowledgeArticle {ArticleId} linked to device {DeviceId}.",
            request.ArticleId, request.DeviceId);
    }
}

public sealed record UnlinkKnowledgeArticleDeviceCommand(
    Guid ArticleId,
    Guid DeviceId) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UnlinkKnowledgeArticleDeviceCommandHandler(
    IKnowledgeArticleRepository articleRepository,
    IKnowledgeArticleDeviceRepository linkRepository,
    ILogger<UnlinkKnowledgeArticleDeviceCommandHandler> logger)
    : IRequestHandler<UnlinkKnowledgeArticleDeviceCommand>
{
    public async Task Handle(
        UnlinkKnowledgeArticleDeviceCommand request, CancellationToken cancellationToken)
    {
        _ = await articleRepository.GetByIdAsync(request.ArticleId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Artigo '{request.ArticleId}' não encontrado.");

        var link = await linkRepository.FindAsync(
                request.ArticleId, request.DeviceId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Vínculo do artigo '{request.ArticleId}' com o device "
                + $"'{request.DeviceId}' não encontrado.");

        link.Delete(); // ADR-030 §5 — evento na mesma transação
        linkRepository.Remove(link);
        await linkRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "KnowledgeArticle {ArticleId} unlinked from device {DeviceId}.",
            request.ArticleId, request.DeviceId);
    }
}

public sealed record LinkKnowledgeArticleIncidentCommand(
    Guid ArticleId,
    Guid IncidentId) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class LinkKnowledgeArticleIncidentCommandHandler(
    IKnowledgeArticleRepository articleRepository,
    IIncidentRepository incidentRepository,
    IKnowledgeArticleIncidentRepository linkRepository,
    ILogger<LinkKnowledgeArticleIncidentCommandHandler> logger)
    : IRequestHandler<LinkKnowledgeArticleIncidentCommand>
{
    public async Task Handle(
        LinkKnowledgeArticleIncidentCommand request, CancellationToken cancellationToken)
    {
        _ = await articleRepository.GetByIdAsync(request.ArticleId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Artigo '{request.ArticleId}' não encontrado.");

        _ = await incidentRepository.GetByIdAsync(request.IncidentId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Incidente '{request.IncidentId}' não encontrado.");

        if (await linkRepository.ExistsAsync(
                request.ArticleId, request.IncidentId, cancellationToken))
            throw new InvalidOperationException(
                "O incidente já está vinculado ao artigo.");

        await linkRepository.AddAsync(
            KnowledgeArticleIncident.Create(request.ArticleId, request.IncidentId),
            cancellationToken);
        await linkRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "KnowledgeArticle {ArticleId} linked to incident {IncidentId}.",
            request.ArticleId, request.IncidentId);
    }
}

public sealed record UnlinkKnowledgeArticleIncidentCommand(
    Guid ArticleId,
    Guid IncidentId) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UnlinkKnowledgeArticleIncidentCommandHandler(
    IKnowledgeArticleRepository articleRepository,
    IKnowledgeArticleIncidentRepository linkRepository,
    ILogger<UnlinkKnowledgeArticleIncidentCommandHandler> logger)
    : IRequestHandler<UnlinkKnowledgeArticleIncidentCommand>
{
    public async Task Handle(
        UnlinkKnowledgeArticleIncidentCommand request, CancellationToken cancellationToken)
    {
        _ = await articleRepository.GetByIdAsync(request.ArticleId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Artigo '{request.ArticleId}' não encontrado.");

        var link = await linkRepository.FindAsync(
                request.ArticleId, request.IncidentId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Vínculo do artigo '{request.ArticleId}' com o incidente "
                + $"'{request.IncidentId}' não encontrado.");

        link.Delete(); // ADR-030 §5 — evento na mesma transação
        linkRepository.Remove(link);
        await linkRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "KnowledgeArticle {ArticleId} unlinked from incident {IncidentId}.",
            request.ArticleId, request.IncidentId);
    }
}

public sealed record LinkKnowledgeArticleServiceCommand(
    Guid ArticleId,
    Guid ServiceId) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class LinkKnowledgeArticleServiceCommandHandler(
    IKnowledgeArticleRepository articleRepository,
    IServiceRepository serviceRepository,
    IKnowledgeArticleServiceRepository linkRepository,
    ILogger<LinkKnowledgeArticleServiceCommandHandler> logger)
    : IRequestHandler<LinkKnowledgeArticleServiceCommand>
{
    public async Task Handle(
        LinkKnowledgeArticleServiceCommand request, CancellationToken cancellationToken)
    {
        _ = await articleRepository.GetByIdAsync(request.ArticleId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Artigo '{request.ArticleId}' não encontrado.");

        _ = await serviceRepository.GetByIdAsync(request.ServiceId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Serviço '{request.ServiceId}' não encontrado.");

        if (await linkRepository.ExistsAsync(
                request.ArticleId, request.ServiceId, cancellationToken))
            throw new InvalidOperationException(
                "O serviço já está vinculado ao artigo.");

        await linkRepository.AddAsync(
            KnowledgeArticleService.Create(request.ArticleId, request.ServiceId),
            cancellationToken);
        await linkRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "KnowledgeArticle {ArticleId} linked to service {ServiceId}.",
            request.ArticleId, request.ServiceId);
    }
}

public sealed record UnlinkKnowledgeArticleServiceCommand(
    Guid ArticleId,
    Guid ServiceId) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class UnlinkKnowledgeArticleServiceCommandHandler(
    IKnowledgeArticleRepository articleRepository,
    IKnowledgeArticleServiceRepository linkRepository,
    ILogger<UnlinkKnowledgeArticleServiceCommandHandler> logger)
    : IRequestHandler<UnlinkKnowledgeArticleServiceCommand>
{
    public async Task Handle(
        UnlinkKnowledgeArticleServiceCommand request, CancellationToken cancellationToken)
    {
        _ = await articleRepository.GetByIdAsync(request.ArticleId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Artigo '{request.ArticleId}' não encontrado.");

        var link = await linkRepository.FindAsync(
                request.ArticleId, request.ServiceId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Vínculo do artigo '{request.ArticleId}' com o serviço "
                + $"'{request.ServiceId}' não encontrado.");

        link.Delete(); // ADR-030 §5 — evento na mesma transação
        linkRepository.Remove(link);
        await linkRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "KnowledgeArticle {ArticleId} unlinked from service {ServiceId}.",
            request.ArticleId, request.ServiceId);
    }
}
