using FluentAssertions;
using InfraControl.Infrastructure.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace InfraControl.UnitTests.Uploads;

/// <summary>F24-T008 — store físico preserva extensão e isola por módulo.</summary>
public class PhysicalImageUploadStoreTests : IDisposable
{
    private readonly string _contentRoot;
    private readonly PhysicalImageUploadStore _store;

    public PhysicalImageUploadStoreTests()
    {
        _contentRoot = Path.Combine(
            Path.GetTempPath(), "infracontrol-uploads-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_contentRoot);
        _store = new PhysicalImageUploadStore(
            Options.Create(new StorageOptions
            {
                ImageUploadsPath = Path.Combine("wwwroot", "uploads"),
            }),
            new FakeHostEnvironment(_contentRoot));
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
            Directory.Delete(_contentRoot, recursive: true);
    }

    [Theory]
    [InlineData(".png")]
    [InlineData(".jpg")]
    [InlineData(".webp")]
    [InlineData(".avif")]
    [InlineData(".svg")]
    public async Task SaveAsync_ShouldPersistWithOriginalExtension(string extension)
    {
        await using var content = new MemoryStream("fake-image"u8.ToArray());

        var path = await _store.SaveAsync("knowledge", extension, content);

        path.Should().StartWith("/uploads/knowledge/");
        path.Should().EndWith(extension);
        var fileName = Path.GetFileName(path);
        var onDisk = Path.Combine(_contentRoot, "wwwroot", "uploads", "knowledge", fileName!);
        File.Exists(onDisk).Should().BeTrue();
        (await File.ReadAllBytesAsync(onDisk)).Should().Equal("fake-image"u8.ToArray());
    }

    [Fact]
    public async Task SaveAsync_UnknownModule_ShouldThrow()
    {
        await using var content = new MemoryStream([1, 2, 3]);
        var act = () => _store.SaveAsync("evil", ".png", content);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Módulo*");
    }

    [Fact]
    public async Task SaveAsync_DisallowedExtension_ShouldThrow()
    {
        await using var content = new MemoryStream([1, 2, 3]);
        var act = () => _store.SaveAsync("knowledge", ".exe", content);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*não permitido*");
    }

    private sealed class FakeHostEnvironment(string contentRoot) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } =
            new PhysicalFileProvider(contentRoot);
    }
}
