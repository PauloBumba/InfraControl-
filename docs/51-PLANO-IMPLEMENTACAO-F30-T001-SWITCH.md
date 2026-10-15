# Plano de Execução — F30-T001: Switch (implementação)

> **Status**: Em execução — S1 + S2 + S3 (2026-10-14) + S4a e S5 (a-c) (2026-10-15) concluídos
> **Base**: `docs/42-FICHA-TECNICA-SWITCH.md` (ficha, Rascunho) confrontada com o
> código real do produto.
> **Regras**: TDD (teste antes, RED confirmado), sem `git push`, commit só com
> autorização, nenhuma tarefa grande executada sem fatiamento (`AGENTS.md`).

---

## 1. Ficha vs. código real

| O que a ficha desenha | O que já existe | Distância |
|---|---|---|
| Entidade `SwitchHealth` (§C L200-213) | `MonitoringResult` — histórico por poll com `Values` + timestamp | **Nenhuma** — não criar entidade nova |
| `ISnmpHealthProvider` + factory por vendor (§Estratégia L443) | `IMonitoringProvider` + `ProviderRegistry.Resolve` + `ISnmpClient` | **Nenhuma** — ver §2 |
| Histórico de saúde com tendência | `MonitoringResult` (`DeviceId`, `Kind`, `Succeeded`, `DurationMs`, `Error`, `Values`) | Só a consulta agregada |
| Limiar/duração/tendência/recorrência (§F) | `AlertThresholds` estático + `AlertEvaluator` (PingFailureStreak, TonerMinPercent, DrumMinPercent) | **Gap real** — hardcoded, sem tendência nem duração |
| `Firmware`/`Uptime` no cadastro (§A) | `sysUpTime` lido hoje pelo `SnmpSystemMonitoringProvider`, só no `Values`; `Firmware` existe só em DTO UniFi (`UnifiDeviceDto`) | Não persistido em `Device` |
| `SwitchPort` Velocidade/Duplex/PoE (§B) | `SwitchPort` = `DeviceId`, `Name`, `Description`, `LinkUp` | 5 campos ausentes |
| Topologia (§D) | F29-5 ghost nodes + LLDP/CDP ingestion | **Completa** |
| Gestão SSH (§E) | — | **Fora de escopo** (decisão da ficha) |
| Defaults do tipo | `DeviceProfileDefaults[Switch] = [Ping, SnmpSystem]` | Check novo entra opcional |

---

## 2. Decisões já tomadas (não reabrir)

1. **Usar `IMonitoringProvider` existente**, não criar `ISnmpHealthProvider`.
   Evidência nova: das 8 fichas da F30, **só a do Switch nomeia a seam nova** —
   as outras 7 já propõem `SnmpXxxProvider`, o padrão do código.
2. **O provider recebe o vendor e escolhe os OIDs.** `CheckAsync(target)` não
   muda: o provider injeta `IDeviceRepository` e usa `GetByIpAsync` (porta que
   já existe, usada por `StartDiscoveryCommands` e `ZabbixInventoryCommands`).
3. **Saúde SNMP primeiro**, sem migração de banco nesta etapa.

---

## 3. Precisa de ADR?

**Não** para S1/S2/S4: adicionar `CheckKind` + provider + campos é exatamente
o que F29-3 fez com `SnmpInterfaces` (`CheckKind = 6`). É a arquitetura
existente, não uma nova.

**Sim** para **S6 (prevenção)**: modelo de limiar/duração/tendência/recorrência/
impacto/confiança é capacidade nova de avaliação e muda como `AlertEvaluator`
funciona. `AGENTS.md`: não alterar arquitetura sem ADR.

---

## 4. Fatiamento

### S1 — Seam de saúde SNMP (área C)

> **✅ CONCLUÍDO 2026-10-14** — 13 testes novos RED→GREEN.
> Evidência: `SnmpHealthProviderTests` 13/13 · UnitTests **2339/2339** ·
> ArchitectureTests 131/132 (a única falha é a pré-existente
> `Nginx_Should_Proxy_The_Api_Health_Checks`, ADR-044).

- **Objetivo**: um switch cadastrado, SNMP respondendo ⇒ o poll grava
  `MonitoringResult` com CPU/memória/temperatura lidos, sem valor inventado.
- **Escopo**:
  - `CheckKind.SnmpHealth = 7` (documentado como opt-in, como `SnmpInterfaces`)
  - `SnmpHealthMonitoringProvider : IMonitoringProvider` (padrão de
    `SnmpInterfacesMonitoringProvider`)
  - Resolução de vendor por `IDeviceRepository.GetByIpAsync(target)`
  - OIDs dos 2 primeiros da prioridade da ficha: **UniFi** e **Intelbras**
    (§C L170-172)
  - Registro no `DependencyInjection` ao lado dos outros 4 providers
  - **Não** entra em `DeviceProfileDefaults`
- **Fora**: vendors restantes (S2), histórico agregado, UI, coluna `Firmware`.
- **Testes**: `tests/InfraControl.UnitTests/Monitoring/SnmpHealthProviderTests.cs`
  (novo), espelhando `SnmpInterfacesProviderTests`, com `ISnmpClient` fake e
  `IDeviceRepository` via Moq (já no csproj):
  1. `Name`/`CanHandle` batem com o `CheckKind`
  2. vendor UniFi ⇒ OIDs corretos percorridos, `Values` com `cpuPercent` etc.
  3. vendor Intelbras ⇒ OIDs próprios (não os da UniFi)
  4. vendor desconhecido ⇒ `Succeeded = false`, **`Values` sem chaves**
  5. device sem IP / IP não cadastrado ⇒ erro claro, sem exceção vazada
  6. métrica ausente na resposta ⇒ **chave omitida**, nunca `0`
- **Critério de aceite**: 6 casos verdes; `dotnet test` completo verde.
- **Evidência**: RED confirmado antes da implementação.

### S2 — Vendors restantes (HP, TP-Link, Aruba)

> **✅ CONCLUÍDO 2026-10-14** — 12 testes novos RED→GREEN (25 no arquivo).
> Evidência: `SnmpHealthProviderTests` 25/25 · UnitTests **2351/2351** ·
> ArchitectureTests 131/132 (só a pré-existente ADR-044).

- **Objetivo**: cobrir as 5 marcas levantadas na ficha.
- **Escopo**: tabela de OIDs por vendor no mesmo provider + testes 1 por vendor.
- **Critério de aceite**: 5 vendors com teste próprio; qualquer outro vendor ⇒
  erro claro "vendor não suportado para saúde SNMP: X".
- **Depende**: S1.

**Feito**: `HpOids`, `ArubaOids`, `TpLinkOids` + recusa com a **listagem dos 5
suportados**. O resolve passou a casar por **token inteiro** (`HP`, `HPE`,
`TP`) em vez de subcadeia — `Contains("hp")` solto cairia em qualquer nome
contendo o par.

**Comportamentos honestos travados em teste**:
- **Aruba não tem OID de temperatura** na tabela (docs/42 L174: "via MIBs de
  hardware") ⇒ a chave não existe, nunca `0`.
- **Temperatura TP-Link vem como OctetString** sem decodificação documentada ⇒
  omitida, não vira número inventado.
- **Fallback de candidato** (Aruba instant → 1m) com o walk registrado.

**Fora do escopo, registrado na §7**: fans e PSUs (só HP tem OID na ficha) —
exigiriam o mapeamento de enumeração documentado do fabricante; sem isso a
leitura seria um número/string sem significado garantido.

### S3 — Firmware/Uptime expostos (área A)

> **✅ CONCLUÍDO 2026-10-14** — 7 testes novos RED→GREEN.
> Evidência: `DeviceSystemInfoQueryTests` 7/7 · UnitTests **2358/2358** ·
> ArchitectureTests 131/132 (só a pré-existente ADR-044; as regras de
> rota/autorização do endpoint novo passaram).

- **Objetivo**: o detalhe do switch mostra firmware e uptime.
- **Escopo**: leitura a partir do histórico (`MonitoringResult` de
  `SnmpSystem`/`SnmpHealth`) no DTO do switch — **sem migração de banco**.
- **Testes**: handler de `GetSwitchQuery` devolve `Firmware`/`Uptime` a partir
  do último poll; sem histórico ⇒ `null` (não string vazia, não `0`).
- **Decisão pendente (ver §7)**: expor do histórico (recomendado) vs. coluna
  em `Device` (exigiria migração).
- **Depende**: de S1 para ter dado de saúde; independente para `Uptime`.

**Feito (e como divergiu do rascunho — decidido, não escondido):**

O rascunho dizia "no DTO do switch". `DeviceDto` é cadastro puro e o
`ToDto(device)` é **estático, sem acesso ao histórico** — estenderlo mudaria
todo chamador (create/update/list). Em vez disso: query nova
`GetDeviceSystemInfoQuery` (`BaseQuery` + `IAuthorizedRequest`, cache 30s) +
endpoint `GET /api/v1/inventory/devices/{id}/system-info`, irmã do `/status`.
`DeviceDto` ficou intacto.

**`Firmware` como campo próprio NÃO entrou.** Não há coluna em `Device`
(sem migração) nem regra de parsing documentada por fabricante — inventar um
regex sobre `sysDescr` seria versão inventada. A área A recebe hoje o
**`sysDescr` cru** (é onde os vendors embutem modelo + versão), o
`uptimeSeconds` e o carimbo **do poll que forneceu o dado**. `Firmware`
ficou como pendência na §7 (item 7).

**Comportamentos travados em teste**: o último poll **bom** vence (um timeout
não apaga o dado anterior, e o carimbo é o do poll bom, não o de agora) · sem
histórico ⇒ tudo `null` · `uptimeSeconds` não numérico ⇒ ignorado · device
inexistente ⇒ `KeyNotFoundException` e **nenhuma** consulta ao histórico.

### S4 — Portas: Velocidade/Duplex/PoE (área B)

> **✅ CONCLUÍDO 2026-10-15** — fatiado em S4a (a ficha pedia mais do que a
> arquitetura permite; §7 itens 8-11). TDD: 31 testes de domínio, 4 de sync e
> 2 asserções de parser, todos RED→GREEN.
> Evidência: `SwitchPortSpeedDuplexTests` **31/31** · Network+UnifiApiClient
> **206/206** · migração `AddSwitchPortSpeedDuplex` validada por
> `migrations script` (`ADD duplex text` + `ADD speed text` — cada uma vez
> só, nullable, sem backfill).

- **Objetivo**: `SwitchPort` passa a descrever a porta como a ficha L136-147.
- **Escopo**: campos `Speed`, `Duplex`, `PoEEnabled`, `PoEPowerBudget`
  (enums/validações da ficha) + migração EF + preenchimento a partir de
  `ifSpeed`/`ifDuplex` já no caminho do F29-3, e do `port_table` UniFi
  (Fase 07a).
- **Testes**: domínio (criação/validação), migração, provider que popula.
- **Critério de aceite**: porta existe ⇒ campos preenchidos quando o device
  responde; device sem SNMP ⇒ campos `null`, **nunca `0`/`Full` inventado**.
- **Depende**: S1 (mesma seam), migração é a maior parte do risco.

**Feito (fatiado — parou para perguntar em vez de violar o ADR):**

A ficha §B manda persistir `Speed`, `Duplex`, `PoEEnabled` e `PoEPowerBudget`.
Mas o **ADR-028 §10** nomeia *potência PoE* como métrica instantânea consultada
sob demanda e cacheada em Redis, e o código já cumpria (REGRA 8 em
`SyncSwitchPortsFromUnifiCommand`, `UnifiLiveDtos` e docs/36 L359). A própria
ficha se contradiz — a L342 dela cita REGRA 8. **Perguntado ao usuário em
2026-10-15** → "só Speed/Duplex no banco": PoE continua ao vivo, ADR-028 §10
intacto, **sem ADR novo** (§7 item 8).

O que entrou:

- `SwitchPort.Speed`/`Duplex` como enums da ficha L144-145, persistidos como
  **string** (`HasConversion<string>`, padrão da casa) + migração
  `AddSwitchPortSpeedDuplex` (nullable, sem backfill).
- **`Observe(linkUp, speed, duplex)`** no domínio — caminho do controller
  (UniFi/SNMP). Diferente de `Update` (cadastro manual): o cadastro manual
  **não** mexe em velocidade/duplex (SwitchPort.cs L11-12: vem do SNMP).
  Retorna `false` quando nada mudou ⇒ sync idempotente não conta update nem
  gera evento.
- **Ausência de leitura não apaga o valor** (keep-last-known): porta sem
  resposta não perdeu o enlace de 1G (§7 item 9).
- População pelos **dois** syncs UniFi (`SyncSwitchPortsFromUnifiCommand` e
  `SyncUnifiInventoryCommand`) via `Observe` + parsers.
- **Parser `full_duplex` corrigido**: campo ausente no JSON virava `false` →
  meio-duplex **inventado**; agora `null` (§7 item 10).
- `SwitchPortDto` ganha `Speed`/`Duplex` — enum serializado como texto por
  `JsonConverter` **por tipo** (o MVC não registra `JsonStringEnumConverter`
  global: o contrato numérico legado de `KnowledgeArticleStatus` 0..7 não pode
  mudar).

**Limitação honesta (§7 item 11)**: taxa fora da enum da ficha (2.5G/5G
multi-gig) ⇒ `null`. Incluir no enum ou migrar para bps é decisão humana.

**Não entrou**: preenchimento por SNMP puro — o `SnmpPortDetails` da ficha não
existe e o `SnmpInterfacesMonitoringProvider` atual não lê ifSpeed/ifDuplex.
Hoje só o caminho UniFi popula; switches não-UniFi ficam com `null`.

### S5 — Exposição na UI (áreas A-C)

> **Status: S5a + S5b + S5c ✅ CONCLUÍDOS 2026-10-15** — S5 completo.
> Evidência S5a: `format.test.ts` (formatação de porta) · `SwitchesPage.test.tsx`
> (colunas Velocidade/Duplex, RED na coluna que faltava → GREEN).
> Evidência S5b: `format.test.ts` `formatUptime` 3 testes (RED `not a function`
> → GREEN) · `SwitchesPage.test.tsx` 2 testes de faixa "Uptime/Sistema" (RED
> sem a faixa → GREEN) · `getDeviceSystemInfo` chamado com o id do switch.
> Evidência S5c: `SwitchesPage.test.tsx` 3 testes de saúde (último
> `SnmpHealth` vence o antigo; sem resultado ⇒ "Saúde: —"; falha ⇒ "—" por
> métrica) — RED 3 → GREEN.

- **Objetivo**: o técnico vê saúde e portas na tela do switch.
- **Escopo**: `SwitchesPage`/detalhe com CPU, memória, temperatura, uptime,
  firmware e portas com velocidade/duplex/PoE.
- **Testes**: suíte Vitest própria, TDD, casca `ListPageShell` já migrada (C-3).
- **Critério de aceite**: `tsc` 0, `eslint` 0, testes verdes.
- **Depende**: S3, S4.

**S5a feito (portas — colheita do S4):** `SwitchPortItem` ganha
`speed`/`duplex`; tabela de portas do switch ganha as colunas **Velocidade**
e **Duplex**; `formatPortSpeed`/`formatPortDuplex` em `utils/format.ts`
traduzem o enum serializado (`"Speed1G"` → `"1G"`) e mandam `null` → `"—"`
(nunca 0/vazio). Valor que o front não conhece aparece **crudo** (nada
escondido). PoE já aparece na tabela UniFi ao vivo (F26 Fase 5 — REGRA 8,
consulta sob demanda).

**S5b feito**: faixa **Uptime / Sistema** sob o cabeçalho do painel de portas
do switch — `getDeviceSystemInfo` (`services/api/inventory.ts`) + o type
`DeviceSystemInfoItem`; `formatUptime` (`utils/format.ts`) formata
"1d 1h"/"1h 1m"/"1m"/"42s" e manda leitura ausente ou absurda (negativo/NaN)
para "—" (nunca 0s inventado). A faixa **some** quando a query está pendente
ou em erro (device sem `system-info` não polui a tela); `sysDescr` aparece
**cru** (S3 expõe o texto do switch, sem parsing). Nota de teste: `getByText`
não junta text nodes irmãos — label e valor ficam no mesmo text node
(`{`Uptime: ${...}`}`).

**S5c feito**: saúde SNMP (`SnmpHealth`, S1) na mesma faixa do painel do
switch — `getDeviceResults(id, 100)` (o endpoint não filtra por tipo; 100
polls cabem SnmpHealth no meio de Pings e o mais recente vem primeiro) e
`check === "SnmpHealth"`. Labels: `CPU %` · `Memória %` · `Temperatura °C`
(chaves `cpuPercent`/`memoryPercent`/`temperatureCelsius`, texto do backend,
exibido cru). Sem resultado do tipo ⇒ `"Saúde: —"`; resultado falho
(`values` null) ⇒ `—` por métrica — **nunca 0 inventado**. A faixa some
em erro/pendência da consulta.

### S6 — Prevenção (área F) — **exige ADR antes**

- **Objetivo**: transformar o `AlertThresholds` hardcoded em modelo com
  limiar, duração, tendência, recorrência, impacto e confiança.
- **Estado**: gap que as 8 fichas declaram; nenhuma implementa.
- **Passo 0**: redigir ADR, aprovar, **depois** fatiar em sub-tarefas.
- **Depende**: histórico consolidado (S1/S2 rodando) para ter o que comparar.

---

## 5. Sequência

```
S1 ──► S2 ──────────────► S5 ◄── S4
  └──► S3 ────────────────┘      │
                                 └── (migração: maior risco)
S6 ── ADR ── (após histórico existir)
```

Primeira leva: **S1**. Depois S2, S3 (barato), S4, S5. S6 é outro ciclo.

---

## 6. Critérios globais (valem para todos os slices)

1. Teste escrito **antes**, RED confirmado, GREEN depois.
2. `dotnet test`, `tsc` e `eslint` verdes — teste quebrado nunca escondido.
3. **Nenhum valor inventado**: métrica indisponível ⇒ chave ausente ou
   `Succeeded = false`, nunca `0`.
4. Nada em `DeviceProfileDefaults` sem decisão explícita.
5. Sem `git push`; commit só com autorização; `tsconfig.tsbuildinfo` fora do
   stage.
6. Ao fim de cada slice: docs atualizados, `PROJECT_STATE.md`/`TASKS.md`
   atualizados, resumo + arquivos + testes + resultado + pendências.

---

## 7. Pendências de decisão

| # | Pergunta | Situação |
|---|---|---|
| 1 | **S3**: Firmware/Uptime do histórico ou coluna nova em `Device`? | **Resolvida no S3**: histórico, sem migração (recomendação adotada) |
| 7 | **`Firmware` como campo próprio** | **Aberta** — não entrou no S3. Opções: (a) coluna em `Device` + migração, (b) parsing documentado por fabricante sobre `sysDescr`, (c) ler ao vivo da API UniFi (F18 já tem `Firmware` no DTO, sem persistir). Enquanto isso a área A expõe `sysDescr` cru |
| 2 | **S4**: enums (`Speed`/`Duplex`) ou string livre? | **Resolvida no S4**: enums da ficha §B (L144-145), persistidos como string (`HasConversion<string>`, padrão da casa) e serializados como texto na API |
| 3 | **S6**: quem escreve o ADR e quando? | **Aberta** — depois de S1+S2, com histórico real |
| 4 | Chave de memória no `Values` | **Resolvida no S1**: `memoryPercent` = % em uso. Onde o OID reporta % direta, passa-se como o device reportou; só UniFi calcula (total − livre)/total porque os OIDs dela são de bytes. Valor fora de 0..100 é descartado (bytes no lugar de % não vira número enganoso) |
| 5 | Falha de banco no lookup do vendor | **Resolvida no S1**: mensagem própria (`Falha ao consultar o device por IP...`), nunca "não cadastrado" |
| 6 | Fans e PSUs (HP tem OID, os outros não) | **Aberta** — precisa do mapeamento de enumeração do fabricante; sem isso ler seria string sem significado garantido |
| 8 | **S4**: PoE persiste em `SwitchPort`? | **Resolvida (usuário, 2026-10-15)**: NÃO — ADR-028 §10 nomeia potência PoE como métrica sob demanda (Redis 30-60s) e já é entregue ao vivo (`GetSwitchUnifiPortsQuery`/`UniFiLivePage`). Persistem só Speed/Duplex, **sem ADR novo** |
| 9 | **S4**: ausência de leitura apaga velocidade/duplex? | **Resolvida no S4**: não — `Observe` só grava o que foi reportado (valor semi-estático sobrevive a poll sem resposta); cadastro manual (`Update`) não mexe nesses campos |
| 10 | **S4**: `full_duplex` ausente no JSON | **Resolvida no S4**: o parser virava `false` → meio-duplex **inventado**; agora `null` (o DTO já era `bool?`) |
| 11 | **S4**: taxas fora da enum da ficha (2.5G/5G multi-gig) | **Aberta** — hoje `null` (nada inventado). Opções: incluir no enum da ficha ou trocar a coluna por bps |
| 12 | **S4**: preenchimento por SNMP puro (switches não-UniFi) | **Aberta** — exige o `SnmpPortDetails` da ficha (não existe; o `SnmpInterfacesMonitoringProvider` atual não lê ifSpeed/ifDuplex). Hoje só o caminho UniFi popula |

### 7.1 Achado fora do escopo — corrigido no caminho

O projeto de testes **não compilava desde o `f0583c6`**: `IUnifiApiClient`
ganhou `SetPortConfigAsync` (Fase 07e / ADR-051) e os 3 fakes de teste não
foram atualizados (`GetSwitchUnifiPortsQueryTests`,
`SyncSwitchPortsFromUnifiCommandTests`, `UniFiSettingsCommandsTests`).
Sem isso nenhum TDD era possível. Corrigido com stub que **lança**
`NotImplementedException` — falha alto se algum teste de leitura tentar
escrever porta. Não é código de produto; é restauro do build.

### 7.2 Achados do S4 — build quebrado por sessão paralela (corrigido no caminho)

O repo **não compilava** ao iniciar o S4 (2026-10-15), por dois motivos que
não são do escopo desta task — ambos corrigidos porque sem isso nenhum TDD é
possível, e ambos visíveis/ comentados no código:

1. **DI registrava providers que não existiam**: `DependencyInjection.cs`
   L358-359 registrava `SnmpColorTonerProvider` e `SnmpPrinterStatusProvider`
   (F30-T002 impressora). A F30-T002 foi **só planejamento** (docs/43: "nenhuma
   alteração de código nesta task") — as classes nunca foram criadas.
   Registradas **comentadas**, com a data e a condição de reativar (criar as
   classes junto).
2. **`SetPortConfigAsync` duplicado em 3 fakes** (`CS0111`): o restauro da
   §7.1 e a sessão paralela adicionaram o mesmo stub nos mesmos arquivos.
   Mantida a versão que **falha alto**, removida a `Task.CompletedTask`.

Nenhum teste foi escondido: as duas correções foram feitas **antes** do RED do
S4, e a suíte completa fecha verde (§ evidência do S4).

---

## 8. Fora de escopo desta execução

- Área D (topologia) — já completa (F29-5).
- Área E (gestão SSH) — fora de escopo pela própria ficha.
- `Device.Criticality`, detecção de flaps/reboots (prioridade média da ficha).
- Qualquer coisa da F29-C3 (já concluída e commitada em `7c81751`).
