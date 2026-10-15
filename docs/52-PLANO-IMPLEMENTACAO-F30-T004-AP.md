# Plano de Execução — F30-T004: Access Point Wi-Fi (implementação)

> **Status**: S0 (reparo da fusão) ✅ 2026-10-15 (commit `743d010`) ·
> S1 ✅ (commit `b2a912a`) · S2 ✅ (commit `94de61b`) · S3 ✅ CONCLUÍDO
> 2026-10-15 (aguarda commit) — S4 (exige ADR) e S1b aguardam decisão.
> **Ficha**: `docs/45-FICHA-TECNICA-ACCESS-POINT.md` (planejamento, 2026-10-13).
> **Task**: F30-T004 — a ficha é só plano; este documento fatia a **implementação**.
> **Espelha**: `docs/51-PLANO-IMPLEMENTACAO-F30-T001-SWITCH.md` (mesmo formato,
> mesmo padrão de slice: TDD RED→GREEN, semântica de ausência `—`, opt-in por
> device, `DeviceProfileDefaults` intocado).

---

## 1. Ficha vs. código real (auditoria 2026-10-15)

Ambiente: **100% UniFi** (ficha §Visão Geral), controller via F18.

| Área da ficha | Item | Estado real (2026-10-15) |
|---|---|---|
| A | Nome/Vendor/Modelo/SN/IP/Local/MAC | ✅ `Device` (System of Record) |
| A | Firmware/Uptime sob demanda | ✅ `GET /devices/{id}/system-info` (F30-T001 S3 — genérico, serve ao AP) |
| B | Online/Offline | ✅ Ping (`DeviceProfileDefaults[AccessPoint] = [Ping, HttpWan]`) |
| B | Estado do AP (Connected/…), uptime, CPU/mem | ✅ `UnifiAccessPointDto` via `GET /integrations/unifi/wifi` (cache 60s) — **já existe** |
| B | Estado do rádio | ✅ `UnifiRadioDto` (band/channel/width/txPower/utilization) — **já existe** |
| C | Clientes ativos + lista (MAC/IP/SSID/RSSI) | ✅ `GET /integrations/unifi/clients` (`ap_mac`, `essid`, `signal`) — **já existe** |
| C | **Clientes por rádio / por SSID; RSSI médio** | ❌ Não modelado — **derivável** do payload de clientes (front, S1) |
| D | SSIDs configurados | ✅ `ListSsidsAsync` (`/rest/ssidconf`, F18-T006) |
| D | Canais/largura por rádio | ✅ `UnifiRadioDto` |
| D | BSSID | ⚠️ Não exibido — pendência |
| E | Utilização de rádio | ✅ `UnifiRadioDto.Utilization` |
| E | Interferência / ruído de fundo / taxa de erro | ❌ Endpoint novo de controller (`/stat/health`) — **pendência §7** |
| E | RSSI (via IEEE 802.11 MIB, fallback SNMP) | ❌ **0 código** — `dot11*` só existe na ficha → **S2** |
| F | Tráfego | ✅ F29-3 `SnmpInterfaces` (ifTable/ifXTable) |
| F | PoE status/potência | ❌ Não modelado — pendência §7 (AP não é fonte do PoE do próprio AP sem OID mapeado) |
| G | Histórico/prevenção (tendência, limiares 80/90%) | ❌ Modelo não existe — **exige ADR** → S4 |

**Consequência**: a prioridade 1 da ficha ("estender UniFi") está em grande
parte **já construída** (F18 + WiFiPage). O código realmente novo é SNMP
rádio/fallback (S2) e agregados de tela (S1).

---

## 2. Decisões já tomadas (não reabrir)

- **S0 — reparo da fusão (2026-10-15, autorizado)**:
  - `CheckKind`: vírgula ausente pós-`SnmpPrinterCounters = 9` (CS1003, repo
    não compilava) + `<summary>` restaurados + **`SnmpHealth` 7 → 10** (colidia
    com `SnmpColorToner = 7` vindou da outra sessão: o `ToString()` virava
    "SnmpColorToner", o `Name` do provider errava e o guard Printer-only
    disparava para Switch). Seguro: `kind` é persistido como **string**
    (`HasConversion<string>` nas duas tabelas) e nenhum teste afirma valor
    numérico do enum — sem migração, sem dado perdido.
  - `DependencyInjection.cs`: conflito resolvido **mantendo os registros ativos**
    de `SnmpColorToner/PrinterStatus/PrinterCounters` (as classes existem — o
    comentário "ACHADO: providers inexistentes" era verdadeiro no mundo
    pré-fusão e ficou obsoleto). Registro de `SnmpHealth` preservado.
  - `PROJECT_STATE.md`: 3 marcadores de conflito removidos **mantendo o
    conteúdo** do stash (seção Fase 13 + notas — verificado: único no HEAD,
    não duplicava).
  - `tsconfig.tsbuildinfo`: conflito UU limpo via `git checkout HEAD` —
    artefato gerado pelo `tsc -b`, **nunca stageado**.
- **Opt-in por device** (padrão F30-T001/T002): nenhum check novo entra em
  `DeviceProfileDefaults` — o default do AccessPoint segue `[Ping, HttpWan]`.
- **Sem `SnmpApHealthProvider` terceiro** (prioridade 3 da ficha): consolidado
  — CPU/mem/temperatura por vendor já vem do `SnmpHealth` (F30-T001, funciona
  em UAP UniFi), `ifOperStatus` agregado já vem do `SnmpInterfaces` (F29-3);
  o que sobra de "estado do rádio" (dot11) entra no `SnmpRadioProvider` (S2).
  **Reabrir só com pedido explícito** (§7 item 1).
- **Ausência = `—`/null, nada inventado** (herança F30-T001): OID não
  respondido vira chave ausente; resultado falho não vira 0.
- **Persistência**: telemetria de massa (rádios, clientes, utilização) continua
  **sob demanda com cache 60s** (REGRA 8 / ADR-028 §10) — o histórico numérico
  é do Zabbix. Nenhuma tabela nova neste plano.
- **Informativo sem alerta**: `SnmpRadio` cai no `default` do `AlertEvaluator`
  (mesmo destino de `SnmpHealth`/`SnmpInterfaces`) — limiares de rádio são
  prevenção e exigem ADR (S4).

---

## 3. Precisa de ADR?

- **S1, S2, S3: não.** Seguem ADR-014 (profile), ADR-028 §10 (sob demanda),
  padrão F30-T001/T002 de check novo.
- **S4 (prevenção/limiares): sim** — decide retenção, janela de tendência,
  severidade (ficha: >80% Warning, >90% Critical; RSSI < −70 dBm) e quem
  avalia (AlertEvaluator vs consulta de histórico Zabbix). Mesmo precedente
  do S6 do docs/51.

---

## 4. Fatiamento

### S0 — Reparo da fusão (pré-requisito) — ✅ executado 2026-10-15

Conforme §2. Critério: `dotnet build` 0 erros, marcadores de conflito = 0 no
repo, suíte completa verde, commit próprio (autorizado).

### S1 — Agregados UniFi na tela (áreas C/E, prioridade 1 da ficha)

- **Objetivo**: fechar as lacunas deriváveis do payload **já existente**:
  clientes por rádio (2.4/5 GHz), clientes por SSID e **RSSI médio** — tudo a
  partir de `GET /integrations/unifi/clients` (`ap_mac`, `essid`, `signal`).
- **Escopo**: front puro (`WiFiPage` + helpers de agregação), sem backend novo.
- **Testes**: TDD Vitest — agregadores puros (fixtures de clientes) +
  renderização; `tsc` 0, `eslint` 0.
- **Critério de aceite**: a tabela/painel mostra as 3 métricas com `—` quando
  não há clientes; nenhuma chamada de rede nova.
- **Depende**: nada (dados já no endpoint).
- **Executado 2026-10-15** (RED: import inexistente + 4 testes de página →
  GREEN 21/21; `tsc` 0, `eslint` 0): `src/utils/wifiClients.ts`
  (`summarizeLiveClients`/`formatSignal`) + painel "Clientes ao vivo" na
  WiFiPage (sem tabela — logo sem seletor de visão; label+valor no mesmo
  text node).
- **Desvio registrado**: "clientes por rádio (2.4/5 GHz)" **não é derivável**
  — a banda não existe em nenhum ponto da cadeia (o parser backend não lê
  `radio` do `stat/sta`). Entregue o derivável sem backend: total, fio/sem-fio,
  clientes por SSID e RSSI médio. Banda por cliente vira pendência §7 item 6
  (candidata a S1b: 1 campo no DTO + parser).

### S2 — `SnmpRadioProvider` (IEEE 802.11 MIB, área E/D — fallback SNMP)

- **Objetivo**: rádio/SSID/canal/RSSI via SNMP para APs fora do controller
  (ficha prioridade 2).
- **Escopo**: `CheckKind.SnmpRadio = 11` (próximo livre pós-S0);
  `SnmpRadioProvider : IMonitoringProvider` com walk
  `1.3.6.1.2.1.80.1.1.3` (`dot11NumberOfMobileStations`, `dot11DesiredSSID`,
  `dot11CurrentChannel`, `dot11CurrentTxPowerLevel`, `dot11RSSI`,
  `dot11PrivacyOptionImplemented` — OIDs da ficha §C/D/E); guard
  **AccessPoint-only** em `RunCheckCommands` (mesmo molde do guard Printer);
  DI `AddScoped<IMonitoringProvider, …>`; **sem** regra no `AlertEvaluator`;
  `DeviceProfileDefaults` intocado.
- **Testes**: TDD — provider (nome/keys, "só afirma o que respondeu",
  walk vazio ⇒ falha pt-BR, empty target, SNMP throw, OID order) + guard
  (Switch/Printer ⇒ `ArgumentException`) + sem default do profile.
- **Critério de aceite**: UnitTests verdes; opt-in documentado.
- **Depende**: S0.

#### Execução do S2 (2026-10-15)

- **TDD**: RED confirmado antes de qualquer implementação — build quebrado com
  `CS0117: "CheckKind" não contém uma definição para "SnmpRadio"` (+ tipo
  `SnmpRadioProvider` inexistente) → GREEN 10/10 na rodada filtrada
  (7 provider + 2 guard + 1 profile); 11/11 após completar o plano
  (guard vira `[Theory]` Switch/Printer).
- **Entregue**:
  - `CheckKind.SnmpRadio = 11` (doc comment com semântica e gate);
  - `src/InfraControl.Infrastructure/Monitoring/SnmpRadioProvider.cs` (novo):
    walk única na raiz `1.3.6.1.2.1.80.1.1.3`; colunas da ficha §C/D/E
    (1 estações, 2 SSID, 4 canal, 5 potência TX, 6 RSSI, 7 privacidade)
    parseadas como `<coluna>.<ifIndex>` → chaves `<métrica>.<ifIndex>` +
    `radioCount`. Só afirma o que respondeu: coluna ausente, valor em branco,
    coluna desconhecida, ifIndex não numérico e OID fora da raiz ficam de
    fora — **nunca 0 inventado**; walk vazio ou só lixo = falha honesta
    pt-BR com `Values = null`. Chave **por rádio** (não agregada) porque
    ssid/canal/potência não são agregáveis — agregar os dois rádios (2.4/5)
    inventaria dado.
  - guard **AccessPoint-only** em `RunCheckCommands` (molde exato do guard
    Printer, mensagem pt-BR "só se aplica a devices do tipo AccessPoint."),
    coberto por `[Theory]` (Switch e Printer ⇒ `ArgumentException`, linhas
    não persistem);
  - DI `AddScoped<IMonitoringProvider, SnmpRadioProvider>` (auto-registro
    F0-T024) logo após o `SnmpHealthMonitoringProvider`;
  - testes: `SnmpRadioProviderTests` (7: nome/keys, chaves por rádio +
    `radioCount` + ausência honesta, filtro de lixo com conjunto exato de
    keys, walk vazio, só lixo, SNMP throw, empty target) + 3 guard em
    `RunCheckCommandTests` (2 não-AP + 1 AP executa e persiste `Kind`) +
    1 guarda de regressão `Defaults_ShouldNeverIncludeSnmpRadio_OptInOnly`
    em `DeviceProfileTests` (nasce verde — protege o opt-in).
- **Intocado (como planejado)**: `DeviceProfileDefaults` (AccessPoint segue
  `[Ping, HttpWan]`, já assertado pelo `[InlineData]` do ADR-014);
  `AlertEvaluator` sem regra nova (default do switch); sem catálogo de
  checks (confirmado por busca: `SnmpInterfaces` só vive em enum + DI +
  provider — não há lista a registrar).
- **Desvio**: nenhum material. O plano pedia teste "OID order" — a resposta
  é dicionário sem ordem de contrato; o teste de lixo asserta o **conjunto
  exato** de keys (mais forte que ordem).
- **Validação**: filtrado **11/11** (7 provider + 3 guard + 1 profile) ·
  UnitTests completas **2503/2503** (base 2492 + 11 do S2) · Arch
  **152/153** (falha conhecida `Nginx_Should_Proxy_The_Api_Health_Checks`,
  pré-existente) · `tsc`/`eslint` intocados (slice só de back).

### S3 — Exposição na UI (áreas A/B/E)

- **Objetivo**: o técnico vê rádio/estado do AP na tela do AP/WiFi.
- **Escopo**: resultados de `SnmpRadio` (e `SnmpHealth`, se habilitado) no
  detalhe do device + `system-info` (S3 do T001 já genérico) para
  uptime/`sysDescr` — reaproveitar o padrão da faixa do SwitchesPage (S5b/S5c
  do docs/51); WiFiPage ganha bloco de agregados do S1 se S1 não bastar.
- **Testes**: TDD Vitest (RED na coluna/faixa que falta → GREEN); `tsc`/
  `eslint`/suíte verdes.
- **Depende**: S1, S2.

#### Execução do S3 (2026-10-15)

- **TDD**: RED confirmado — 2 testes da faixa ("Uptime: 1d 1h" e
  "Uptime: —") falhando por texto ausente → GREEN 4/4.
- **Entregue** (`DeviceDetailPage.tsx` + `DeviceDetailPage.test.tsx` novo):
  - faixa `system-info` na barra de identificação do detalhe do device —
    `useObjectQuery(["deviceSystemInfo", id], getDeviceSystemInfo)`,
    **genérica para QUALQUER tipo** (endpoint S3 do T001 já genérico);
    label+valor no mesmo text node (`Uptime: 1d 1h` via `formatUptime` ·
    `Sistema: <sysDescr cru>`), "—" onde nunca houve leitura, faixa some
    em pending/erro — mesmo molde da faixa S5b da SwitchesPage;
  - 4 testes: faixa com valores, dashes honestos, some em erro (guard) e
    poll de `SnmpRadio` com chaves por rádio na coluna "Detalhes" (guard
    nasce verde — a coluna é genérica e já renderizava qualquer `values`).
- **Confirmado sem código**: `SnmpRadio`/`SnmpHealth` já aparecem no
  "Últimos polls" do detalhe (coluna Detalhes genérica) — a exigência do
  escopo está coberta pelo contrato; WiFiPage não mudou (S1 bastou:
  telemetria de rádios do controller + agregados ao vivo entregues).
- **Ajustes de teste no caminho** (ambos documentados): clique na aba
  "Monitoramento" precisou de espera (`findByRole` antes — o click
  síncrono caía no "Carregando…", a página usa loader manual) e o render
  ganhou `QueryClientProvider` (a página passou a usar `useObjectQuery`).
- **Validação**: filtrado **4/4** · `tsc` **0** · `eslint` **0** (1 warning
  pré-existente PrintersPage) · suíte completa com 9 falhas de timeout em
  6 arquivos (SwitchesPage/PdiAccompanimentDialog/TasksPage/InvitationsPage/
  DevicesPage/AccountPage — **0 asserções**, durações 13-20s, mesmo flake
  de carga registrado no S0/S1; nenhum arquivo do S3) e **36/36 verdes**
  quando os 6 rodam isolados.

### S4 — Prevenção (área G) — **exige ADR antes**

- Limiares de saturação (80/90%), tendência de RSSI, densidade de clientes;
  histórico via Zabbix (REGRA 8). **Não começa sem ADR aprovado** — igual ao
  S6 do docs/51.

---

## 5. Sequência

```text
S0 (reparo) → commit verde → docs/52 (este doc) → autorização
→ S1 (agregados, front) → S2 (SnmpRadioProvider, back) → S3 (UI)
→ [ADR] → S4 (prevenção)
```

## 6. Critérios globais (valem para todos os slices)

- TDD obrigatório: teste primeiro, RED confirmado, GREEN depois.
- **Gate de verde** (mesmo das execuções F30-T001/S5):
  - `dotnet test` → **UnitTests** 100% + **ArchitectureTests** 152/153
    (a única falha é a conhecida `Nginx_Should_Proxy_The_Api_Health_Checks`,
    pré-existente);
  - front: `vitest run` 100% + `tsc -b` 0 + `eslint .` 0.
  - `IntegrationTests` **fica fora do gate**: usa Testcontainers/Docker
    (Postgres + RabbitMQ) e só verde com a stack no ar — executar sob demanda
    quando o usuário tiver o Docker ativo (registrado como pendência).
  - Atenção operacional: rodar back e front **em paralelo** na mesma máquina
    derruba testes por timeout (52× `testTimeout` 5000ms numa execução de
    2026-10-15; a mesma suíte verde isolada) — rodar sequencialmente.
- Commit por autorização; `tsconfig.tsbuildinfo` **nunca** stageado; push nunca
  automático.
- Migração (se algum slice criar tabela — **nenhum previsto**): `dotnet ef
  migrations add` **sem** `--no-build` + validação por `migrations script`.

## 7. Pendências de decisão

1. **`SnmpApHealthProvider`** (prioridade 3 da ficha): consolidado em
   `SnmpHealth`+`SnmpInterfaces`+`SnmpRadio` (§2) — reabrir só com pedido.
2. **Interferência / ruído / taxa de erro** (ficha §E): exigem endpoint de
   controller que não usamos (`/stat/health`) — decidir se o UniFi deste
   ambiente expõe; fora do escopo atual.
3. **PoE status/potência** (fichas §B/§F): AP é *alimentado* por PoE; o dado
   "de quem alimenta" é do switch (porta) — definir fonte antes de modelar.
4. **BSSID** (ficha §D): `ifPhysAddress` por SSID não está mapeado — baixa
   prioridade, registrar se o operador pedir.
5. **Estado do AP "Ready/Upgrading/Isolated"** (ficha §B): o parse atual lê
   `state == 1 → Connected` só; granularidade extra exige contrato novo.
6. **Banda (2.4/5 GHz) por cliente** (ficha §C): o `stat/sta` do controller
   traz `radio` por cliente, mas o `UnifiConnectedClientDto` não lê esse
   campo — exige 1 propriedade no DTO + 1 linha no parser (backend). O S1
   entregou o derivável sem backend; banda = candidata a **S1b**.

## 8. Fora de escopo desta execução

- Operações de escrita em AP (reiniciar, ajustar canal/SSID, bloquear
  cliente) — ficha §"Operações futuras".
- Vendor-specific (Ruckus/Aruba/TP-Link) e histórico/prevenção sem ADR.
- Qualquer tabela nova / telemetria persistida (REGRA 8, ADR-028 §10).
