using FluentAssertions;
using InfraControl.Domain.Entities;

namespace InfraControl.UnitTests.Knowledge;

/// <summary>
/// F10-T001: KnowledgeCategory — invariantes pt-BR (docs/04: categorias
/// da base de conhecimento).
/// </summary>
public sealed class KnowledgeCategoryTests
{
    [Fact]
    public void Create_ShouldPersistNameAndDescription()
    {
        var category = KnowledgeCategory.Create(
            "Redes", "Artigos sobre topologia e conectividade.");

        category.Name.Should().Be("Redes");
        category.Description.Should().Be("Artigos sobre topologia e conectividade.");
    }

    [Fact]
    public void Create_EmptyName_ShouldThrowPtBr()
    {
        var act = () => KnowledgeCategory.Create("   ", null);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O nome da categoria é obrigatório.*");
    }

    [Fact]
    public void Create_NameOver120_ShouldThrowPtBr()
    {
        var act = () => KnowledgeCategory.Create(new string('a', 121), null);

        act.Should().Throw<ArgumentException>()
            .WithMessage("O nome da categoria pode ter no máximo 120 caracteres.*");
    }

    [Fact]
    public void Create_DescriptionOver500_ShouldThrowPtBr()
    {
        var act = () => KnowledgeCategory.Create("Redes", new string('d', 501));

        act.Should().Throw<ArgumentException>()
            .WithMessage("A descrição da categoria pode ter no máximo 500 caracteres.*");
    }

    [Fact]
    public void Create_WithoutDescription_ShouldAcceptNull()
    {
        var category = KnowledgeCategory.Create("Backup", null);

        category.Description.Should().BeNull();
        category.Name.Should().Be("Backup");
    }

    [Fact]
    public void Update_ShouldChangeNameAndDescription()
    {
        var category = KnowledgeCategory.Create("Redes", null);

        category.Update("Segurança", "Artigos de segurança da informação.");
        category.Update("Segurança", null);

        category.Name.Should().Be("Segurança");
        category.Description.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldTrimName()
    {
        var category = KnowledgeCategory.Create("  Redes  ", null);

        category.Name.Should().Be("Redes");
    }
}
