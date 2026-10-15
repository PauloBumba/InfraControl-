using System.Globalization;
using System.Text.Json.Serialization;

namespace InfraControl.Domain.Enums;

/// <summary>
/// Velocidade do enlace de uma porta de switch — enum da ficha
/// docs/42 §B L144 (10M/100M/1G/10G/40G/100G/Auto).
/// Persistida como STRING (<c>HasConversion&lt;string&gt;</c>), como todo enum
/// da casa: reordenar/incluir membros não corrói dado antigo.
/// O <c>JsonConverter</c> é por TIPO porque o MVC não registra
/// JsonStringEnumConverter global — o contrato numérico legado de
/// <c>KnowledgeArticleStatus</c> (0..7) não pode mudar.
/// Coluna null = ainda não observada.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SwitchPortSpeed
{
    /// <summary>Auto-negociação — valor da ficha; nenhum coletor reporta taxa assim.</summary>
    Auto = 0,

    /// <summary>10 Mbps.</summary>
    Speed10M = 1,

    /// <summary>100 Mbps.</summary>
    Speed100M = 2,

    /// <summary>1 Gbps.</summary>
    Speed1G = 3,

    /// <summary>10 Gbps.</summary>
    Speed10G = 4,

    /// <summary>40 Gbps.</summary>
    Speed40G = 5,

    /// <summary>100 Gbps.</summary>
    Speed100G = 6,
}

/// <summary>
/// Converte o token que o controller reportou em <see cref="SwitchPortSpeed"/>.
/// Formatos reais encontrados: UniFi devolve "1G", "1000" (Mbps puro) ou
/// "{n}M" (<c>UnifiApiClient.ReadSpeed</c> normaliza int para "{n}M").
/// REGRA: taxa fora da enum da ficha ⇒ <c>null</c> — nunca Auto, nunca 0.
/// 2.5G/5G multi-gig não constam em docs/42 L144 → null (pendência §7 do
/// docs/51: incluir no enum ou mudar para bps, decisão humana).
/// </summary>
public static class SwitchPortSpeedParser
{
    public static SwitchPortSpeed? Parse(string? observed)
    {
        if (string.IsNullOrWhiteSpace(observed))
            return null;

        var token = observed.Trim();

        if (token.Equals("auto", StringComparison.OrdinalIgnoreCase))
            return SwitchPortSpeed.Auto;

        // "10000M" → 10000 Mbps · "10G" → 10000 Mbps · "1000" → 1000 Mbps
        // (sem sufixo o UniFi manda Mbps puro; "2.5G" não parseia → null).
        var suffix = token[^1];
        if (suffix is 'M' or 'm' or 'G' or 'g')
        {
            if (!long.TryParse(
                    token[..^1], NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var scaled))
                return null;

            return MapMbps(suffix is 'G' or 'g' ? scaled * 1000 : scaled);
        }

        return long.TryParse(
                token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mbps)
            ? MapMbps(mbps)
            : null;
    }

    private static SwitchPortSpeed? MapMbps(long mbps) => mbps switch
    {
        10 => SwitchPortSpeed.Speed10M,
        100 => SwitchPortSpeed.Speed100M,
        1000 => SwitchPortSpeed.Speed1G,
        10000 => SwitchPortSpeed.Speed10G,
        40000 => SwitchPortSpeed.Speed40G,
        100000 => SwitchPortSpeed.Speed100G,
        _ => null, // 0 (porta down), 2500/5000 (multi-gig) e lixo: fora da ficha
    };
}
