using FluentAssertions;
using InfraControl.Domain.Entities;

namespace InfraControl.UnitTests.Notifications;

/// <summary>
/// F9-T001 (docs/07): Notification nasce de um domain event consumido —
/// ADR-012 separa Evento, Notification e Delivery.
/// </summary>
public sealed class NotificationTests
{
    [Fact]
    public void Create_ValidEvent_ShouldBuildNotification()
    {
        var eventId = Guid.NewGuid();

        var notification = Notification.Create(
            eventId, "UserCreatedDomainEvent", "Usuário criado", "O usuário admin@example.com foi criado.");

        notification.EventId.Should().Be(eventId);
        notification.EventType.Should().Be("UserCreatedDomainEvent");
        notification.Title.Should().Be("Usuário criado");
        notification.Message.Should().Be("O usuário admin@example.com foi criado.");
        notification.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_EmptyEventId_ShouldThrowArgumentPtBr()
    {
        var action = () => Notification.Create(
            Guid.Empty, "UserCreatedDomainEvent", "Título", "Mensagem.");

        action.Should().Throw<ArgumentException>()
            .WithMessage("*evento de origem é obrigatório*");
    }

    [Fact]
    public void Create_WithoutEventType_ShouldThrowArgumentPtBr()
    {
        var action = () => Notification.Create(
            Guid.NewGuid(), " ", "Título", "Mensagem.");

        action.Should().Throw<ArgumentException>()
            .WithMessage("*tipo do evento é obrigatório*");
    }

    [Fact]
    public void Create_WithoutTitle_ShouldThrowArgumentPtBr()
    {
        var action = () => Notification.Create(
            Guid.NewGuid(), "UserCreatedDomainEvent", " ", "Mensagem.");

        action.Should().Throw<ArgumentException>()
            .WithMessage("*título da notificação é obrigatório*");
    }

    [Fact]
    public void Create_TitleOverLimit_ShouldThrowArgumentPtBr()
    {
        var action = () => Notification.Create(
            Guid.NewGuid(), "UserCreatedDomainEvent", new string('a', 201), "Mensagem.");

        action.Should().Throw<ArgumentException>()
            .WithMessage("*máximo 200 caracteres*");
    }

    [Fact]
    public void Create_WithoutMessage_ShouldThrowArgumentPtBr()
    {
        var action = () => Notification.Create(
            Guid.NewGuid(), "UserCreatedDomainEvent", "Título", " ");

        action.Should().Throw<ArgumentException>()
            .WithMessage("*mensagem da notificação é obrigatória*");
    }
}
