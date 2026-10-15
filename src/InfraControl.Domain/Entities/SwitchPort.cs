using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Porta física de um switch — Switch(Device) 1:N SwitchPort (docs/03, F3-T002).
/// Nome único POR SWITCH (case-insensitive: pré-check no handler + índice
/// (device_id, LOWER(name)) via SQL bruto na migração). LinkUp null = ainda
/// não observado; o discovery SNMP da F4 (IF-MIB) é quem passa a preencher —
/// propriedades operacionais (velocidade/PoE/VLAN/tráfego) também vêm do
/// SNMP, não do cadastro manual (decisão do usuário na T002).
/// F30-T001 S4a: velocidade/duplex passaram a ser PERSISTIDOS (cadastro
/// semi-estático, colunas speed/duplex) — preenchidos por <c>Observe</c>
/// (controller UniFi/SNMP). PoE continua NÃO persistido: ADR-028 §10 nomeia
/// potência PoE como métrica sob demanda (§7 item 8 do docs/51).
/// </summary>
public sealed class SwitchPort : AggregateRoot
{
    public const int NameMaxLength = 60;
    public const int DescriptionMaxLength = 500;

    /// <summary>Device que é o switch dono da porta.</summary>
    public Guid DeviceId { get; private set; }

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    /// <summary>Estado de link; null = porta ainda não observada (F4).</summary>
    public bool? LinkUp { get; private set; }

    /// <summary>Velocidade do enlace; null = ainda não observada (docs/42 §B).</summary>
    public SwitchPortSpeed? Speed { get; private set; }

    /// <summary>Duplex; null = ainda não observado (docs/42 §B).</summary>
    public SwitchPortDuplex? Duplex { get; private set; }

    private SwitchPort() { }

    /// <summary>ADR-030 §5 — exclusão física (handler persiste o Remove).</summary>
    public void Delete() =>
        RaiseDomainEvent(new SwitchPortDeletedDomainEvent(Id, DeviceId, Name));

    public static SwitchPort Create(Guid deviceId, string name, string? description = null)
    {
        if (deviceId == Guid.Empty)
            throw new ArgumentException("O id do switch é obrigatório.", nameof(deviceId));

        var port = new SwitchPort();
        port.DeviceId = deviceId;
        port.Update(name, description, port.LinkUp);
        port.ClearDomainEvents(); // descarta o Updated transitório do Create
        port.RaiseDomainEvent(new SwitchPortCreatedDomainEvent(port.Id, port.DeviceId, port.Name));
        return port;
    }

    public void Update(string name, string? description, bool? linkUp)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome da porta é obrigatório.", nameof(name));

        name = name.Trim();
        if (name.Length > NameMaxLength)
            throw new ArgumentException(
                $"O nome da porta pode ter no máximo {NameMaxLength} caracteres.", nameof(name));

        if (description?.Length > DescriptionMaxLength)
            throw new ArgumentException(
                $"A descrição da porta pode ter no máximo {DescriptionMaxLength} caracteres.", nameof(description));

        Name = name;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        LinkUp = linkUp;

        RaiseDomainEvent(new SwitchPortUpdatedDomainEvent(Id, DeviceId, Name));
    }

    /// <summary>
    /// Observação operacional vinda do controller (UniFi/SNMP) — F30-T001 S4a.
    /// Diferente de <see cref="Update"/> (cadastro manual), aqui sim gravam-se
    /// velocidade/duplex. <c>linkUp</c> grava o que veio (null = não observado);
    /// <c>speed</c>/<c>duplex</c> só gravam quando reportados — ausência de
    /// leitura não apaga o último valor (velocidade é semi-estática: porta sem
    /// resposta não perdeu o enlace de 1G). Retorna true se algo mudou: sync
    /// idempotente não gera evento nem contagem de update.
    /// </summary>
    public bool Observe(
        bool? linkUp, SwitchPortSpeed? speed, SwitchPortDuplex? duplex)
    {
        var changed = LinkUp != linkUp
            || (speed is not null && Speed != speed)
            || (duplex is not null && Duplex != duplex);

        if (!changed)
            return false;

        LinkUp = linkUp;
        if (speed is not null) Speed = speed;
        if (duplex is not null) Duplex = duplex;

        RaiseDomainEvent(new SwitchPortUpdatedDomainEvent(Id, DeviceId, Name));
        return true;
    }
}
