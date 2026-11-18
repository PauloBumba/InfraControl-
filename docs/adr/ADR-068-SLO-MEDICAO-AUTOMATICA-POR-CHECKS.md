# ADR-068: SLO mede disponibilidade de checks vinculados (job horário → SloMeasurement)

- **Status**: aceito (2026-11-16 — direção do usuário: "segue" após o
  questionário de escopo. Reversível por nova ADR.)
- **Data**: 2026-11-16
- **Relacionado**: docs/04 §Slo/SloMeasurement/SloBudget (Fase 8),
  F5-T001 (`MonitoringCheck`), F54-T001 (`GetAvailabilityCellsAsync`),
  ADR-030 §5 (eventos CRUD), ADR-066 (módulos de acesso)

## Contexto

O SLO hoje é só cadastro + dados manuais: `SloMeasurement` e
`SloBudget` só são gravados por POST manual, então a listagem
("Última medição" / "Error budget") fica permanentemente vazia em
produção. O pedido do usuário é que o SLO meça **de verdade** a
plataforma.

Dados reais disponíveis para medir:

- `MonitoringResult` — um poll por execução de check, com `DeviceId`,
  `Kind`, `Succeeded`, `DurationMs` (F5). É a fonte de verdade de
  "o serviço respondeu?".
- `MonitoringCheck` — instância (DeviceId, Kind, Enabled) criada a
  partir do `DeviceProfile`.
- `Alert`/`Incident` — já cobertos pela semântica de notificação;
  **não** são a fonte escolhida (ver decisão 1).

Lacuna de modelo: `Slo` não tem nenhum vínculo com o que mede — o
`Service` também não referencia devices. É preciso decidir **o que**
o SLO mede e **como** o vínculo é declarado.

## Decisão (aprovada pelo usuário)

1. **Métrica**: disponibilidade de checks —
   `ValuePercent = succeeded / total × 100` dos `MonitoringResult`
   dos checks vinculados, na janela do SLO (`WindowDays`).
   Não mede incidentes/alertas (fora de escopo; pode virar ADR futura).
2. **Vínculo**: multi-seleção explícita **SLO ↔ MonitoringCheck** na
   tela `/sla` — entidade de junção `SloCheckBinding` (SloId FK
   Restrict + MonitoringCheckId FK Restrict, único por par). Mais
   flexível que "Service tem devices" (que acoplaria inventário a SLA)
   e mais honesto que "todos os devices" (número genérico sem sentido).
3. **Cálculo**: `SloMeasurementWorker` (Workers, BackgroundService —
   mesmo padrão de `SlaBreachWorker`/`ScheduledReportWorker`) a cada
   `SloMeasurement:IntervalMinutes` (default 60, clamp 15–1440). Para
   cada SLO **com bindings**, calcula a disponibilidade da janela e
   grava um `SloMeasurement` novo (histórico imutável, ADR-030 §5).
   SLO sem binding **não** é medido (medições manuais continuam
   valendo — o POST existente não muda).
4. **Agregação**: uma query `GROUP BY (DeviceId, Kind)` sobre
   `MonitoringResult` desde `min(WindowDays)` dos SLOs medidos;
   o pareamento check↔resultado é resolvido em memória
   (`MonitoringCheck` → `DeviceId`+`Kind`; `MonitoringResult` não tem
   CheckId). Checks desabilitadas/excluídas saem do vínculo por FK
   Restrict e o resultado ignora `Enabled=false` (check desligada não
   mede — não há poll novo dela).
5. **Sem dado = sem medição**: se `total = 0` na janela, **nenhuma**
   `SloMeasurement` é gravada — a UI mostra "Sem medição na janela"
   (nunca um 100% inventado).
6. **Orçamento**: `SloBudget` permanece **manual** neste slice —
   o cálculo automático de error budget (burn rate) fica para ADR
   futura. A listagem segue mostrando o último orçamento manual.
7. **Permissões**: binding segue `SlaRead`/`SlaManage` (mesmos
   endpoints de SLO — `monitoringCheckIds` entra no
   Create/UpdateSloCommand; a lista de checks para o picker usa
   `GET /monitoring/checks` com `RoleAccess.Read` — mesmo nível da
   listagem de monitoring).

## Consequências

- `slo_check_bindings` nova tabela + migração (FK Restrict nos dois
  lados: excluir SLO ou check com vínculo é bloqueado — coerente com
  a proteção já existente de measurements/budgets).
- Volume: ~24 medições/dia/SLO — aceitável; `slo_measurements` já é
  a série histórica desenhada para isso.
- `SloDto` ganha `monitoringCheckIds`/`boundCheckCount` para o form
  de edição e a coluna "N checks".
- Testes: entity/repo (bind/unbind, único par, Restrict), agregação
  de disponibilidade, worker (janela, skip sem dados, skip sem
  binding), endpoints, frontend do picker.

## Alternativas descartadas

- **Alert/Incident como fonte**: mede "tempo de falha percebida", não
  "disponibilidade do check"; mistura severidade e atrasos de triagem.
- **Service ↔ Devices**: acopla cadastro de serviço a inventário;
  um device pode servir a vários serviços.
- **Cálculo on-demand no GET**: sempre fresco mas sem histórico nem
  insumo para `SloBudget`/tendências.
