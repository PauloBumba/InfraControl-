# ADR-029 — Ciclo de Vida do PDI (Status, Gestor, Período e Histórico)

- **Status**: aceito
- **Data**: 2026-10-04
- **Contexto**: Fase 1 — Fundamento do módulo PDI (`docs/30-PLANO-MODULO-PDI.md` §3, §23, §25). O PDI mínimo implementado em 2026-10-04 (entity de1 registro por usuário com estratégia/prazo/status de3 estados) não cobre o ciclo de vida desenhado pelo usuário, nem guarda quem fez o quê.

---

## Problema

O plano de produto do PDI (docs/30) exige:

1. **Ciclo de vida com estados claros** (§3): Rascunho → Planejado → Aguardando aceite → Ativo → Em acompanhamento → Em avaliação → Concluído, com os ramos Replanejado ⇄ Ativo e Ativo → Cancelado — "não apagar histórico".
2. **Participantes**: além de Admin/Operador, o **gestor/liderança** responsável pelo desenvolvimento (§2) e o **período** do ciclo (§20 mostra painéis por "1º semestre 2027"; §23 prevê `PDIPeriod`).
3. **Histórico/timeline** (§17): linha do tempo auditável de criação, transições e alterações ("quem, quando, de/para").

O enum atual (`InProgress/Completed/Cancelled`) não representa o ciclo e os dados existentes não têm gestor, período nem trilha de eventos.

## Decisão

### 1. `PdiStatus` com 9 estados (enum armazenado como string)

| Estado | Enum (string no banco) |
|---|---|
| Rascunho | `Draft` |
| Planejado | `Planned` |
| Aguardando aceite | `AwaitingAcceptance` |
| Ativo | `Active` |
| Em acompanhamento | `InProgress`¹ |
| Em avaliação | `UnderEvaluation` |
| Concluído | `Completed` |
| Replanejado | `Replanned` |
| Cancelado | `Cancelled` |

¹ O token `InProgress` é reaproveitado com o novo significado "Em acompanhamento" (era "Em andamento"); a migração reescreve as linhas antigas `InProgress` → `Active` antes (ver §4).

### 2. Tabela de transições (setas exatas do diagrama docs/30 §3)

```text
Draft → Planned → AwaitingAcceptance → Active → InProgress → UnderEvaluation → Completed
                                     (aceite)      ├→ Replanned → Active
                                                   └→ Cancelled
```

- Validação no **domínio** (`PdiTransitions`): transição fora da tabela → `ArgumentException` → HTTP 400 pt-BR.
- **Mesmo estado** é sempre permitido (edição de campos sem transição).
- **Permissões de transição**: Admin transiciona qualquer estado válido; Operador só `AwaitingAcceptance → Active` (aceitar o próprio plano, §15). Qualquer outra mudança de status por Operador → 403.
- **Criação**: Admin pode criar em qualquer estado válido (retroativo/importação); o formulário do front inicia em `Draft` (decisão do usuário, 2026-10-04).
- **Cancelado é alcançável somente de `Active`** (decisão do usuário, 2026-10-04) — fiel ao diagrama; estender se a operação pedir.

### 3. Gestor e período na própria entidade

- `ManagerId` (nullable): usuário gestor/liderança responsável — definido **apenas pelo Admin**; entrada do Operador é ignorada (comentário no command). "Meus PDIs" do gestor (docs/30 §19) vem em fase própria.
- `PeriodStart`/`PeriodEnd` (`DateOnly`, opcionais): datas do ciclo; validação `início ≤ fim` quando ambos existirem. Rótulos ("1º semestre 2027") serão derivados nos indicadores (Fase 8).

### 4. Histórico (timeline) — entidade própria, append-only

Nova tabela `individual_development_plan_history`: `id`, `user_id` (dono do plano), `type` (`Created|Updated|StatusChanged`), `actor_id`, `from_status`, `to_status`, `reason` (≤500, opcional), `detail` (≤500, campos alterados em pt-BR), `create_date_time` (momento do evento), índice `(user_id, create_date_time)`.

- Gerado pelo **domínio**: `Create` registra `Created`; `Update` registra `StatusChanged` (de/para + motivo) quando o status muda e/ou `Updated` (lista de campos) quando outros campos mudam; as entradas são drenadas pelo command e persistidas na **mesma transação** do save.
- Append-only: sem edição/exclusão pela API (CRUD de histórico não existe — só `GET`).
- **Não confundir** com "histórico de ciclos" (snapshots de planos anteriores, decisão de2026-10-03 de manter 1 registro vigente por usuário): este é o timeline de eventos do plano vigente; snapshots continuam fora do escopo.

### 5. Notificações e eventos distribuídos

Nenhum evento novo: transições e alterações continuam dentro de `PdiCreated/PdiUpdatedDomainEvent` (pipeline outbox → RabbitMQ → `NotificationEventConsumer`, canais inalterados — criação Email+SignalR, edição SignalR). O `StatusLabel` do consumer passa a cobrir os 9 estados.

### 6. Migração de dados

```sql
UPDATE individual_development_plans SET status = 'Active'   WHERE status = 'InProgress';
-- 'Completed' e 'Cancelled' são mantidos (mesmos tokens)
ALTER  ... ADD manager_id, period_start, period_end;
CREATE TABLE individual_development_plan_history ...;
```

## Consequências

- **Positivas**: ciclo de vida auditável e fiel ao produto; base para aceite (§15), acompanhamento (Fase 3), avaliação (Fase 6), indicadores (§21) e dashboards (§18–20); timeline entrega a auditoria pedida em §17.
- **Negativas/limitações**: estados intermediários obrigam o Admin a percorrer o ciclo (sem "pular etapas" — fiel ao diagrama); `Replanned` não guarda o motivo do replanejamento além do campo `reason` (detalhamento em Fase 5); histórico não detalha valores anterior/novo por campo além do `detail` textual (Fase 5 estende); o campo `InProgress` mudou de significado — qualquer script externo que compare o token deve considerar a migração.
- O consumer notifica alterações de status como "PDI atualizado" (mensagem genérica com prazo/status) — mudanças de redação ficam para fase própria se desejado.

## Referências

- `docs/30-PLANO-MODULO-PDI.md` §2 (participantes), §3 (ciclo de vida), §15 (aprovações), §17 (histórico), §23 (modelo de domínio), §25 (Fase 1).
- `docs/29-OPERATIONS-UX-BACKLOG.md` §8; ADR-005 (domain events/outbox); decisões do usuário em 2026-10-04 (período em datas, criação livre com front em Rascunho, Cancelado só de Ativo).
