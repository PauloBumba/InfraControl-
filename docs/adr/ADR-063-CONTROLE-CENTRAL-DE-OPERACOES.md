# ADR-063: InfraControle — Central de Operações (painel visual + motor de anúncios)

- **Status**: **aceita** — conceito aprovado e executado em partes (F37-T001/T002a/T007
  entregues 2026-10-20/21); refinada pelo prompt master do usuário em 2026-10-24
  (diagnóstico: `docs/63-DIAGNOSTICO-CENTRAL-OPERACOES-TV.md`) com as decisões
  D8/D9 abaixo. A execução continua por fase/task, uma de cada vez.
- **Data**: 2026-10-20 · **Revisões**: 2026-10-24 (D8/D9)
- **Contexto**: conceito "InfraControle — Central de Operações" apresentado
  pelo usuário em 2026-10-20 (fonte do texto: planar.com), com 6 módulos de
  painel, Modo Sala de TI (voz/som/rotação), arquitetura em camadas e 6 fases
  de implementação. Escolha do usuário (2026-10-20): **ADR + plano de fases**
  antes de código. Tasks serão criadas em `TASKS.md` → Fase 37 (F36 está em
  uso).

## Contexto — estado atual verificado no código (2026-10-20)

O documento propõe uma arquitetura que **é a arquitetura atual** do projeto
(ASP.NET Core + SignalR + PostgreSQL + RabbitMQ + Docker Compose). Não há
reconstrução: falta a **camada de experiência** (painel que fala) e a
**inteligência de agrupamento**.

### Módulos × código existente

| Módulo (documento) | Estado hoje | Evidência |
|---|---|---|
| 01 Servidores/virtualização | **existe** | `ServersPage`, `VirtualizationPage`, `DeviceDetailPage`, `DeviceType.Server`, vCenter (ADR-058) |
| 02 Impressoras/consumíveis | **existe (lista)** | `PrintersPage`, discover SNMP; **sem modelo de toner/percentual** por MIB de fabricante |
| 03 Rede/Wi-Fi | **existe** | `Network/Switches/Routers/WiFi/Topology` + `UniFiDashboardPage/UniFiLivePage/IntegrationsPage` + SNMP discovery |
| 04 Backup/armazenamento | **backend sim, front NÃO** | `PostgresBackupService`/`BackupWorker` + `docs/operations/BACKUP-RESTORE.md`; **nenhuma página de backup no front** |
| 05 Câmeras/telefonia | **existe (lista)** | `CamerasPage`, `PhonesPage`; sem integração NVR/ONVIF/CDR |
| 06 Central de ocorrências | **existe** | `AlertsPage` + `IncidentsPage` (severidade, estado, device, responsável, ciclo Open→Resolved ADR-030) |
| Fonte Zabbix | **existe** | webhook → `MonitoringEvent` → `Alert`/`Incident`; páginas `ZabbixHosts/Overview/Settings` |
| Tempo real | **existe** | `NotificationsHub` (`/hubs/notifications`) + `services/signalr/connection.ts` |
| Canais de saída | **existe (sem voz)** | `NotificationChannel`: Email/Telegram/WhatsApp/Webhook/Slack — **0 ocorrências de `speechSynthesis`/áudio no repo** |
| Modelo comum de evento | **existe** | `MonitoringEvent` (source, device, severity, status, occurredAt, message, rawPayload) |
| Deduplicação | **parcial** | anti-storm por device (`GetOpenByDeviceAsync` — 1 incidente aberto por device) + dedup de delivery no fanout WhatsApp (F33-T003); **não existe dedup de ANÚNCIO nem agrupamento cross-device** |
| Agrupamento (3 impressoras = 1 ocorrência) | **não existe** | `IncidentAlert` vincula N alertas a 1 incidente do **mesmo** device; não há correlação por setor/`LocationId` |
| Rotação de painéis / kiosk / fullscreen | **não existe** | 0 ocorrências de rotação/`requestFullscreen`/kiosk |
| Modo Sala de TI (voz+som) | **não existe** | ver acima |
| Dupla experiência (executivo × operacional) | **parcial** | `DashboardPage` único (visão geral); sem variante "à distância" |

## Decisão

### D1 — Duas experiências, um motor

O documento está adotado: **Painel executivo/visual** (legível à distância,
poucos números) e **Central de operações** (densa, com histórico/ações)
consomem **o mesmo motor de eventos já existente** (MonitoringEvent →
Alert → Incident → NotificationEventConsumer), com permissões e densidade
diferentes (`RoleAccess`/policies já existentes). Nenhum modelo de dados
novo para "ocorrência": a ocorrência **é** o `Incident` (com seus vínculos
`IncidentAlert`), estendido apenas onde faltar (D4).

### D2 — Não refazer fontes

Zabbix/UniFi/SNMP continuam sendo a fonte; a plataforma **informa e
correlaciona**, nunca substitui triggers/métricas existentes (postura atual
mantida — ADR-027/ADR-051/Discovery).

### D3 — Motor de anúncios: fila no backend, reprodução no front

- **Fila de anúncios persistida no backend** (tabela + outbox já é padrão
  do projeto): deduplicação, limite de repetição, escalonamento e
  priorização precisam sobreviver a reload/relogin e valer para **todas**
  as telas simultâneas — no front puro cada aba anunciaria em paralelo.
- **Reprodução no frontend**: `speechSynthesis` (Web Speech API) + sons
  via áudio; entrega dos itens da fila **via SignalR já existente**
  (`NotificationsHub` — sem novo hub se o payload couber).
- **Alternativas rejeitadas**: (a) fila só no front (quebra dedup entre
  telas e ao fechar a aba); (b) áudio no servidor (o navegador é o
  renderer de voz, e o documento já aponta a limitação de permissão do
  áudio).

### D4 — Agrupamento/correlação: regras verificáveis, fase posterior

Agrupar N eventos numa ocorrência (ex.: 3 impressoras do mesmo setor) e
escalonar reanúncios exige **regras explícitas e auditáveis** — decisão de
produto com dados reais do hospital. Fase própria (6 do documento), com:

1. deduplicação de anúncio (1 problema ≠ 10 vozes);
2. agrupamento por `LocationId`/evidência (só quando houver);
3. escalonamento de crítico sem reconhecimento;
4. controle de voz (frequência, prioridade, silenciar sem esconder o visual).

Nada disso é implementado hoje; a fase anterior (1–2) **não depende** dele.

### D5 — Voz: Web Speech API com fallback visual

Conforme o documento: anúncio em pt-BR, sessão dedicada de Chrome
(quiosque), validação de áudio no início (gesture/autoplay policy) e
**regra inegociável**: falha de áudio **nunca esconde** o alerta crítico
visual. Áudio gravado (mensagem fixa) como fallback da síntese.
Mensagem de voz é derivada da mensagem visual do evento (o `Incident`
já tem título/severidade/device) + template configurável por painel.

### D6 — Linguagem segura (hospital)

O sistema **informa e recomenda verificação**; nunca ordena destruição
(ex.: "disco cheio" → não anuncia "apague o backup"). Ação de risco
(reiniciar, apagar, VLAN, comandos) fica **fora** da central — se um dia
existir, é fluxo separado, autenticado, autorizado e auditado (matriz
`RoleAccess`/`device.*.connect` já é o precedente).

### D7 — Execução: uma task por vez, frontend primeiro

Sequência do documento adaptada ao backlog existente; **Fase 1–2 validam a
experiência inteira antes de multiplicar integrações** (recomendação final
do próprio documento = decisão do usuário). Próxima fase livre: **F37**
(F33–F36 ocupadas).

### D8 — Exibição da TV: modos alternáveis (2026-10-24, decisão do usuário)

A tela da central tem **dois modos configuráveis pelo admin**: **fixo**
(padrão — todos os módulos visíveis ao mesmo tempo, como decidido em
2026-10-21) **ou rotação** (páginas alternam a cada intervalo configurável,
com ordem e seleção de páginas definidas pelo admin, indicador da página
atual). A rotação nunca reinicia o temporizador por atualização de dados,
não perde estado de incidentes e é suspensa quando a aba fica invisível
(§5 do prompt master). O teste que travava "somente painel fixo" passa a
assegurar o modo padrão, não a proibir a rotação.

### D9 — Prioridade de execução: tempo real → áudio/matriz → dados reais (2026-10-24)

Ordem escolhida pelo usuário: (1) **tempo real** — push de transição de
status de incidente pelo pipeline já existente (outbox → RabbitMQ →
`NotificationEventConsumer` → SignalR; `IncidentUpdatedDomainEvent` entra no
catálogo com default SignalR-only e pulando `Resolved` para não duplicar o
evento de resolução) + refetch nas páginas; (2) **matriz de sons
persistida** copiando o molde F42 (`NotificationEventRule` entidade +
migração + endpoint Administer + UI) e a fila de anúncios (F37-T002b);
(3) **dados reais** na `OperationsCenterPage` (hoje fake, §12 do prompt) +
API de status de backup. Cada etapa é uma task com checkpoint.

## Plano de fases (→ TASKS.md F37+)

| Fase (documento) | Entrega | Reaproveita | Gap real a construir |
|---|---|---|---|
| **1 — Painel central + Zabbix** | página kiosk + cartões de disponibilidade + rotação de telas + atualização em tempo real | SignalR, `Incident`/`Alert`, severidade, `Servers/Switches/Printers` pages como fonte de dados | rotação de painéis, layout "à distância", tela cheia |
| **2 — Alertas sonoros e por voz** | fila de anúncios, prioridade, dedup de anúncio, reconhecimento, limite de repetição, configuração por painel | D3 (backend), `NotificationsHub`, canais existentes | fila persistida + `speechSynthesis`/sons + reconhecimento de anúncio |
| **3 — Impressoras** | toner/tinta/erros reais por modelo (validar MIB antes de prometer %) | `PrintersPage`, discover SNMP | MIB de consumíveis por fabricante; estado aproximado onde não há % |
| **4 — Rede/Wi-Fi** | UniFi + switches cruzados com inventário/setor | UniFi API viva (H6 já valida `enabled`), discovery SNMP, `LocationId` | cruzamento evento↔inventário↔setor no painel |
| **5 — Backup, telefonia, câmeras** | tela de backup + fontes restantes | `PostgresBackupService`/worker (já roda), `CamerasPage`, `PhonesPage` | **página de Backup**; leitura de estado do software de backup; NVR/CDR quando houver API |
| **6 — Inteligência operacional** | correlação, recorrências, tendências, agrupamento | D4; `IncidentAlert`, `LocationId`, histórico | regras verificáveis, agrupamento cross-device, escalonamento |

### Critérios de aceite comuns (fases 1–2)

- Painel roda em Chrome em tela cheia e **reinicia sozinho** sem perder o estado do feed.
- Crítico **interrompe** a rotação; item baixo espera na fila.
- Dedup: 10 atualizações do mesmo problema = 1 anúncio.
- Voz falhando (sem permissão/sessão) = alerta **visual continua**.
- Nenhuma ação destrutiva é anunciada como recomendação (D6).

## Fora de escopo (decisão do documento, mantida)

- Ações operacionais de risco (reiniciar, apagar backup, VLAN, comandos).
- Substituir/replicar o que Zabbix/UniFi já fazem bem.
- RabbitMQ obrigatório na v1 (só quando volume/consumidores justificarem —
  já presente no compose para os atuais; o motor de anúncios não exige
  novo broker).

## Consequências

- **Pendência de aprovação**: este ADR é **rascunho** — nenhuma task F37
  é executada antes do aceite humano (gate idêntico ao ADR-060 F34-T001).
- Novas tabelas (fila de anúncios) = migração EF com `--no-build`.
- A Fase 3 (impressoras) **não promete percentual universal** de toner —
  validação por modelo antes de escalar (alerta do próprio documento).
- O "modo quiosque" pode expor dados em tela grande — usar a
  permissão de leitura já existente (`RoleAccess.Read`) e não exibir
  credenciais/serviços sensíveis no painel público.
