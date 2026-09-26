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

## Provider SNMP (F4-T004)

Escopo aprovado: **só o system group MIB-II** (sem ifTable).

```text
StartDiscoveryCommand(provider: "Snmp")
  → walk 1.3.6.1.2.1.1 via ISnmpClient (mesma seam do monitoring)
  → 1 DiscoveryResult(Kind=Host) com sys_descr/sys_name/sys_up_time/
    sys_location/sys_contact + reachable=true
  → Summary "SNMP: <sys_name> — <sys_descr>" (≤500)
```

Sem resposta ou exceção → achado negativo (`reachable=false`, erro
pt-BR), run `Completed` (mesma decisão do ICMP).

## Provider LLDP (F4-T005)

Decisões do usuário: leitura via **SNMP/LLDP-MIB** (não SSH/CLI) e walk
sem vizinhos → **registrar "nenhum"** como achado.

```text
StartDiscoveryCommand(provider: "Lldp")
  → walk 1.0.8802.1.1.2.1.4.1.1 (lldpRemTable) via ISnmpClient
  → colunas agrupadas por índice (timeMark.localPort.remIndex)
  → 1 achado por vizinho: Kind=Neighbor, Summary "<sys_name> — <porta>",
    Values: sys_name/chassis_id/port_id/port_desc/sys_descr/local_port
  → walk vazio: 1 achado "Nenhum vizinho LLDP." (neighbors=0)
```

**Contrato** (evolução no F4-T005): `IDiscoveryProvider.DiscoverAsync`
retorna **lista** de `DiscoveryProviderResult(Kind, Succeeded,
Attributes, Error?, Summary?)` — ICMP/SNMP devolvem 1 achado
`Kind=Host`, LLDP N achados `Kind=Neighbor`. O comando grava todos em 1
`SaveChanges`; `DeviceId` do achado só é preenchido para `Host` (o
próprio device) — vizinhos ficam sem vínculo até o **T010** casar.

## Provider CDP (F4-T006)

Mesmo padrão do LLDP (F4-T005), CISCO-CDP-MIB:

```text
StartDiscoveryCommand(provider: "Cdp")
  → walk 1.3.6.1.4.1.9.9.23.1.2.1.1 (cdpCacheTable) via ISnmpClient
  → colunas .4 address / .5 version / .6 device_id / .7 device_port
    agrupadas por índice (ifIndex.deviceIndex)
  → 1 achado por vizinho: Kind=Neighbor, Summary "<device_id> — <porta>"
  → walk vazio: 1 achado "Nenhum vizinho CDP." (neighbors=0)
```

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
