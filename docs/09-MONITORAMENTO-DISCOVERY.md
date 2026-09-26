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

## DiscoveryResult (F4-T002)

Achado de uma execução de discovery — modelo genérico aprovado
(**Kind + Summary + JSON**) para servir todos os providers sem nova
migração:

| Campo | Tipo | Observação |
|---|---|---|
| `DiscoveryRunId` | uuid | FK → `discovery_runs` **cascade** |
| `Kind` | enum string | `Neighbor`, `ArpEntry`, `MacTableEntry`, `DhcpLease`, `Host` |
| `Summary` | ≤500 | texto legível do achado |
| `Values` | JSON | detalhes livres do provider |
| `DeviceId` | uuid? | vínculo opcional (T010) — `SetNull` |
| `CreatedAt` | timestamptz | ordenação (DESC) |

Endpoints (leitura; escrita vem com os providers F4-T003+), policy
`MonitoringRead`:

```text
GET api/v1/discovery/runs/{runId}/results?page&pageSize
GET api/v1/discovery/results/{id}
```

Run desconhecido → 404 (`KeyNotFoundException`).

## Provider ICMP e gatilho (F4-T003)

```text
POST api/v1/discovery/devices/{deviceId}/runs
Body: { "provider": "Icmp" }
Roles: Admin,User (MonitoringManage) — AuthorizationBehavior é a 2ª barreira
```

Fluxo: valida device (404) → resolve provider (400 pt-BR se não
registrado) → exige IP (400) → cria run → `IcmpDiscoveryProvider` sonda
o IP pela seam `IIcmpClient` (sem rede real nos testes) → grava
`DiscoveryResult(Kind=Host, Values: reachable/roundtrip_ms)` → `Complete`.

Decisões do usuário: **ping falho = achado negativo** (run `Completed` +
`reachable=false`); só erro de infraestrutura (exceção do probe) marca o
run `Failed` e devolve o DTO com `errorMessage`.

> Nota ADR-008: autorização de controller é por **Roles**
> (`[Authorize(Roles=...)]`); claims de permissão ficam no pipeline
> MediatR. Os controllers de discovery usavam `Policy="MonitoringRead"`
> inexistente (bug do F4-T001) — corrigido no F4-T003.
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
