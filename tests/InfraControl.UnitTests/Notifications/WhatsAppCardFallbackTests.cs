using FluentAssertions;
using InfraControl.Application.Notifications.WhatsApp;

namespace InfraControl.UnitTests.Notifications;

/// <summary>
/// Pedido do usuário: toda mensagem WhatsApp de alerta/incidente sai como
/// card — quando não há série Zabbix, o fallback monta o
/// <see cref="IncidentChartData"/> sem pontos (o renderer mostra
/// "Sem dados históricos disponíveis") em vez de deixar o texto solto.
/// </summary>
public sealed class WhatsAppCardFallbackTests
{
    [Fact]
    public void Build_AlertMessage_ShouldExtractQuotedSubject()
    {
        var card = WhatsAppCardFallback.Build(
            "Incidente aberto",
            "O incidente 'Docker: Container /api: Healthcheck failing' foi aberto (CRÍTICO).",
            "IncidentCreatedDomainEvent",
            new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc),
            "infracontrol-api");

        card.Title.Should().Be("Docker: Container /api: Healthcheck failing");
        card.DeviceLabel.Should().Be("infracontrol-api");
        card.Kind.Should().Be(ChartCardKind.Incident);
        card.SeverityLabel.Should().Be("Critical");
        card.Series.Should().BeEmpty();
    }

    [Fact]
    public void Build_ResolvedEvent_ShouldBeResolutionKind()
    {
        var card = WhatsAppCardFallback.Build(
            "Incidente resolvido",
            "O incidente 'Host X fora do ar' foi resolvido (CRÍTICO).",
            "IncidentResolvedDomainEvent",
            DateTime.UtcNow);

        card.Kind.Should().Be(ChartCardKind.Resolution);
    }

    [Fact]
    public void Build_WarningSeverity_ShouldBeWarningKind()
    {
        var card = WhatsAppCardFallback.Build(
            "Alerta criado",
            "O alerta 'Disco quase cheio' foi aberto (Warning).",
            "AlertCreatedDomainEvent",
            DateTime.UtcNow);

        card.Kind.Should().Be(ChartCardKind.Warning);
        card.SeverityLabel.Should().Be("Warning");
    }

    [Fact]
    public void Build_NoQuotes_ShouldUseFirstMessageLine()
    {
        var card = WhatsAppCardFallback.Build(
            "Tarefa resolvida",
            "A tarefa de revisão foi concluída.\nDetalhes adicionais aqui.",
            "TaskResolvedDomainEvent",
            DateTime.UtcNow);

        card.Title.Should().Be("A tarefa de revisão foi concluída.");
    }

    [Fact]
    public void Build_EmptyMessage_ShouldFallbackToTitle()
    {
        var card = WhatsAppCardFallback.Build(
            "Notificação do sistema", "", "CustomEvent", DateTime.UtcNow);

        card.Title.Should().Be("Notificação do sistema");
        card.Kind.Should().Be(ChartCardKind.Incident);
    }
}
