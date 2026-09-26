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

## Provider ARP (F4-T007)

Decisões aprovadas: **1 achado por entrada** e **sem filtro de tipo**.

```text
StartDiscoveryCommand(provider: "Arp")
  → walk 1.3.6.1.2.1.4.22.1 (ipNetToMediaTable, IP-MIB)
  → colunas .1 ifIndex / .2 physAddress / .3 netAddress / .4 type
    agrupadas por índice ifIndex.a.b.c.d
  → 1 achado por entrada: Kind=ArpEntry, Summary "<ip> — <mac>",
    Values: ip/mac/if_index/arp_type/local_if/arp_entries
  → tabela vazia: 1 achado "Nenhuma entrada ARP." (arp_entries=0)
```

## Provider DHCP (F4-T008)

Escopo aprovado: **bindings de DHCP snooping** (vendor Cisco, mesmo
natureza do CDP). MIB: CISCO-DHCP-SNOOPING-MIB.

```text
StartDiscoveryCommand(provider: "Dhcp")
  → walk 1.3.6.1.4.1.9.9.380.1.4.1 (cdsBindingsTable)
    via ISnmpClient
  → colunas .1 vlan / .2 mac / .3 addrType / .4 ip / .5 interface /
    .6 leasedTime / .7 status / .8 hostname,
    agrupadas por índice vlan + mac (octetos)
  → 1 achado por binding: Kind=DhcpLease, Summary "<ip> — <mac>",
    Values: ip/mac/vlan/addr_type/local_if/leased_time/binding_status/
    hostname/dhcp_bindings
  → binding sem ip: descartado (não identifica o lease)
  → tabela vazia: 1 achado "Nenhuma entrada DHCP." (dhcp_bindings=0)
```

## Provider MAC Tables (F4-T009)

Decisões aprovadas: **filtro só status=3 (learned)** e **chave "Mac"**.

```text
StartDiscoveryCommand(provider: "Mac")
  → walk 1.3.6.1.2.1.17.4.3 (dot1dTpFdbTable, BRIDGE-MIB RFC 4188)
    via ISnmpClient
  → colunas .1 address / .2 port / .3 status
    agrupadas por índice MAC (6 octetos)
  → 1 achado por entrada learned: Kind=MacTableEntry,
    Summary "<mac> — porta <port>",
    Values: mac/bridge_port/fdb_status/mac_entries
  → status != 3 (self/invalid/other): descartadas
  → tabela vazia ou só não-learned: 1 achado
    "Nenhuma entrada MAC." (mac_entries=0)
```

## New Device Detection (F4-T010)

Decisões aprovadas: **vincular + flag** (não cria device) e chaves
**ip > mac > nome**.

Após o probe, o `StartDiscoveryCommandHandler` casa cada achado
`Succeeded` não-Host com devices do inventário, na ordem de prioridade:

1. `Values.ip` → `IDeviceRepository.GetByIpAsync` (case-insensitive);
2. `Values.mac` → `GetByMacAsync` (sem separador/caixa: `aa:bb:...` e
   `AA-BB-...` casam);
3. `Values.sys_name` (LLDP) / `device_id` (CDP) / `hostname` (DHCP) →
   `GetByNameAsync` (case-insensitive).

Resultado:

- achado casado → `DiscoveryResult.DeviceId` preenchido;
- achado `Succeeded` sem match → `Values["new_device"]="true"`
  (candidato a novo device, sem cadastro automático);
- achado negativo ("Nenhuma entrada…") → sem vínculo e **sem** flag;
- achado `Host` → `DeviceId` = próprio device descoberto (inalterado).

Consultas por chave ficam em cache no handler (ip/mac/nome repetidos
não re-consultam o banco).

> Nota ADR-008: autorização de controller é por **Roles**
> (`[Authorize(Roles=...)]`); claims de permissão ficam no pipeline
> MediatR. Os controllers de discovery usavam `Policy="MonitoringRead"`
> inexistente (bug do F4-T001) — corrigido no F4-T003.
- CPU
- Memory
- Temperature

Somente monitorar métricas efetivamente disponibilizadas pelo equipamento.

## Check HTTP/HTTPS (F5-T005)

Decisões aprovadas: **HTTPS com fallback HTTP**, **qualquer resposta
HTTP recebida = sucesso** (erro de rede/timeout = falha) e **validação
do cert TLS** (self-signed = falha do esquema HTTPS → tenta HTTP).

```text
RunCheckCommand(check: "HttpWan")   // CheckKind.HttpWan
  → HttpWanMonitoringProvider (Name HttpWan, timeout 5s)
  → sonda https://{ip}/ via IHttpProbeClient
      IPv6: https://[2001:db8::1]/
  → resposta HTTP (qualquer status) → sucesso
      Values: url / scheme / status_code
  → falha → fallback http://{ip}/ (mesma regra)
  → ambos falham → erro "HTTP indisponível em {ip}."
      Values: https_error / http_error
  → exceção do seam → "Falha ao consultar HTTP: {msg}" (sem fallback)
```

Seam: `IHttpProbeClient` + `HttpProbeReply(Success, StatusCode, Error?)`;
implementação real `SystemNetHttpProbeClient` (HttpClient estático,
`ResponseHeadersRead`, validação TLS padrão do .NET; timeout por
`CancellationToken`). Testes usam `FakeHttpProbeClient` (sem rede).

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
