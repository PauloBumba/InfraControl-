using FluentAssertions;
using InfraControl.Application.Uploads;

namespace InfraControl.UnitTests.Uploads;

/// <summary>F24-T008 — allowlist reutilizável de tipos de imagem.</summary>
public class ImageUploadRulesTests
{
    [Theory]
    [InlineData("foto.jpg", "image/jpeg")]
    [InlineData("foto.JPEG", "image/jpeg")]
    [InlineData("a.png", "image/png")]
    [InlineData("a.gif", "image/gif")]
    [InlineData("a.webp", "image/webp")]
    [InlineData("a.bmp", "image/bmp")]
    [InlineData("a.svg", "image/svg+xml")]
    [InlineData("a.avif", "image/avif")]
    public void EnsureAllowed_AcceptedImageTypes_ShouldPass(string fileName, string contentType)
    {
        var act = () => ImageUploadRules.EnsureAllowed(fileName, contentType, 100);
        act.Should().NotThrow();
        ImageUploadRules.NormalizeExtension(fileName)
            .Should().Be(Path.GetExtension(fileName).ToLowerInvariant());
    }

    [Fact]
    public void EnsureAllowed_DisallowedExtension_ShouldThrowPtBr()
    {
        var act = () => ImageUploadRules.EnsureAllowed("malware.exe", "application/octet-stream", 10);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Tipo de arquivo não permitido*");
    }

    [Fact]
    public void EnsureAllowed_Oversize_ShouldThrowPtBr()
    {
        var act = () => ImageUploadRules.EnsureAllowed(
            "a.png", "image/png", ImageUploadRules.MaxFileSizeBytes + 1);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*no máximo*");
    }

    [Theory]
    [InlineData("knowledge")]
    [InlineData("general")]
    [InlineData("Knowledge")]
    public void NormalizeModule_Allowed_ShouldNormalize(string module)
    {
        ImageUploadRules.NormalizeModule(module).Should().Be(module.ToLowerInvariant());
    }

    [Fact]
    public void NormalizeModule_Unknown_ShouldThrowPtBr()
    {
        var act = () => ImageUploadRules.NormalizeModule("../etc");
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Módulo de upload inválido*");
    }
}
