# ADR-070 — Política de ruído: estabilização (flapping), adiamento de entrega e supressão de ciclo

- **Status**: Aceito
- **Data**: 2026-11-14
- **Contexto**: F55 (ADR-068) garante notificação só em mudança de estado;
  F57 eliminou a duplicidade ALERTA/INCIDENTE na mesma ocorrência. O
  usuário aprovou (2026-11-14) uma camada de redução de ruído inspirada em
  "Alert Noise Reduction": estabilização configurável por severidade,
  sem descartar eventos — todo PROBLEM/OK continua gravado em
  `monitoring_events`.

## Decisão

### 1. Estabilização = defer + cancel na entrega (não debounce no domínio)

O ciclo de vida do problema **não muda**: PROBLEM continua criando
Alert/Incident e levantando domain events na hora. O que muda é **quando
a entrega sai**:

- `NotificationDelivery.NextAttemptAt` passa a significar "não antes de"
  também para `Pending` (hoje só era consultado em retry `Failed`).
  `ListPendingByChannelAsync` passa a filtrar
  `Pending && (NextAttemptAt == null || NextAttemptAt <= now)` —
  retrocompatível (Pending nunca tinha NextAttemptAt preenchido).
- No consumer, eventos de monitoramento (AlertCreated/AlertResolved/
  IncidentCreated/IncidentResolved) resolvem o `MonitoringProblemState`
  da ocorrência e calculam
  `notBefore = OpenedAt + estabilização(status)`:
  - `ProblemStatus.Incidente` → `StabilizationCriticalMinutes` (default 2)
  - `ProblemStatus.Alerta` → `StabilizationWarningMinutes` (default 5)
  - `0` minutos = desligado (comportamento atual).
- `NotificationDelivery.DeferUntil(notBefore)` agenda a entrega; o
  registro Notification/Delivery existe desde já (auditável).

### 2. Flapping = ciclo curto cancela as entregas pendentes

Quando o OK chega e `ResolvedAt - OpenedAt < estabilização` do status do
ciclo, o ciclo é classificado como **flapping**:

- As deliveries ainda `Pending` das notificações do ciclo são marcadas
  `Cancelled` (nunca enviadas — a janela de defer garante que ainda não
  saíram).
- O evento de resolução **não gera notificação** (retorno antecipado no
  consumer).
- Regra do usuário: "não notificar o ciclo" — nem abertura nem
  resolução. O problema persistente além da janela notifica normalmente
  (a entrega deferred simplesmente vence).

### 3. Vínculo notificação ↔ ocorrência

`notifications` ganha `source_alert_id`/`source_incident_id` (nullable),
preenchidos pelo consumer a partir dos domain events. É o que permite
cancelar as entregas pendentes do ciclo sem ambiguidade — inclusive com
vários problemas simultâneos no mesmo host.

### 4. Escalonamento Alerta→Incidente substitui a notificação de alerta

`MonitoringProblemState.PreviousAlertId` guarda o Alert do ciclo anterior
em `UpdateOpen`. Quando o consumer processa a criação do incidente (ou o
alerta Critical), cancela as deliveries pendentes cujo
`SourceAlertId == PreviousAlertId` — o grupo recebe **somente** a
classificação INCIDENTE, nunca ALERTA+INCIDENTE da mesma ocorrência.
Alertas resolvidos sem incidente seguem intactos.

### 5. Configuração

`notification_policy_settings` (singleton, padrão
`whatsapp_integration_settings`): nesta fase só
`StabilizationWarningMinutes`/`StabilizationCriticalMinutes` (0–120,
0=off). Lembretes, rate limit, manutenção e demais políticas do spec
ficam para as próximas fases da F58 — migrações aditivas.

## Consequências

- Nenhum evento é descartado: `monitoring_events` continua gravando
  PROBLEM/OK/repetições; deliveries canceladas ficam `Cancelled`
  (auditável); o motivo vai para log estruturado (auditoria formal de
  supressão é F58-T005).
- Críticos não esperam a janela de atenção: têm janela própria menor.
- Sem estado de problema (alertas nativos F6, manuais, identity) nada
  muda — defer/cancel só se aplica a eventos com
  `MonitoringProblemState` resolvível.
