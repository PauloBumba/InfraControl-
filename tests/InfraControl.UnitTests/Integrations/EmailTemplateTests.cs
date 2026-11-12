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

    [Fact]
    public void Build_Header_ShouldCarryTheBrandLogoTile()
    {
        var (html, _) = EmailTemplate.Build("Título", "Descrição");

        // tile navy invertido (135deg) com borda clara — espelho do Logo.tsx
        html.Should().Contain("linear-gradient(135deg,#1f2937 0%,#101828 100%)");
        html.Should().Contain("border:1px solid rgba(255,255,255,0.18)");
        // rack de 3 barras brancas + trilho de rede azul
        html.Should().Contain("background:#f8fafc");
        html.Should().Contain("background:#60a5fa");
        // LEDs de status (verde/âmbar/verde)
        html.Should().Contain("background:#22c55e");
        html.Should().Contain("background:#eab308");
        // wordmark ao lado do tile
        html.Should().Contain(">InfraControl</td>");
    }

    [Fact]
    public void Build_WithCta_ShouldUseTheBrandAccentBlue()
    {
        var (html, _) = EmailTemplate.Build(
            "Título", "Descrição",
            new EmailTemplate.Cta("Abrir", "https://app.test/x"));

        // CTA alinhado ao --uni-accent-hover do app (#0052cc era off-palette)
        html.Should().Contain("background:#2563eb");
        html.Should().Contain("background:#2563eb;color:#ffffff");
    }

    [Fact]
    public void Build_ShouldCloseTheCardWithTheBrandRail()
    {
        var (html, _) = EmailTemplate.Build("Título", "Descrição");

        // trilho de marca navy→azul entre corpo e rodapé
        html.Should().Contain("#1f2937 55%,#60a5fa 100%");
    }
}
