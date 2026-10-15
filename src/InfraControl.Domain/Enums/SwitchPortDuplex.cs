using System.Text.Json.Serialization;

namespace InfraControl.Domain.Enums;

/// <summary>
/// Duplex de uma porta de switch — enum da ficha docs/42 §B L145
/// (Full/Half/Auto). Persistido como string, como os demais enums da casa.
/// Coluna null = ainda não observada.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SwitchPortDuplex
{
    /// <summary>Auto-negociação — valor da ficha; nenhum coletor reporta assim.</summary>
    Auto = 0,

    /// <summary>Meio-duplex.</summary>
    Half = 1,

    /// <summary>Integral.</summary>
    Full = 2,
}

/// <summary>
/// Converte o booleano do controller em <see cref="SwitchPortDuplex"/>.
/// REGRA: campo AUSENTE ⇒ null, nunca Half — o parser antigo do UniFi virava
/// false quando <c>full_duplex</c> não existia no JSON e inventava meio-duplex.
/// </summary>
public static class SwitchPortDuplexParser
{
    public static SwitchPortDuplex? Parse(bool? fullDuplex) => fullDuplex switch
    {
        true => SwitchPortDuplex.Full,
        false => SwitchPortDuplex.Half,
        null => null,
    };
}
