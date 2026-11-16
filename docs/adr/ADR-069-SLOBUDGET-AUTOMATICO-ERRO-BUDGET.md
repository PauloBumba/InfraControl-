# ADR-069: SloBudget automático — consumo linear do error budget

- **Status**: aceito (2026-11-16 — direção do usuário: "Error budget
  automático" + "Consumo linear simples" + "Limitar em 100". Reversível
  por nova ADR.)
- **Data**: 2026-11-16
- **Relacionado**: ADR-068 (SLO mede checks vinculados), F8-T007
  (`SloBudget`), docs/04 §SloBudget

## Contexto

ADR-068 decidiu que `SloBudget` permanece **manual** — o cálculo
automático de error budget ficou explicitamente para ADR futura. Sem ele,
a coluna "Error budget" da `/sla` depende de POST manual e fica vazia em
produção, exatamente como as medições estavam antes do ADR-068.

O ADR-068 já produz, a cada ciclo do `SloMeasurementWorker`, a
disponibilidade real do SLO (`ValuePercent`) a partir dos
`MonitoringResult` dos checks vinculados — insumo suficiente para o
budget sem nova fonte de dados.

## Decisão (aprovada pelo usuário)

1. **Métrica**: consumo linear simples do error budget —
   `ConsumedPercent = min(100, (100 − ValuePercent) / (100 − TargetPercent) × 100)`.
   O `WindowDays` cancela na razão (fração de indisponibilidade observada
   ÷ fração de indisponibilidade permitida pelo alvo). Ex.: alvo 99.5%,
   disponibilidade observada 98.0% → `(2.0 / 0.5) × 100 = 400%` →
   gravado **100** (ver item 3).
2. **Período**: cada ciclo grava um `SloBudget` com
   `PeriodStart = now − WindowDays`, `PeriodEnd = now` — snapshot da
   janela móvel, mesmo desenho append-only das `SloMeasurement`
   (~24 linhas/dia/SLO, aceitável pela mesma justificativa do ADR-068).
   "Vigente" = o mais recente (`GetLatestBySloIdsAsync` já resolve).
3. **Limite em 100**: `SloBudget.ConsumedPercent` mantém a invariante
   de domínio 0–100 (decisão do usuário sobre permitir >100). Acima de
   100 o budget está esgotado — a UI já distingue
   Normal/Atenção/Esgotado nos limiares ≥50/≥100; o quanto estourou
   fica implícito na `LatestValuePercent` vs alvo.
4. **Gatilho**: dentro do mesmo ciclo do `SloMeasurementWorker` e só
   quando uma `SloMeasurement` é gravada — budget e medição são a
   mesma observação; ciclo pulado (sem dados ou já medido no intervalo)
   não gera budget. SLO sem vínculo não gera budget automático
   (POST manual de budget continua valendo).
5. **Alvo 100%**: `TargetPercent = 100` ⇒ budget de erro zero ⇒
   qualquer indisponibilidade consome 100%; disponibilidade 100% ⇒ 0%.
   Sem divisão por zero.
6. **Sem projeção/burn rate**: fora de escopo (o usuário escolheu a
   fórmula linear simples; projeção "o budget acaba em X dias" pode
   virar ADR futura).

## Consequências

- Nenhuma entidade/tabela nova — reusa `slo_budgets` e a validação
  0–100 existente; migração nenhuma.
- `SloBudgetCreatedDomainEvent` já existia (F8-T007) — a série
  automática passa a emiti-lo; nenhum consumer de notificação o trata
  (verificado: só catalogados os eventos com regra semeada).
- Cálculo puro extraído em `SloBudgetCalculator` (Application) —
  testável sem worker.
- Frontend não muda: `budgetConsumedPercent`/`budgetPeriodEndUtc` já
  fluem na listagem; budgets automáticos aparecem como os manuais.

## Alternativas descartadas

- **Permitir >100**: rejeitado pelo usuário — manteria información extra
  mas quebraria a invariante 0–100 do domínio e exigiria revisão de UI.
- **Budget por período fixo (mensal/semanal)**: o SLO já define
  `WindowDays` como janela de observação; período fixo criaria duas
  janelas conflitantes.
- **Atualizar um único budget aberto por SLO**: `SloBudget` é
  append-only por desenho (F8-T007, período fechado); histórico de
  consumo é desejável.
