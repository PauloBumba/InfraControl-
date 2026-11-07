using FluentAssertions;
using InfraControl.Application.Integrations.Zabbix;
using InfraControl.Application.Notifications.Routing;
using InfraControl.Domain.Entities;

namespace InfraControl.UnitTests.Notifications;

/// <summary>
/// F39-T003: precedência substitutiva aprovada no ADR-063 (docs/62 §2–§3):
/// NoPolicy → condicionais que casarem (união dedup) → regra do tipo sem
/// filtros → defaults globais; match AND entre dimensões, OR dentro de
/// lista, tags case-insensitive, contexto ausente não casa.
/// </summary>
public sealed class NotificationRoutingResolverTests
{
    private const string Event = "IncidentCreatedDomainEvent";

    private static NotificationRoutingPolicy EnabledPolicy(
        string defaults = "Email,SignalR", int version = 3)
    {
        var policy = NotificationRoutingPolicy.Create(enabled: true, defaults);
        // Create nasce com Version=1; simula gravações anteriores.
        for (var i = 1; i < version; i++)
            policy.Update(policy.Enabled, policy.Defaults);
        return policy;
    }

    private static ZabbixEventContextDto Context(
        string? triggerId = "22001",
        string[]? hostIds = null,
        string[]? groupIds = null,
        ZabbixEventTagDto[]? tags = null) =>
        new(
            triggerId,
            hostIds ?? ["10"],
            groupIds ?? ["4"],
            tags ?? [new ZabbixEventTagDto("service", "web")]);

    // ── compatibilidade (sem política persistida) ─────────────────────────────

    [Fact]
    public void Resolve_WithoutPolicy_ShouldKeepLegacyFanout()
    {
        var result = NotificationRoutingResolver.Resolve(
            null, [], new NotificationRoutingInput(Event, Severity: 4, Context()));

        result.Origin.Should().Be(NotificationRoutingOrigin.NoPolicy);
        result.Channels.Should().BeNull();
        result.PolicyVersion.Should().BeNull();
    }

    [Fact]
    public void Resolve_WithDisabledPolicy_ShouldKeepLegacyFanout()
    {
        var policy = NotificationRoutingPolicy.Create(enabled: false, "Email");

        var result = NotificationRoutingResolver.Resolve(
            policy, [], new NotificationRoutingInput(Event, 4, Context()));

        result.Origin.Should().Be(NotificationRoutingOrigin.NoPolicy);
        result.Channels.Should().BeNull();
    }

    // ── precedência ───────────────────────────────────────────────────────────

    [Fact]
    public void Resolve_WithoutMatchingRules_ShouldUseGlobalDefaults()
    {
        var result = NotificationRoutingResolver.Resolve(
            EnabledPolicy(), [],
            new NotificationRoutingInput(Event, 4, Context()));

        result.Origin.Should().Be(NotificationRoutingOrigin.GlobalDefault);
        result.Channels.Should().Equal("Email", "SignalR");
        result.PolicyVersion.Should().Be(3);
    }

    [Fact]
    public void Resolve_WithUnconditionalTypeRule_ShouldReplaceDefaults()
    {
        var rule = NotificationRoutingRule.Create(
            Event, enabled: true, null, null,
            hostIds: string.Empty, hostGroupIds: string.Empty,
            triggerId: null, tags: "[]", channels: "WhatsApp");

        var result = NotificationRoutingResolver.Resolve(
            EnabledPolicy(), [rule],
            new NotificationRoutingInput(Event, 4, Context()));

        result.Origin.Should().Be(NotificationRoutingOrigin.TypeRule);
        result.Channels.Should().Equal("WhatsApp");
    }

    [Fact]
    public void Resolve_WithDisabledTypeRule_ShouldFallBackToDefaults()
    {
        var rule = NotificationRoutingRule.Create(
            Event, enabled: false, null, null,
            string.Empty, string.Empty, null, "[]", "WhatsApp");

        var result = NotificationRoutingResolver.Resolve(
            EnabledPolicy(), [rule],
            new NotificationRoutingInput(Event, 4, Context()));

        result.Origin.Should().Be(NotificationRoutingOrigin.GlobalDefault);
        result.Channels.Should().Equal("Email", "SignalR");
    }

    [Fact]
    public void Resolve_ConditionalRulesMatching_ShouldUnionAndDeduplicate()
    {
        var bySeverity = NotificationRoutingRule.Create(
            Event, enabled: true, minSeverity: 4, maxSeverity: null,
            string.Empty, string.Empty, null, "[]", "WhatsApp,Email");
        var byHost = NotificationRoutingRule.Create(
            Event, enabled: true, null, null,
            hostIds: "10", hostGroupIds: string.Empty,
            triggerId: null, tags: "[]", channels: "Email,SignalR");

        var result = NotificationRoutingResolver.Resolve(
            EnabledPolicy(), [bySeverity, byHost],
            new NotificationRoutingInput(Event, 5, Context()));

        result.Origin.Should().Be(NotificationRoutingOrigin.ConditionalUnion);
        result.Channels.Should().Equal("WhatsApp", "Email", "SignalR");
    }

    [Fact]
    public void Resolve_NoConditionalMatch_ShouldUseTypeRuleInsteadOfDefaults()
    {
        var conditional = NotificationRoutingRule.Create(
            Event, enabled: true, minSeverity: 4, maxSeverity: null,
            string.Empty, string.Empty, null, "[]", "WhatsApp");
        var typeRule = NotificationRoutingRule.Create(
            Event, enabled: true, null, null,
            string.Empty, string.Empty, null, "[]", "Telegram");

        var result = NotificationRoutingResolver.Resolve(
            EnabledPolicy(), [conditional, typeRule],
            new NotificationRoutingInput(Event, Severity: 1, Context()));

        result.Origin.Should().Be(NotificationRoutingOrigin.TypeRule);
        result.Channels.Should().Equal("Telegram");
    }

    [Fact]
    public void Resolve_TypeRuleWithEmptyChannels_ShouldYieldNoChannels_WithoutEmailFallback()
    {
        var typeRule = NotificationRoutingRule.Create(
            Event, enabled: true, null, null,
            string.Empty, string.Empty, null, "[]", channels: string.Empty);

        var result = NotificationRoutingResolver.Resolve(
            EnabledPolicy(), [typeRule],
            new NotificationRoutingInput(Event, 4, Context()));

        result.Origin.Should().Be(NotificationRoutingOrigin.TypeRule);
        result.Channels.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_RulesOfOtherEventTypes_ShouldBeIgnored()
    {
        var other = NotificationRoutingRule.Create(
            "AlertCreatedDomainEvent", enabled: true, null, null,
            string.Empty, string.Empty, null, "[]", "WhatsApp");

        var result = NotificationRoutingResolver.Resolve(
            EnabledPolicy(), [other],
            new NotificationRoutingInput(Event, 4, Context()));

        result.Origin.Should().Be(NotificationRoutingOrigin.GlobalDefault);
        result.Channels.Should().Equal("Email", "SignalR");
    }

    // ── filtros de severidade (Zabbix 0–5) ────────────────────────────────────

    [Theory]
    [InlineData(3, 3, 4, false)]
    [InlineData(4, 5, 4, true)]
    [InlineData(0, 2, 1, true)]
    [InlineData(4, null, 5, true)]
    [InlineData(null, 2, 3, false)]
    public void Resolve_SeverityRange_ShouldMatchInclusiveBounds(
        int? min, int? max, int severity, bool shouldMatch)
    {
        var rule = NotificationRoutingRule.Create(
            Event, enabled: true, min, max,
            string.Empty, string.Empty, null, "[]", "WhatsApp");

        var result = NotificationRoutingResolver.Resolve(
            EnabledPolicy(), [rule],
            new NotificationRoutingInput(Event, severity, Context()));

        var expected = shouldMatch
            ? NotificationRoutingOrigin.ConditionalUnion
            : NotificationRoutingOrigin.GlobalDefault;
        result.Origin.Should().Be(expected);
    }

    [Fact]
    public void Resolve_SeverityFilter_WhenEventHasNoSeverity_ShouldNotMatch()
    {
        var rule = NotificationRoutingRule.Create(
            Event, enabled: true, minSeverity: 0, maxSeverity: null,
            string.Empty, string.Empty, null, "[]", "WhatsApp");

        var result = NotificationRoutingResolver.Resolve(
            EnabledPolicy(), [rule],
            new NotificationRoutingInput(Event, Severity: null, Context()));

        result.Origin.Should().Be(NotificationRoutingOrigin.GlobalDefault);
    }

    // ── filtros de host/grupo/trigger (AND entre dimensões, OR na lista) ─────

    [Fact]
    public void Resolve_HostFilter_ShouldMatchAnyHostInList()
    {
        var rule = NotificationRoutingRule.Create(
            Event, enabled: true, null, null,
            hostIds: "99,10", hostGroupIds: string.Empty,
            triggerId: null, tags: "[]", channels: "WhatsApp");

        var result = NotificationRoutingResolver.Resolve(
            EnabledPolicy(), [rule],
            new NotificationRoutingInput(Event, 4, Context(hostIds: ["10"])));

        result.Origin.Should().Be(NotificationRoutingOrigin.ConditionalUnion);
    }

    [Fact]
    public void Resolve_HostAndGroupFilters_ShouldRequireBothDimensions()
    {
        var rule = NotificationRoutingRule.Create(
            Event, enabled: true, null, null,
            hostIds: "10", hostGroupIds: "7",
            triggerId: null, tags: "[]", channels: "WhatsApp");

        var result = NotificationRoutingResolver.Resolve(
            EnabledPolicy(), [rule],
            new NotificationRoutingInput(Event, 4, Context(hostIds: ["10"], groupIds: ["4"])));

        result.Origin.Should().Be(NotificationRoutingOrigin.GlobalDefault);
    }

    [Fact]
    public void Resolve_TriggerFilter_ShouldCompareOrdinally()
    {
        var rule = NotificationRoutingRule.Create(
            Event, enabled: true, null, null,
            string.Empty, string.Empty, triggerId: "22001",
            tags: "[]", channels: "WhatsApp");

        var hit = NotificationRoutingResolver.Resolve(
            EnabledPolicy(), [rule],
            new NotificationRoutingInput(Event, 4, Context(triggerId: "22001")));
        var miss = NotificationRoutingResolver.Resolve(
            EnabledPolicy(), [rule],
            new NotificationRoutingInput(Event, 4, Context(triggerId: "22002")));

        hit.Origin.Should().Be(NotificationRoutingOrigin.ConditionalUnion);
        miss.Origin.Should().Be(NotificationRoutingOrigin.GlobalDefault);
    }

    [Fact]
    public void Resolve_MetadataFilter_WithoutPersistedContext_ShouldNotMatch()
    {
        var rule = NotificationRoutingRule.Create(
            Event, enabled: true, null, null,
            hostIds: "10", hostGroupIds: string.Empty,
            triggerId: null, tags: "[]", channels: "WhatsApp");

        var result = NotificationRoutingResolver.Resolve(
            EnabledPolicy(), [rule],
            new NotificationRoutingInput(Event, 4, Zabbix: null));

        result.Origin.Should().Be(NotificationRoutingOrigin.GlobalDefault);
    }

    // ── filtros de tags (todos os pares, case-insensitive) ────────────────────

    [Fact]
    public void Resolve_TagFilter_ShouldRequireAllPairsIgnoringCase()
    {
        var rule = NotificationRoutingRule.Create(
            Event, enabled: true, null, null,
            string.Empty, string.Empty, null,
            tags: """[{"Key":"Service","Value":"Web"},{"Key":"env","Value":"prod"}]""",
            channels: "WhatsApp");

        var matching = NotificationRoutingResolver.Resolve(
            EnabledPolicy(), [rule],
            new NotificationRoutingInput(Event, 4, Context(tags:
            [
                new ZabbixEventTagDto("service", "web"),
                new ZabbixEventTagDto("ENV", "prod"),
                new ZabbixEventTagDto("extra", "ignored"),
            ])));
        var partial = NotificationRoutingResolver.Resolve(
            EnabledPolicy(), [rule],
            new NotificationRoutingInput(Event, 4, Context(tags:
            [
                new ZabbixEventTagDto("service", "web"),
            ])));

        matching.Origin.Should().Be(NotificationRoutingOrigin.ConditionalUnion);
        partial.Origin.Should().Be(NotificationRoutingOrigin.GlobalDefault);
    }
}
