using FluentAssertions;
using InfraControl.Infrastructure.Integrations.Email;

namespace InfraControl.UnitTests.Integrations;

/// <summary>
/// F41-T002: layout HTML único (600px, 1 CTA, plain-text equivalente) —
/// blocos por ação; nunca expõe a URL cru sem o botão junto.
/// </summary>
public sealed class EmailTemplateTests
{
    [Fact]
    public void Build_WithCta_ShouldRenderButtonAndFallbackUrl()
    {
        var (html, plainText) = EmailTemplate.Build(
            "Redefinição de senha — InfraControl",
            "Recebemos um pedido de redefinição.",
            new EmailTemplate.Cta("Redefinir senha", "https://app.test/reset?token=abc"));

        html.Should().Contain("Redefinir senha");
        html.Should().Contain("https://app.test/reset?token=abc");
        html.Should().Contain("InfraControl");
        html.Should().Contain("Recebemos um pedido de redefini&#231;&#227;o.");
        plainText.Should().Contain("Redefinir senha: https://app.test/reset?token=abc");
        plainText.Should().Contain("Recebemos um pedido de redefinição.");
        plainText.Should().Contain("Mensagem automática do InfraControl.");
    }

    [Fact]
    public void Build_WithoutCta_ShouldNotRenderButton()
    {
        var (html, _) = EmailTemplate.Build(
            "Incidente aberto",
            "O incidente 'X' foi aberto (Critical).");

        html.Should().Contain("Incidente aberto");
        html.Should().NotContain("background:#2563eb");
        html.Should().NotContain("href=");
        html.Should().NotContain("Redefinir senha");
    }

    [Fact]
    public void Build_WithDetails_ShouldRenderTableRows()
    {
        var (html, plainText) = EmailTemplate.Build(
            "Título", "Descrição",
            details: [("Severidade", "Critical"), ("Dispositivo", "srv-01")]);

        html.Should().Contain("Severidade");
        html.Should().Contain("Critical");
        html.Should().Contain("srv-01");
        plainText.Should().Contain("Severidade: Critical");
        plainText.Should().Contain("Dispositivo: srv-01");
        html.Should().Contain("Descri&#231;&#227;o");
    }

    [Fact]
    public void Build_ShouldHtmlEncodeDynamicContent()
    {
        var (html, _) = EmailTemplate.Build(
            "<script>alert(1)</script>",
            "Descrição com <b>html</b>");

        html.Should().NotContain("<script>alert(1)</script>");
        html.Should().Contain("&lt;script&gt;");
        html.Should().NotContain("<b>html</b>");
    }

    [Fact]
    public void Build_WithGreeting_ShouldRenderBeforeTitle()
    {
        var (html, plainText) = EmailTemplate.Build(
            "Título", "Descrição", greeting: "Olá, Fulano:");

        html.Should().Contain("Ol&#225;, Fulano:");
        plainText.Should().Contain("Olá, Fulano:");
    }
}
