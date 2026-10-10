using System.Text.RegularExpressions;
using InfraControl.Application.Network.Services;
using InfraControl.Infrastructure.NetworkServices.Ports;

namespace InfraControl.Infrastructure.NetworkServices;

/// <summary>
/// Driver Cisco (F18-T005 · ADR-042 §3): <c>show access-lists</c> do
/// IOS clássico. Cabeçalhos ("Standard/Extended IP access list X" ou
/// "IP access list X" do IOS-XE) separam os ACLs; cada linha de regra
/// (<c>&lt;seq&gt; permit|deny …</c>) vira um item com protocolo
/// extraído quando é palavra conhecida (ip/tcp/udp/icmp/…) e a
/// expressão CRU restante — sem inventar semântica de host/any/
/// wildcard. Linhas que não são regra nem cabeçalho são ignoradas.
/// </summary>
public sealed class CiscoFirewallVendorReader : IFirewallVendorReader
{
    private static readonly Regex Header = new(
        @"^(?<scope>Standard|Extended)?\s*IP access list\s+(?<name>\S+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex Rule = new(
        @"^(?<sequence>\d+)\s+(?<action>permit|deny)\s+(?<rest>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Palavras que ocupam a posição de protocolo nas ACLs
    /// estendidas do IOS (nas padrões a 1ª palavra já é o endereço).</summary>
    private static readonly HashSet<string> Protocols = new(StringComparer.OrdinalIgnoreCase)
    {
        "ip", "tcp", "udp", "icmp", "icmp6", "ipv6", "gre", "esp", "ah",
        "igmp", "ospf", "eigrp", "tcp6", "udp6", "nos"
    };

    public string Vendor => "cisco";

    public string Command => "show access-lists";

    public IReadOnlyList<FirewallRuleDto> Parse(string commandOutput)
    {
        var rules = new List<FirewallRuleDto>();
        var acl = string.Empty;
        var scope = string.Empty;

        foreach (var raw in commandOutput.Split('\n'))
        {
            var line = raw.TrimEnd('\r').Trim();
            if (line.Length == 0)
                continue;

            var header = Header.Match(line);
            if (header.Success)
            {
                acl = header.Groups["name"].Value;
                scope = header.Groups["scope"].Value; // vazio no "IP access list X" do IOS-XE
                continue;
            }

            var rule = Rule.Match(line);
            if (!rule.Success || acl.Length == 0)
                continue;

            var rest = rule.Groups["rest"].Value.Trim();
            var protocol = string.Empty;
            var space = rest.IndexOf(' ');
            if (space > 0)
            {
                var first = rest[..space];
                if (Protocols.Contains(first))
                {
                    protocol = first.ToLowerInvariant();
                    rest = rest[(space + 1)..].Trim();
                }
            }

            rules.Add(new FirewallRuleDto(
                acl,
                scope,
                long.Parse(rule.Groups["sequence"].Value),
                rule.Groups["action"].Value.ToLowerInvariant(),
                protocol,
                rest));
        }

        return rules;
    }
}
