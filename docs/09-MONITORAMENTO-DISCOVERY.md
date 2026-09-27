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

## Ingestão de Connections (F3-T005b)

Decisões aprovadas: **gatilho automático no fim do run** (mesmo
`SaveChanges`; erro de ingestão não derruba o run) e **walk do ifName
local** nos providers Neighbor — LLDP/CDP trazem só o ifIndex local
(`local_port`/`local_if`), e `NetworkInterface` é casado por NOME.

```text
Providers LLDP/CDP (F4-T005/T006)
  → após a tabela de vizinhos: walk tolerante do ifName
    (IF-MIB 1.3.6.1.2.1.31.1.1.1.1) via IfNameLookup
  → atributo local_if_name (falha → dicionário vazio → sem atributo)

StartDiscoveryCommandHandler (após gravar achados)
  → candidatos = achados Kind=Neighbor, Succeeded, DeviceId ≠ null
    (F4-T010 já casou o vizinho no inventário)
  → NeighborConnectionImporter.ImportAsync(device do run, candidatos):
      local   = (device do run, local_if_name)
      vizinho = (DeviceId do achado, port_desc ?? port_id ?? device_port)
      ambos casam NetworkInterface por nome → Connection dirigida
      self-loop / ponta ausente / par já existente → pula
      erro por candidato → log Warning e continua
  → RunComplete + 1 SaveChanges persiste achados e conexões
```

Sem `SaveChanges` próprio no importer — o run é quem persiste (mesmo
DbContext scoped). Cada provider ganhou `IfNameTableOid` público; o
lookup compartilhado fica em `Infrastructure/Discovery/IfNameLookup.cs`.

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

## Check TCP (F5-T006)

Escopo aprovado: **porta fixa 37777** (default de câmera) e o perfil
default de `DeviceType.Camera` passou a incluir o check
(ADR-014: mudança de default exige release).

```text
RunCheckCommand(check: "Tcp")   // CheckKind.Tcp = 5
  → TcpMonitoringProvider (Name Tcp, porta 37777, timeout 2s)
  → conecta {ip}:37777 via ITcpProbeClient
  → handshake completo → sucesso, Values: port=37777
  → recusa/timeout → "Porta 37777 inacessível em {ip}."
      Values: tcp_error (detalhe)
  → exceção do seam → "Falha ao consultar TCP: {msg}"
```

Seam: `ITcpProbeClient` + `TcpProbeReply(Success, Error?)`; implementação
real `SystemNetTcpProbeClient` (TcpClient.ConnectAsync, timeout por
`CancellationToken`). Testes usam `FakeTcpProbeClient` (sem rede).

## Polling periódico (F5-T007)

Decisões aprovadas: **serviço + worker fino** (padrão `ICleanupService`)
e **honrar os checks habilitados** por device (não `RunAllChecks` por
kind — esse roda o check em todos os devices e quebra com
`SnmpToner`/`Drum` fora de impressora).

```text
MonitoringPollingWorker (InfraControl.Workers, BackgroundService)
  → config MonitoringPolling:Enabled (default true)
             MonitoringPolling:IntervalSeconds (default 60)
  → a cada ciclo: scope → IMonitoringPollingService.PollOnceAsync

MonitoringPollingService (Application, testável sem host)
  → devices com IP (GetAllOrderedByNameAsync)
  + checks habilitados (IMonitoringCheckRepository.GetEnabledAsync)
  → por device/check: RunCheckCommandHandler.ExecuteAsync
      (guard de impressora/provider ausente → log Warning + pula;
       erro inesperado → log Error + continua — o ciclo nunca aborta)
  → 1 SaveChanges por ciclo (mesma decisão do RunAll)
  → retorna nº de checks executados (log do worker)
```

Config (appsettings do Workers): seção `MonitoringPolling`. Sem
auditoria por execução (worker não passa pelo pipeline MediatR —
histórico fica em `MonitoringResult`, F5-T002).

## Alertas

### F6-T001 — criação/resolve pós-check (concluída)

Avaliação acontece **logo após gravar o MonitoringResult**, no mesmo
`SaveChanges` (compartilhada por RunCheck, RunAll e polling F5-T007);
falha do avaliador vira Warning e não derruba o check.

Regras e thresholds **fixos em código** (`AlertThresholds` — decisão do
usuário; generalizar para perfil é F6-T002+ADR):

| Check | Condição | Severidade | Ação |
|---|---|---|---|
| Ping | streak de **3 falhas consecutivas** (F6-T002) | Critical | abre `Open` |
| Ping | `Succeeded = true` | — | resolve o `Open` |
| SnmpToner | `levelPercent < 20` | Warning | abre `Open` |
| SnmpDrum | `levelPercent < 10` | Warning | abre `Open` |
| Toner/Drum ≥ mínimo | — | — | resolve o `Open` |
| Toner/Drum sem `Values` (erro SNMP) | nível desconhecido | — | não avalia |
| Demais kinds (SnmpSystem, HttpWan, Tcp) | — | — | sem regra ainda |

- **Anti alert-storm**: 1 alerta `Open` por (device, check) — repetição
  do problema não cria linha nova; histórico abre/fecha sem duplicar.
- Entidade `Alert` (docs/03); API `GET /api/v1/monitoring/alerts`
  (filtros `deviceId`, `status=open|resolved`, paginação) — criação é
  automática, não há POST de alerta.

### F6-T002 — Alert Rules (concluída)

Decisões do usuário: **Ping N=3 via histórico**; **dependência de
upstream fica para F6-T004 Correlation** (sem modelo hoje); **anti-storm
só o básico** (unicidade Open + streak).

- **Consecutive failures** (`AlertThresholds.PingFailureStreak = 3`):
  o alerta de down só abre com **3 falhas consecutivas** — N-1=2 do
  histórico (`GetRecentByDeviceAndKindAsync`, por device+kind, mais
  recente primeiro) + o resultado atual. Sucesso salvo no meio quebra a
  sequência; sucesso resolve o `Open` e a contagem recomeça. Streak é
  determinístico (lido do banco — sobrevive a restart, sem estado em
  memória). Toner/Drum continuam imediatos (estado, não falha).
- **Upstream**: pendência documentada para F6-T004 (Correlation).

### F6-T003 — Incident auto-aberto (concluída)

- Alert **Critical** (Ping down com streak) → cria **1 Incident Open por
  device** e vincula o alerta (N:N `incident_alerts`); Warning não abre.
- Alert órfão (aberto antes do T003) ganha vínculo na próxima avaliação;
  repetição da falha não duplica incidente nem alerta.
- Estados da UI (Open → Investigating → Identified → Monitoring →
  Resolved, transições livres ≠ atual); `Acknowledge` = Investigating;
  Resolved estampa/limpa `ResolvedAt`. Vínculos de exclusão: cascade.
- API `POST /api/v1/incidents` (manual), `POST {id}/acknowledge`,
  `PUT {id}/status`, `GET` lista/detalhe — policies HTTP `IncidentsRead`
  / `IncidentsManage` (achado: policies por permissão registradas em
  `PermissionPolicies`, docs/05).
- Fechamento do incidente quando os alerts resolvem = **F6-T006
  Recovery**; correlação entre alertas/incidentes = F6-T004.

### F6-T004 — Correlation / supressão topológica (concluída)

Decisões do usuário: **supressão topológica** (usa `Connections` do
F3-T005) e **todos os kinds** do downstream.

- **Gatilho**: BFS não-dirigido (visited, semântica de distância) pelas
  `Connection`s a partir dos vizinhos de D, si-mesmo excluído; suprime
  enquanto qualquer device alcançável tiver **Incident Open** (só
  Critical cria incidente — um toner Warning não suprime a rede).
  Implementação: grafo em memória (2 queries LINQ — Sqlite e PG, sem CTE
  recursiva) em `UpstreamCorrelationRepository`.
- **Efeito**: alerta de D (ping/toner/drum) nasce `Suppressed` ou
  `Open→Suppressed` (anti-storm preservado: 1 ativo por device+kind),
  é vinculado ao incidente raiz (`incident_alerts`, idempotente) e D
  **não abre incidente próprio**; normalização resolve `Suppressed`
  também; upstream sem incidente → `Reopen` + incidente próprio se
  Critical. Casca transitiva (core → switch → servidor): basta o root
  ter Incident Open — vizinhos intermédios não precisam de alerta.
- **Limitação documentada**: janela de corrida (downstream avaliado
  antes do root no mesmo ciclo) — a próxima avaliação converte
  Open→Suppressed; incidente já aberto não é mesclado (F6-T006 trata
  fechamento).
- `AlertStatus` ganha `Suppressed` (string(20), **sem migração**);
  `Alert.Suppress()`/`Reopen()` (Resolved é definitivo);
  `IAlertRepository.GetActiveAsync` (Open OU Suppressed);
  filtro de busca `suppressed` (400 pt-BR para entrada inválida).
- Testes: transições de domínio (6), repositório de correlação (8),
  avaliador (6 — supressão, flip, todos os kinds, resolve, liberação,
  gatilho só-incidente), queries (2), integração (3 — DI real + leitura
  pela query no pipeline).

### F6-T005 — Root Cause / causa provável (concluída)

- `Incident.RootCause` (string? 500, migração `AddIncidentRootCause`) +
  `SetRootCause` (trim, null limpa, >500 → 400 pt-BR, mesmo valor é
  no-op). Exposto no `IncidentDto`.
- `IRootCauseResolver` (`RootCauseResolver`): heurística fixa —
  (1) BFS do F6-T004 acha upstream com Incident Open → "Falha no
  upstream '{nome}'…"; (2) vizinho (1 hop via `GetNeighborDeviceIdsAsync`)
  com alerta de Ping **Open** (Suppressed é vítima, não conta) cujo
  alerta é **mais antigo** que o incidente → "Vizinho '{nome}' sem
  resposta — provável enlace ou switch intermediário."; (3) senão →
  "Falha local no device (energia, processo ou enlace direto).".
  Regra de tempo = sem culpar vítimas (raiz cai primeiro; vítimas
  depois).
- O avaliador seta a causa na criação do incidente e chama
  `RefreshAsync(deviceId)` ao final de **todo** ciclo — reavalia a
  causa do Incident Open do device (cobre a corrida em que upstream ou
  vizinho abrem depois; incidente manual sem device → null).
- `IConnectionRepository.GetNeighborDeviceIdsAsync` (grafo não-dirigido,
  si-mesmo excluído) + `IncidentRepository.GetOpenByDeviceAsync` com
  tracking (SetRootCause persiste).
- Testes: IncidentTests +4 (set/limpa/limite/no-op), RootCauseResolverTests
  (8 — local, upstream, vizinho antes, vítima depois, Suppressed não
  conta, sem device, refresh, no-op), avaliador +2 (causa local na
  abertura; corrida atualiza p/ upstream no ciclo seguinte), integração
  +1 (RootCause via SearchIncidentsQuery no pipeline).

### F6-T006+ — pendências

- recovery (F6-T006);
- correlação entre múltiplas raízes / upstream explícito em Device.

## Impressoras

Quando disponível via SNMP:

- toner;
- drum;
- papel;
- contadores;
- erros;
- status.
