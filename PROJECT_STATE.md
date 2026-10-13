# PROJECT_STATE

## Estado

**Fase:** Fundação técnica e execução incremental
**Status:** Em execução incremental
**Última atualização:** 2026-10-13

## Regra operacional

Uma única Task principal deve estar em `IN_PROGRESS` por vez, salvo autorização explícita para trabalho paralelo.

## Estado atual

- [x] Visão do produto definida
- [x] Arquitetura principal definida
- [x] PostgreSQL definido
- [x] Redis definido
- [x] RabbitMQ + Outbox definidos
- [x] CQRS definido
- [x] Vertical Slice definido
- [x] MediatR Behaviors definidos
- [x] OpenTelemetry definido
- [x] Identity/RBAC definido (autorização só por role — ADR-016)
- [x] Tasks/SLA/SLO definidos
- [x] Notifications definidas
- [x] Remote Access definido
- [x] Metodologia Anti-Vibe Coding definida
- [x] Divisão em Tasks/SubTasks definida
- [x] Solution criada
- [x] Estrutura de projetos definida
- [x] Solução de testes criada
- [x] Docker Compose configurado
- [x] PostgreSQL configurado
- [x] Redis configurado
- [x] RabbitMQ configurado (MassTransit)
- [x] Migrations configuradas
- [x] Observabilidade base (Serilog, Health Checks, OpenTelemetry)
- [x] Frontend estrutura (React + TypeScript + Vite + PrimeReact)
- [x] Primeiro projeto compilando
- [x] Primeiros testes
- [x] Primeiro módulo implementado (dashboard web inicial)
- [x] Fundação ASP.NET Core Identity implementada com domínio, eventos, roles e sessões
- [x] Bootstrap Admin implementado como caso de uso em Application com adaptador em Infrastructure
- [x] RBAC de roles implementado como Vertical Slice/CQRS com policies e sincronização Identity
- [x] Infrastructure reorganizada conforme Sentinel (`Auth`, `Caching`, `Persistence`, `Repositories` e `Services`)
- [x] Permissions implementadas no domínio com matriz por role, claims e policies granulares
- [x] Capacidades transversais do Sentinel documentadas
- [x] Behaviors MediatR base implementados (correlation, logging, performance e validation)
- [x] Idempotência opt-in implementada com aquisição atômica no Redis
- [x] Outbox Dispatcher implementado no projeto Workers
- [x] Health checks operacionais separados em live, ready e startup
- [x] Auditoria persistida no pipeline, com migration e redaction por metadados
- [x] Inbox deduplicada por evento e consumidor
- [x] Resiliência por provider com retry, timeout e circuit breaker
- [x] Cache com invalidação opt-in após sucesso do handler
- [x] Feature flags com configuração e override temporário no Redis
- [x] Registry extensível para providers de Discovery e Monitoring
- [x] BuildingBlocks compartilhado criado e integrado ao Domain, Application e Infrastructure
- [x] Identity backend com registro, login por cookie, refresh rotativo, logout e endpoint `me`
- [x] Recuperação e troca de senha com token single-use no Redis e revogação de sessões
- [x] Login social Google no backend (F1-T014): GSI → `POST /api/v1/auth/google`, `id_token` validado no servidor (assinatura/JWKS, `aud`, `iss`, `exp`, `email_verified`), sem auto-provisionamento, anti-enumeração e Client Secret fora do frontend
- [x] Autenticação do dashboard (F14-T008): Login UniFi com alternância Senha/Google (sem One Tap), recuperação de senha, `authSlice` com `status` + persistência apenas de `user`, SessionGate revalidando com `/auth/me` no boot, rotas protegidas e botão "Sair" no AppLayout
- [x] Dashboard backend de usuários com consulta, ativação/desativa e administração de roles
- [x] API organizada em estrutura MVC modular alinhada ao Dashboard Sentinel, com Controllers, ViewModels, Services, HealthChecks, Middleware e Hubs
- [x] Pipeline MediatR ampliado com Authorization, Caching, Observability, Transaction e RateLimiting distribuído por Redis
- [x] Convites criados como agregado com token single-use (hash SHA256) e expiração
- [x] Retenção e cleanup operacional com worker separado e deletes em lote
- [x] Planejamento, validação técnica e diretrizes de governança da arquitetura Operations Center registrados nas Fases 19, 20, 21 e 22 (TASKS.md)
- [x] Implantação da infraestrutura Docker do Zabbix (F18-T001): profile `zabbix` com Zabbix Server (`zabbix/zabbix-server-pgsql:alpine-trunk`, API 8.0.0 — decisão do usuário 2026-10-03 de manter trunk até a tag 8.0 sair de *devel*), Web Nginx (8086 — 8085 é do frontend), Agent 2 (10053) e PostgreSQL 17 dedicado (`zabbix-db` na porta 5434); portas/token via `.env` (`ZABBIX_DB_PORT/ZABBIX_SERVER_PORT/ZABBIX_WEB_PORT/ZABBIX_AGENT_PORT/ZABBIX_TOKEN`); volumes persistentes `zabbix_db_data`, `zabbix_server_snmptraps` e `zabbix_server_mibs`; validação com `docker compose config` e runbook em `docs/27`
- [x] Integração Zabbix ↔ InfraControl configurada e validada ponta-a-ponta (2026-10-03): media types webhook 105 (Problem) / 106 (Recovery) via JSON-RPC → `POST /api/v1/integrations/zabbix/problems` com `X-Zabbix-Token`; action única id 7 (`operations` + `recovery_operations`); item trapper `test.value` (72011) + trigger 33178 no host "Zabbix server"; ciclo real PROBLEM (evento 8) → `monitoring_events` → alerta Critical → incidente → OK (evento 9) → alerta `Resolved` (gotchas: bitmask `severity:63` no user media, `default_msg:0` com mensagem explícita, sem condição PROBLEM/OK — documentados em docs/27 §4)
- [x] Especificação da Arquitetura da Camada de Integrações (F19-T000 / ADR-028): definindo padrões de autenticação, health, segredos, resiliência (Polly), sincronização e o modelo unificado de integração (Zabbix, UniFi, n8n) em `docs/adr/ADR-028-INTEGRATION-LAYER-ARCHITECTURE.md`
- [x] Especificação e ADR do Módulo de Projetos (F28-T001 / ADR-043 / docs/38): ciclo de vida de projetos de infraestrutura, fases sequenciais, acompanhamento bilateral, homologação pelo Administrador e geração automática na Knowledge Base
- [x] Domínio Base e Persistência de Projetos (F28-T002): agregado Project com 7 estados e aprovação Admin, ProjectMember, enums, eventos de domínio, IProjectRepository, mapeamentos EF Core, DbSets e migração AddProjectsAndMembers
- [x] Fases do Projeto e Estrutura (F28-T003): ProjectPhase com critérios/ordenação/progresso, CQRS Add/Update/Reorder/SetStatus + query com progresso ponderado, migração AddProjectPhases
- [x] Módulo de Acompanhamento (F28-T004): ProjectProgress, ProjectImpediment, ProjectEvidence e ProjectHistoryEntry com persistência EF, file store local e testes de domínio; tabelas incluídas na migração validada `AddProjectTrackingAndMembershipInvitations`
- [x] Convites e Equipe do Projeto (F28-T009): ADR-046, autorização criador/Admin, fluxo Invitation/onboarding, UI de membros e migration; 39 testes focados e script SQL idempotente validados
- [x] Governança e Homologação Admin (F28-T005): submission para aprovação final, homologação exclusiva de Administrador e gravação de parecer formal (`ApprovalNotes`)
- [x] Integração com a Base de Conhecimento (F28-T006): origem `Project`, composição determinística do rascunho e geração automática do artigo a partir de projeto homologado
- [ ] ADR-045 etapa 1 — Redis Sentinel: Compose usa subnet/IPs estáveis configuráveis e descoberta do primary para API/Workers/SignalR. Smoke test isolado validou quorum, failover após parar o container primary, chave replicada, restart do primary promovido sem reversão de papel e retorno do antigo primary como réplica autenticada. Migração de volumes com endereços dinâmicos antigos exige procedimento planejado e está documentada.
- [ ] F0-T026 — Hardening de escala/produção Compose (ADR-044): key ring compartilhado e persistente, evidências/backup em volumes nomeados, upstream DNS dinâmico Nginx, restart policies, limites e pool configuráveis no overlay de produção, segredo/TLS externos, coordenação Outbox e cenário k6; Compose base/produção, builds das imagens API/Workers/frontend, Nginx final e parser k6 validados; testes focados 3/3. Suite completa: 2127/2129, com duas falhas de retenção (expectativa de 9 tipos versus 10 incluindo UniFiSyncRuns). A migration AddUniFiIntegrationSetting foi corrigida para criar somente a tabela UniFi; regressão 2/2 e todas as migrations aplicadas com sucesso em PostgreSQL descartável. Carga API ponta-a-ponta permanece pendente: K6_AUTH_COOKIE ausente/vazio em .env.k6 na última checagem.
- [ ] CI configurado e validado remotamente (workflow criado; bloqueado por limite de minutos do GitHub Actions)

---

## Fase 30 — Operations Center: Capacidades de Dispositivos

> **Contexto**: Especificação do usuário (2026-10-13) - "A plataforma não deve ser apenas um inventário que recebe alertas. Ela deve ser o teu centro de operações de TI, capaz de reunir o estado dos equipamentos, a configuração administrativa, as métricas, os incidentes e os dados necessários para antecipar falhas."
> **Método de trabalho**: Criar ficha técnica por tipo de dispositivo antes de implementar (seção 7 da especificação). Cada ficha responde: dados que queremos visualizar, dados que o equipamento disponibiliza, fonte de cada dado, campos do cadastro, métricas com histórico, condições de alerta, problemas antecipáveis, operações permitidas, tratamento de indisponibilidade, estratégia de teste.
> **Ambiente real**: UniFi (Ubiquiti), Intelbras, HP, Aruba, TP-Link

- [x] **F30-T001 Ficha Técnica: Switch** <!-- CONCLUÍDA 2026-10-13: documento `docs/42-FICHA-TECNICA-SWITCH.md` criado com as 6 áreas propostas na especificação (A-F) e adaptado ao ambiente real multivendor. Mapeamento detalhado: (A) Identificação - lacunas em Firmware/Uptime (já persistido via SNMP sob demanda); (B) Portas - lacunas em Velocidade/Duplex/PoE/Trunk-Access (SNMP ifSpeed/ifDuplex/vendor-specific OIDs levantados); (C) Saúde - lacunas em Temperatura/Fans/PSUs com OIDs por fabricante real levantados via web search; (D) Topologia - área completa (F29-5 ghost nodes + LLDP/CDP ingestion); (E) Gestão SSH - fora escopo inicial (decisão registrada); (F) Prevenção - gap principal identificado: modelo de limiar/duração/tendência/recorrência/impacto/confianca não existe. OIDs levantados por fabricante: UniFi (CPU 1.3.6.1.4.1.10002.1.1.1.4.2.1.3.1/2/3, Memoria 1.3.6.1.4.1.10002.1.1.1.1.2.0/1.1.0, Temp 1.3.6.1.4.1.4413.1.1.43.1.8.1.5.1.0/1), Intelbras (CPU 1.3.6.1.4.1.13464.1.14.2.1.3.1.0/26138.3.600.9.1.1.3.33.1, Memoria 1.3.6.1.4.1.13464.1.14.2.1.3.2.0/26138.3.600.10.1.1.10.33, Temp 1.3.6.1.4.1.13464.1.14.2.1.3.3.0/26138.3.600.2.1.1.20.33), HP (CPU 1.3.6.1.4.1.11.2.14.11.5.1.9.6.1.0, Memoria 1.3.6.1.4.1.11.2.14.11.5.1.1.2.1.1.1.6.1, Temp 1.3.6.1.4.1.11.2.14.11.1.2.8.1.1.9.0, Fans 1.3.6.1.4.1.11.2.14.11.1.2.6.1.4.1, PSUs 1.3.6.1.4.1.11.2.14.11.1.2.6.1.4.2/3), Aruba (CPU 1.3.6.1.4.1.47196.4.1.1.3.22.1.0.1.1.3/10/11, Memoria 1.3.6.1.4.1.47196.4.1.1.3.22.1.0.1.1.4), TP-Link (CPU 1.3.6.1.4.1.11863.6.4.1.1.1.1.2/3/4, Memoria 1.3.6.1.4.1.11863.6.4.1.2.1.1.2, Temp 1.3.6.1.4.1.11863.6.4.1.3). Estrategia de implementacao multivendor definida: abstracao ISnmpHealthProvider + factory por Device.Vendor, providers especificos por fabricante, UniFi via controller API (F18), fallback seguro para vendors nao suportados. Prioridade de implementacao: 1) UniFi (reutilizar F18), 2) Intelbras (primeiro SNMP real), 3) HP, 4) TP-Link, 5) Aruba. Proximos passos recomendados: (1) Implementar campos faltantes em Device/SwitchPort; (2) Criar abstracao ISnmpHealthProvider + factory; (3) Implementar UniFiHealthProvider (reutilizar F18); (4) Implementar IntelbrasSnmpHealthProvider; (5) Implementar modelo de manutencao preventiva (limiar/duracao/tendencia); (6) Adicionar Device.Criticality. Nenhuma alteracao de codigo nesta task - apenas planejamento documentado. -->
- [x] **F30-T002 Ficha Técnica: Impressora e Multifunção** <!-- CONCLUÍDA 2026-10-13: documento `docs/43-FICHA-TECNICA-IMPRESSORA.md` criado com 7 áreas (A-G) baseadas na especificação. Mapeamento detalhado: (A) Identificação - lacunas em Firmware/Uptime (já coletado via SNMP sob demanda, não persistido); (B) Estado - lacunas em estado da bandeja/tampa/atolamentos; (C) Consumíveis - SnmpToner/SnmpDrum existentes (F2-T008), falta toner color/cilindro/fusor/resíduo; (D) Contadores - lacunas em total de páginas/split mono/color/volume mensal; (E) Filas - fora escopo inicial (requer integração com servidor de impressão); (F) Logs - lacunas em alertas/histórico; (G) Prevenção - gap principal identificado: modelo de limiar/tendência/volume mensal não existe. Printer-MIB (RFC 3805) universal levantado: prtMarkerSupplies (toner/cilindro: .6/.8/.9), prtMarkerLifeCount (total de páginas: .4.1.1), prtAlertTable (alertas: .18), prtInputStatus (bandeja: .8.2.1.9), prtOutputStatus (saída: .9.2.1.6), prtCoverStatus (tampa: .6.1.1.3). Cálculo de porcentagem: % = (prtMarkerSuppliesLevel / prtMarkerSuppliesMaxCapacity) × 100. Sentinelas especiais: -1=unknown, -2=unrestricted, -3=some remaining. Vendor-specific levantado: HP (total/mono/color .2.5/.2.6/.2.7, fusor/drum/transfer kits), Xerox (total/mono/color .20.1/.20.34/.20.35). Estratégia de implementação definida: abstração universal via Printer-MIB (maioria dos fabricantes implementa), estender SnmpTonerDrumProvider existente para color, criar PrinterStatusProvider (bandejas/tampas/alertas via prtAlertTable), criar PrinterCountersProvider (total de páginas via prtMarkerLifeCount), vendor-specific opcional para split mono/color detalhado. Filas de impressão fora escopo inicial (requer integração com servidor de impressão - Windows Print Server, CUPS, etc.). Prioridade de implementação: 1) Estender SnmpTonerDrumProvider para color, 2) PrinterStatusProvider, 3) PrinterCountersProvider, 4) Modelo de histórico/prevenção, 5) Vendor-specific counters. Nenhuma alteração de código nesta task - apenas planejamento documentado. -->

