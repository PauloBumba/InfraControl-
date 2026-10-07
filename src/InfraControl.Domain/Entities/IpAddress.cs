using System.Net;
using System.Net.Sockets;
using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>
/// IP cadastrado em uma interface — NetworkInterface 1:N IpAddress (docs/03,
/// F2-T006), IP único GLOBAL (pré-check no handler + índice UNIQUE canônico na
/// migração). Normalização canônica pt-BR (IPv4 192.0.2.10, IPv6 minúsculo
/// 2001:db8::1). ValueObject órfão em Domain/ValueObjects/IpAddress.cs fica
/// fora do escopo (sem consumidores).
/// </summary>
public sealed class IpAddress : AggregateRoot
{
    public const int IpMaxLength = 45;

    public Guid InterfaceId { get; private set; }
    public string Ip { get; private set; } = string.Empty;

    private IpAddress() { }

    /// <summary>ADR-030 §5 — exclusão física (handler persiste o Remove).</summary>
    public void Delete() =>
        RaiseDomainEvent(new IpAddressDeletedDomainEvent(Id, InterfaceId, Ip));

    public static IpAddress Create(Guid interfaceId, string ip)
    {
        if (interfaceId == Guid.Empty)
            throw new ArgumentException("O id da interface é obrigatório.", nameof(interfaceId));

        var entity = new IpAddress();
        entity.InterfaceId = interfaceId;
        entity.Update(ip);
        entity.ClearDomainEvents(); // descarta o Updated transitório do Create
        entity.RaiseDomainEvent(new IpAddressCreatedDomainEvent(entity.Id, entity.InterfaceId, entity.Ip));
        return entity;
    }

    public void Update(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
            throw new ArgumentException("O IP é obrigatório.", nameof(ip));

        Ip = Normalize(ip)
            ?? throw new ArgumentException("O IP informado é inválido.", nameof(ip));

        RaiseDomainEvent(new IpAddressUpdatedDomainEvent(Id, InterfaceId, Ip));
    }

    private static string? Normalize(string value)
    {
        if (!IPAddress.TryParse(value.Trim(), out var address))
            return null;

        if (address.AddressFamily is not (AddressFamily.InterNetwork or AddressFamily.InterNetworkV6))
            return null;

        var canonical = address.ToString();
        return canonical.Length > IpMaxLength ? null : canonical;
    }
}
