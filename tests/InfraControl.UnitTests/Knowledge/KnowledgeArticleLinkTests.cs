using FluentAssertions;
using InfraControl.Domain.Entities;

namespace InfraControl.UnitTests.Knowledge;

/// <summary>
/// F10-T004: vínculos N:N Artigo ↔ Device/Incident/Service — o par é
/// obrigatório (Guid vazio = 400 pt-BR) e a chave é o par composto.
/// </summary>
public sealed class KnowledgeArticleLinkTests
{
    private static readonly Guid Article = Guid.NewGuid();

    // ── KnowledgeArticleDevice ────────────────────────────────────────────

    [Fact]
    public void KnowledgeArticleDevice_Create_ShouldSetPair()
    {
        var device = Guid.NewGuid();

        var link = KnowledgeArticleDevice.Create(Article, device);

        link.ArticleId.Should().Be(Article);
        link.DeviceId.Should().Be(device);
    }

    [Fact]
    public void KnowledgeArticleDevice_Create_EmptyArticleId_ShouldThrow()
    {
        var act = () => KnowledgeArticleDevice.Create(Guid.Empty, Guid.NewGuid());

        act.Should().Throw<ArgumentException>()
            .WithMessage("O id do artigo é obrigatório.*");
    }

    [Fact]
    public void KnowledgeArticleDevice_Create_EmptyDeviceId_ShouldThrow()
    {
        var act = () => KnowledgeArticleDevice.Create(Article, Guid.Empty);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O id do device é obrigatório.*");
    }

    // ── KnowledgeArticleIncident ──────────────────────────────────────────

    [Fact]
    public void KnowledgeArticleIncident_Create_ShouldSetPair()
    {
        var incident = Guid.NewGuid();

        var link = KnowledgeArticleIncident.Create(Article, incident);

        link.ArticleId.Should().Be(Article);
        link.IncidentId.Should().Be(incident);
    }

    [Fact]
    public void KnowledgeArticleIncident_Create_EmptyArticleId_ShouldThrow()
    {
        var act = () => KnowledgeArticleIncident.Create(Guid.Empty, Guid.NewGuid());

        act.Should().Throw<ArgumentException>()
            .WithMessage("O id do artigo é obrigatório.*");
    }

    [Fact]
    public void KnowledgeArticleIncident_Create_EmptyIncidentId_ShouldThrow()
    {
        var act = () => KnowledgeArticleIncident.Create(Article, Guid.Empty);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O id do incidente é obrigatório.*");
    }

    // ── KnowledgeArticleService ───────────────────────────────────────────

    [Fact]
    public void KnowledgeArticleService_Create_ShouldSetPair()
    {
        var service = Guid.NewGuid();

        var link = KnowledgeArticleService.Create(Article, service);

        link.ArticleId.Should().Be(Article);
        link.ServiceId.Should().Be(service);
    }

    [Fact]
    public void KnowledgeArticleService_Create_EmptyArticleId_ShouldThrow()
    {
        var act = () => KnowledgeArticleService.Create(Guid.Empty, Guid.NewGuid());

        act.Should().Throw<ArgumentException>()
            .WithMessage("O id do artigo é obrigatório.*");
    }

    [Fact]
    public void KnowledgeArticleService_Create_EmptyServiceId_ShouldThrow()
    {
        var act = () => KnowledgeArticleService.Create(Article, Guid.Empty);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O id do serviço é obrigatório.*");
    }
}
