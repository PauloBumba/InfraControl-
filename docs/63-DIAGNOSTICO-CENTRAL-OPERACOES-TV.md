# 63 — Diagnóstico: Central de Operações TV (Fase 1 do prompt master)

- **Data**: 2026-10-24 · **Base**: prompt master do usuário (16 seções) + ADR-063
- **Natureza**: auditoria somente-leitura (nenhum código alterado nesta fase)
- **Confiança**: tudo abaixo foi verificado no código com evidência `arquivo:linha`
  por 2 explorações paralelas (front + back) em 2026-10-24.

## 1. Estado atual do projeto

Arquitetura (confirmada, não reconstruir): **ASP.NET Core .NET 10 modular
monolith** (CQRS/MediatR + EF/PostgreSQL + Outbox/RabbitMQ + 6 workers de
canal) × **React 19 + Vite + TS + React Query v5 + PrimeReact** ×
Docker Compose. SignalR `NotificationsHub` (`/hubs/notifications`) já opera.

### Fases já executadas que se sobrepõem ao prompt

| Fase | O que entregou | Relevância |
|---|---|---|
| **F37-T001** | `OperationsCenterPage` (rota `/operations-center`): 6 módulos, fullscreen, **dados 100% fake** | é o esqueleto da "central", mas §12 do prompt proíbe fake apresentado como real |
| **F37-T002a** | `audioService.ts` (speechSynthesis + Web Audio API), `useAudio` (gesture/autoplay, prioridade, fallback), "Ativar áudio"/"Testar" na página | §6.5 do prompt **já implementado** (exceto estado honesto persistido) |
| **F37-T007** | estilos `uni-ops-*` + `operations-center.css` (tokens `--uni-*`, 2 temas), teste 5/5 | base visual do kiosk pronta |
| **F40** | Dashboard estilo UniFi (HealthBar, DeviceHealthCard, 2 colunas, painel Zabbix real, dark-safe) | §3 (qualidade UniFi) **já feito no /dashboard** |
| **F41** | SMTP configurável pelo admin (`/integrations/email`) | notificação externa por email funcional |
| **F42** | `NotificationEventRule` (entidade + migração + UI `/notifications/rules`, 15 eventos, on/off e canais por evento) | **molde exato** para a "matriz configurável de sons" do §6.2 |
| **M7** | KPI de incidentes abertos via alias `active` | Página 2 pronta |

### ⚠️ Decisão humana que conflita com o prompt master

> **2026-10-21 — painel FIXO (sem rotação)**: o usuário especificou tela
> fixa contínua com todos os módulos; a rotação automática (30s) foi
> **removida por decisão explícita** e travada em teste
> (`OperationsCenterPage.tsx:15-21`, `OperationsCenterPage.test.tsx:27-37`).
>
> O prompt master de 2026-10-24 pede **rotação a cada 60s**. Conflito
> direto — reversão exige autorização explícita (altera teste travado).

## 2. Funcionalidades reais × simuladas (por página-alvo)

| # | Página | Arquivo | Dado | Refresh hoje |
|---|---|---|---|---|
| 1 | Visão geral | `DashboardPage.tsx` (uni-dash), `HealthPage.tsx` | **REAL** (inventário, alertas, incidents, Zabbix summary, tasks) | load único + evento `infracontrol:refresh` (só o Dashboard escuta) |
| 2 | Central de incidentes | `IncidentsPage.tsx`, `AlertsPage.tsx` | **REAL** (CRUD + 5 estados `Open→Investigating→Identified→Monitoring→Resolved`; ack = `Investigating`) | load único |
| 3 | Rede/conectividade | `SwitchesPage`, `RoutersPage`, `WiFiPage`, `TopologyPage`, `UniFiLivePage` (30/60s), `UniFiDashboardPage` (60s), `NetworkPage` | **REAL** — UniFi ao vivo (API + cache Redis 60s), switches persistidos, zabbix-interfaces ao vivo, topologia persistida | só UniFi pagina |
| 4 | Servidores/serviços | `ServersPage`, `VirtualizationPage`, `DeviceDetailPage` | **REAL** (status consolidado, histórico de polls, checks sob demanda) | load único |
| 5 | Câmeras | `CamerasPage.tsx` | **REAL** (`type=Camera` + checks ONVIF `OnvifStorage`/`OnvifRecording` opt-in + `CameraNvrMapping`); **sem stream de vídeo** | load único |
| 6 | Operações | `PrintersPage` (toner SNMP real), `IntegrationsPage`, `Zabbix*`, `NotificationsPage` (push SignalR), `SettingsPage` | **REAL** | load único |
| — | **OperationsCenterPage** | kiosk 6 módulos | **FAKE** ("Dados fakes" `:7`, arrays `:23-119`; backup/uptime inexistentes como API) | relógio 30s + alertas fake 60s |

**Mocks órfãos**: `src/data/topologyData.ts`, `src/data/deviceData.ts` (0
importações); `dashboardData.ts` usado só para rótulos de navegação.

## 3. Problemas encontrados (gaps duros)

### Dados/tempo real
1. **`OperationsCenterPage` roda 100% com dados fictícios** — viola §12 do
   prompt. Não existe API de backup/uptime que ela exibe.
2. **Push mínimo**: o backend envia **um único evento SignalR**
   (`"notification"`, por usuário — `SignalRNotificationDispatcher.cs:20-30`).
   `IncidentUpdatedDomainEvent` (mudança de status/ack) **não está no
   consumer** (`NotificationEventConsumer.cs:37-51`) → ack não empurra nada.
3. **`Settings.autoRefresh` é salvo e nunca consumido** por nenhuma página.
4. **Evento `infracontrol:refresh` só alcança o Dashboard**; demais páginas
   ignoram o botão global de refresh.
5. **Polling existe só na UniFi** (30/60s). As outras 55 páginas: load único.
6. **Sem API de status de backup** — `BackupWorker` roda `pg_dump` 24h e
   grava `latest.json`, invisível para a UI (nenhum controller).
7. **Sem série histórica de métricas locais** — CPU/disco/RAM do Zabbix são
   consultas ao vivo sob demanda (`ZabbixProblemsQueries.cs:105-123`);
   histórico local só dos checks opt-in (`MonitoringResult`).

### Incidentes/áudio (§6)
8. **Sem matriz de sons persistida** — nenhuma entidade de "regra de som"
   (categoria × gravidade × transição × som × repetição × voz). Molde
   pronto: `NotificationEventRule` (F42) e `UserSettings`.
9. **Sem fila de anúncios backend** (F37-T002b backlog): dedup de anúncio,
   limite de repetição, escalonamento, dismiss — hoje o áudio é só
   client-side com alertas fake.
10. **Reconhecido ≠ resolvido é fraco**: `acknowledge` = status
    `Investigating` (não há estado `Acknowledged` separado nem campo de
    "reconhecido por/em"), **sem responsável, notas ou histórico** no
    Incident (`Incident.cs:19-30`).

### Visual/TV (§3, §11)
11. **Dark theme incompleto**: 45 `background:"white"` inline quebram o
    dark; PrimeReact fixo em `lara-light-blue` (`main.tsx:15`).
12. **Sem modo kiosk além do fullscreen manual** (sem anti-sleep,
    hide-cursor, auto-reload, indicador de próxima troca — a última parte
    só se a rotação voltar).
13. **Cores de severidade hardcoded** light-only (`AlertsPage.tsx:33-36`).
14. **LogsPage não tem dados** (só link externo ao Seq).

### Riscos operacionais (§10)
15. Nenhuma escrita destrutiva em equipamento está no escopo atual (bom) —
    UniFi port config (ADR-051) é a única escrita e exige `Operate`.
16. Credenciais: padrão AES-GCM por entidade de settings já é o padrão do
    repo (Zabbix/WhatsApp/Email) — manter para quaisquer novos segredos.

## 4. Requisitos do prompt ainda não implementados

| Requisito | Estado | Task proposta |
|---|---|---|
| §4 Páginas 1-6 com conteúdo específico | 6/6 páginas existem e são reais; **faltam blocos**: backups (P1/P6), fila de notificações (P6), "última atualização por integração" (todas), setor/duração/origem no grid de incidentes (P2) | F44-P1..P6 |
| §5 Rotação 60s configurável + ordem + indicador | **removida em 2026-10-21** (conflito) | pendente de decisão |
| §6.1/6.2 Config de sons + matriz persistida | áudio client-side existe; **matriz não** | F44-T-SOUND |
| §6.3 Comportamento (dedup, repete, rotação não atrasa, áudio falha ≠ perda) | **backend fila não existe** (F37-T002b) | F44-T-QUEUE |
| §6.5 Estado honesto do áudio (ativo/bloqueado/teste) | parcial (botão + teste existem) | fechar no F44-T-SOUND |
| §7 Painel admin de configurações da central | molde F42 pronto; falta UI específica (páginas da rotação, intervalo, sons) | F44-T-ADMIN |
| §8 Tempo real (SignalR) | **F44 ENTREGUE** (2026-10-24): push de transição de status (`IncidentUpdatedDomainEvent`, pulando Resolved) + refetch nas páginas (hooks `useOperationalRefresh`/`useAutoRefresh`) + `autoRefresh` consumido + botão global em todas as páginas centrais. Falta: kiosk/TV consumir o push (F45) | ~~F44-T-PUSH~~ ✓ |
| §9 Arquitetura/reuso | ok — nada a substituir | — |
| §11 TV (banner crítico, indicador de página, stale visível, sem hover) | parcial (kiosk existe) | no F44-T-UI |
| §12 Origem identificável por indicador | **fake na OperationsCenterPage** | F44-T-REALDATA |
| §13 24 testes obrigatórios | base 351 front + ~3031 back; testes de rotação/som novos | F44-T-TESTS |
| §14 Fases 1-6 | Fase 1 = este documento | fases 2-6 abaixo |

## 5. Plano de execução (Fases 2-6)

> Regra mantida: **uma task por vez**, TDD, validação por task, checkpoint
> com o usuário. Numeração: F44 (F43 = Google OAuth ADR-064, da outra sessão) ou continuação da
> F37 (T002b/T003-T006 já existem no backlog) — decisão de numeração na
> Fase 2.

### Fase 2 — Arquitetura (requer confirmação do usuário)
- Atualizar ADR-063 (hoje "rascunho" apesar de F37 executada) com as
  decisões novas: matriz de sons, eventos SignalR, e **rotação × painel
  fixo** (conflito §5).
- Definir entidade `SoundRule` (categoria × severity × transição ×
  enabled/sound/repeat/interval/voice/voiceText) — molde exato F42.
- Definir ciclo de vida do incidente: manter 5 estados e mapear
  "reconhecido" sobre `Investigating` **ou** promover campo
  acknowledgedBy/At (impacto em DTO/permission/audit — precisa de decisão).
- Mapa de fontes por indicador das 6 páginas (quase todo mapeado aqui).

### Fase 3 — Interface
- **F44-T-REALDATA**: ligar `OperationsCenterPage` às APIs reais (inventário
  + status consolidado + incidents + zabbix summary + unifi dashboard +
  printers + **novo endpoint de backup**); onde fonte não existir →
  "Dados indisponíveis" / "Integração não configurada" (§12), nunca fake.
- Modo TV: anti-sleep (`wakeLock`), indicador de última atualização e
  stale, banner de crítico persistente, sem hover essencial.
- Rotação 60s **somente se autorizada** (global, configurável, ordem,
  indicador) — reescreve o teste travado.
- Continuar dark theme (45 brancos inline + chips de severidade).

### Fase 4 — Incidentes + áudio
- **F44-T-SOUND**: entidade + migração + `GET/PUT /central/sound-rules`
  (Administer) + UI de matriz + testes de som/voz (§13 itens 7-9, 14, 18-19).
- **F44-T-QUEUE** (é o F37-T002b): fila de anúncios persistida + dedup +
  repetição configurável + dismiss com prazo + entrega via SignalR.
- Push: consumer de `IncidentUpdatedDomainEvent` (ack/status → SignalR) e
  consumo nas páginas (§13 itens 5-6, 15, 24).
- Config persistida e aplicada sem reiniciar (§7).

### Fase 5 — Integrações
- **F37-T005** (backup): ler `latest.json` → tabela/endpoint de status +
  bloco nas páginas 1/6; nunca mostrar sucesso sem resultado da execução.
- **F37-T003** (impressoras por modelo) e **F37-T004** (rede × setor).
- Qualidade de dados por fonte: `stale`/`unreachable`/`not configured`
  visível (UniFi já tem `controllerReachable`/`stale` como molde).

### Fase 6 — Validação
- Bateria dos 24 testes do §13 (rotação, som por categoria, dedup, áudio
  bloqueado, reconexão, stale, permissões), smoke ao vivo dos containers,
  logs sem erros 4xx/5xx, docs (`docs/14-UI-UX.md`, ADR-063, runbook),
  limitações documentadas.

## 6. Pendências / fora do escopo imediato

- Agrupamento cross-device (3 impressoras = 1 ocorrência) — F37-T006/Fase 6.
- Histórico de métricas local (TSDB) — só se gráfico TV exigir.
- Câmeras: sem stream por privacidade/banda (§4.5 do prompt) — mantido.
- M5 da caça-bugs e 34 `manual-review` continuam abertos (outro front).
- Backend: 2 testes `NotificationEventCatalogTests` podem estar dessincronizados
  (contagem 15 eventos na F42) — verificar na validação da Fase 6.
