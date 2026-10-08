using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.UnitTests.Tasks;

/// <summary>
/// F23-T004/P4c — evidência (§11/ADR-033): allowlist de extensões, 10 MB,
/// nome normalizado (nunca caminho) e StoredName = GUID + extensão.
/// </summary>
public class TaskEvidenceTests
{
    [Fact]
    public void Create_ShouldStoreMetadataAndRaiseCreatedEvent()
    {
        var taskId = Guid.NewGuid();

        var evidence = TaskEvidence.Create(
            taskId, "relatorio.pdf", "application/pdf", 2048, "resultado medido");

        evidence.TaskId.Should().Be(taskId);
        evidence.FileName.Should().Be("relatorio.pdf");
        evidence.ContentType.Should().Be("application/pdf");
        evidence.FileSize.Should().Be(2048);
        evidence.Note.Should().Be("resultado medido");
        // GUID (32 hex) + extensão validada — nunca o nome do usuário.
        evidence.StoredName.Should().MatchRegex("^[0-9a-f]{32}\\.pdf$");
        evidence.DomainEvents.Should()
            .ContainSingle()
            .Which.Should().BeOfType<TaskEvidenceCreatedDomainEvent>();
    }

    [Fact]
    public void Create_EmptyTaskId_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskEvidence.Create(Guid.Empty, "a.png", null, 10);

        action.Should().Throw<ArgumentException>()
            .WithMessage("O id da task é obrigatório.*");
    }

    [Fact]
    public void Create_EmptyFileName_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskEvidence.Create(Guid.NewGuid(), "   ", null, 10);

        action.Should().Throw<ArgumentException>()
            .WithMessage("O nome do arquivo é obrigatório.*");
    }

    [Fact]
    public void Create_FileNameAboveMax_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskEvidence.Create(
            Guid.NewGuid(), new string('n', 197) + ".png", null, 10);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*no máximo 200 caracteres*");
    }

    [Fact]
    public void Create_DisallowedExtension_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskEvidence.Create(
            Guid.NewGuid(), "virus.exe", null, 10);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*Tipo de arquivo não permitido*");
    }

    [Fact]
    public void Create_WithoutExtension_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskEvidence.Create(
            Guid.NewGuid(), "semextensao", null, 10);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*Tipo de arquivo não permitido*");
    }

    [Fact]
    public void Create_WithPathInFileName_ShouldKeepOnlyLastSegment()
    {
        var evidence = TaskEvidence.Create(
            Guid.NewGuid(), @"C:\temp\rack\foto.png", "image/png", 10);

        evidence.FileName.Should().Be("foto.png");
        // StoredName nunca contém separador de caminho (travessia fora).
        evidence.StoredName.Should().NotContainAny("/", "\\");
    }

    [Fact]
    public void Create_UppercaseExtension_ShouldBeAcceptedAndNormalized()
    {
        var evidence = TaskEvidence.Create(
            Guid.NewGuid(), "IMAGEM.PNG", "image/png", 10);

        evidence.StoredName.Should().EndWith(".png");
        evidence.FileName.Should().Be("IMAGEM.PNG");
    }

    [Fact]
    public void Create_EmptyFile_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskEvidence.Create(
            Guid.NewGuid(), "a.png", "image/png", 0);

        action.Should().Throw<ArgumentException>()
            .WithMessage("O arquivo está vazio.*");
    }

    [Fact]
    public void Create_AboveTenMb_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskEvidence.Create(
            Guid.NewGuid(), "grande.png", "image/png",
            TaskEvidence.MaxFileSizeBytes + 1L);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*no máximo 10 MB*");
    }

    [Fact]
    public void Create_NoteAboveMax_ShouldThrowArgumentPtBr()
    {
        var action = () => TaskEvidence.Create(
            Guid.NewGuid(), "a.png", "image/png", 10,
            new string('x', 301));

        action.Should().Throw<ArgumentException>()
            .WithMessage("*no máximo 300 caracteres*");
    }

    [Fact]
    public void Create_DefaultContentType_WhenNullOrWhitespace()
    {
        TaskEvidence.Create(Guid.NewGuid(), "a.txt", "  ", 10)
            .ContentType.Should().Be("application/octet-stream");
    }

    [Fact]
    public void Delete_ShouldRaiseDeletedEvent()
    {
        var evidence = TaskEvidence.Create(Guid.NewGuid(), "a.txt", "text/plain", 10);

        evidence.Delete();

        // O evento de Create continua na lista — o Delete entra junto.
        evidence.DomainEvents.Should()
            .Contain(domainEvent => domainEvent is TaskEvidenceDeletedDomainEvent);
    }
}
