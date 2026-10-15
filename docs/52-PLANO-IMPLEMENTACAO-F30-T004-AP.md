# Plano de Execução — F30-T004: Access Point Wi-Fi (implementação)

> **Status**: S0 (reparo da fusão) ✅ CONCLUÍDO 2026-10-15 (commit `c4fbbef`) —
> S1 a S4 aguardam autorização.
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

### S3 — Exposição na UI (áreas A/B/E)

- **Objetivo**: o técnico vê rádio/estado do AP na tela do AP/WiFi.
- **Escopo**: resultados de `SnmpRadio` (e `SnmpHealth`, se habilitado) no
  detalhe do device + `system-info` (S3 do T001 já genérico) para
  uptime/`sysDescr` — reaproveitar o padrão da faixa do SwitchesPage (S5b/S5c
  do docs/51); WiFiPage ganha bloco de agregados do S1 se S1 não bastar.
- **Testes**: TDD Vitest (RED na coluna/faixa que falta → GREEN); `tsc`/
  `eslint`/suíte verdes.
- **Depende**: S1, S2.

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

## 8. Fora de escopo desta execução

- Operações de escrita em AP (reiniciar, ajustar canal/SSID, bloquear
  cliente) — ficha §"Operações futuras".
- Vendor-specific (Ruckus/Aruba/TP-Link) e histórico/prevenção sem ADR.
- Qualquer tabela nova / telemetria persistida (REGRA 8, ADR-028 §10).
