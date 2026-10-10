using FluentAssertions;
using InfraControl.Application.AI.Tools;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Incidents;
using InfraControl.Application.Incidents.Queries;
using InfraControl.Application.Knowledge.Articles;
using InfraControl.Application.Knowledge.Articles.Queries;
using InfraControl.Domain.Enums;
using Moq;
using MediatR;

namespace InfraControl.UnitTests.AI;

/// <summary>
/// FAI-T008/S7 (docs/34 §20/§21/§52): as duas primeiras tools de
/// leitura mapeadas a queries MediatR EXISTENTES (nunca banco direto) —
/// get_active_incidents (só status ativos, cap de resultados) e
/// search_knowledge (term obrigatório, resumo). Sem Docker: mediator
/// fake.
/// </summary>
public sealed class AiReadToolsTests
{
    private static IncidentDto Incident(string title, IncidentStatus status, DateTime openedAt) =>
        new(
            Guid.NewGuid(),
            title,
            Description: null,
            Severity: "High",
            Status: status.ToString(),
            DeviceId: null,
            OpenedAt: openedAt,
            ResolvedAt: null,
            AlertCount: 0,
            AlertIds: []);

    [Fact]
    public async Task GetActiveIncidents_consulta_somente_status_ativos_e_ordena_por_abertura()
    {
        var openedAt = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(send => send.Send(It.IsAny<SearchIncidentsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SearchIncidentsQuery query, CancellationToken _) =>
            {
                var items = query.Status switch
                {
                    "Open" => new PagedResult<IncidentDto>(
                        [Incident("servidor fora do ar", IncidentStatus.Open, openedAt.AddDays(1))],
                        1, 1, 50),
                    "Monitoring" => new PagedResult<IncidentDto>(
                        [Incident("link instável", IncidentStatus.Monitoring, openedAt)],
                        1, 1, 50),
                    _ => new PagedResult<IncidentDto>([], 0, 1, 50),
                };
                return items;
            });

        var tool = new GetActiveIncidentsTool(mediator.Object);

        var result = await tool.ExecuteAsync(AiToolArguments.Empty);

        result.ToolName.Should().Be("get_active_incidents");
        // ordenado por abertura — mais recente primeiro
        result.Findings.Select(finding => finding.Title)
            .Should().Equal("servidor fora do ar", "link instável");
        result.Total.Should().Be(2);

        // só status ativos foram consultados — nunca Resolved (§21)
        mediator.Verify(send => send.Send(
            It.Is<SearchIncidentsQuery>(query => query.Status == "Resolved"),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetActiveIncidents_respeita_limit_do_argumento()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(send => send.Send(It.IsAny<SearchIncidentsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SearchIncidentsQuery query, CancellationToken _) =>
                new PagedResult<IncidentDto>(
                    Enumerable.Range(1, query.PageSize)
                        .Select(index => Incident($"incidente {index}", IncidentStatus.Open, DateTime.UtcNow))
                        .ToList(),
                    query.PageSize, 1, query.PageSize));

        var tool = new GetActiveIncidentsTool(mediator.Object);

        var result = await tool.ExecuteAsync(AiToolArguments.From(("limit", "3")));

        result.Findings.Should().HaveCount(3);
    }

    [Fact]
    public async Task SearchKnowledge_passa_term_e_limit_para_a_query()
    {
        var articleId = Guid.NewGuid();
        var mediator = new Mock<IMediator>();
        SearchKnowledgeArticlesQuery? captured = null;
        mediator
            .Setup(send => send.Send(It.IsAny<SearchKnowledgeArticlesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SearchKnowledgeArticlesQuery query, CancellationToken _) =>
            {
                captured = query;
                return new PagedResult<KnowledgeArticleDto>(
                    [new KnowledgeArticleDto(articleId, "Como reiniciar o Zabbix", "Passo a passo…", null, KnowledgeArticleStatus.Published)],
                    1, 1, query.PageSize);
            });

        var tool = new SearchKnowledgeTool(mediator.Object);

        var result = await tool.ExecuteAsync(AiToolArguments.From(("term", " zabbix "), ("limit", "5")));

        captured!.Term.Should().Be("zabbix");
        captured.PageSize.Should().Be(5);
        result.Findings.Should().ContainSingle();
        result.Findings[0].Id.Should().Be(articleId.ToString());
        result.Findings[0].Status.Should().Be("Published");
    }

    [Fact]
    public async Task SearchKnowledge_sem_term_lanca_argumento_invalido()
    {
        var tool = new SearchKnowledgeTool(new Mock<IMediator>().Object);

        var act = async () => await tool.ExecuteAsync(AiToolArguments.Empty);

        var failure = await act.Should().ThrowAsync<AiToolArgumentException>();
        failure.Which.Message.Should().Contain("'term' é obrigatório");
    }
}
