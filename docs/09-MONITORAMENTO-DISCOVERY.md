# Monitoramento e Discovery

## Discovery

Usar:

- SNMP
- ICMP
- LLDP
- CDP
- ARP
- DHCP
- DNS
- MAC tables
- SSH
- HTTP/HTTPS
- TCP

## Providers

```text
IDiscoveryProvider
├── GenericSnmp
├── Cisco
├── MikroTik
├── Ubiquiti
├── Aruba
└── Intelbras
```

## Monitoring

Checks:

- Ping
- SNMP
- HTTP
- HTTPS
- TCP
- Port
- CPU
- Memory
- Temperature

Somente monitorar métricas efetivamente disponibilizadas pelo equipamento.

## Alertas

Evitar alert storm.

Usar:

- dependências;
- correlação;
- consecutive failures;
- recovery;
- causa provável.

## Impressoras

Quando disponível via SNMP:

- toner;
- drum;
- papel;
- contadores;
- erros;
- status.
