# F46-T008 — Correções aprovadas do relatório (execução)

**Data:** 2026-10-25 · **Base:** `F46-RELATORIO-CONSOLIDADO.md` §6 · **Decisão:** usuário autorizou "faz tudo".

## 1. Itens executados do §6

| # | Item | Status | Evidência |
|---|---|---|---|
| 3 | `restart: unless-stopped` no `adminer` | **Feito** | `docker-compose.yml` + `docker compose up -d adminer` — policy `unless-stopped` confirmada no container religado |
| 4 | Triggers/itens `not-supported` do Zabbix | **Acompanhado — transitório** | Auditoria via API: 1166 itens `not-supported` em `infracontrol-docker` = itens órfãos de **containers temporários da própria suíte E2E/Testcontainers** ("No such container: crazy_meitner…" — 40 itens por container descartável), aguardando garbage collection do LLD; `infracontrol-docker` tem **≥1000 itens com valor normal** e o plugin responde (`docker.data_usage`, `docker.containers.discovery` OK no agent). `Zabbix server`: 12–13 itens inaplicáveis do template padrão (ipmi/java/vmware/connector não iniciados). Nenhuma ação necessária. |
| 5 | Bug trunk 8.0rc2 (`web.test.*`) | **Sem ação (contornado na T007)** | Reavaliar em upgrade do Zabbix para 8.0 final |
| 6 | SignalR limitado ao poll de 2s | **Sem ação (informativo)** | Aceitável para notificações |
| 2 | `json_agg` em `/tasks` | **Não feito (proposital)** | 10 SQLs fixos O(1), sem dor — revisar se volume crescer |
| 1 | Rate limit `Default` (120/min) produção | **Pendente de decisão humana** | Altera limite de API pública; requer aprovação explícita antes de mudar |

## 2. Correções encontradas na execução (fora do §6)

### 2.1 Suíte de integração: 4 falhas pré-existentes (ZabbixIngest)

- **Causa:** `BaseIntegrationTest` (whitelist manual de DI) não registrava `IZabbixEventContextReader` nem `IZabbixClient`.
- **Fix:** registro dos dois ports no construtor + `tests/.../Common/FakeZabbixClient.cs` (`IsConfigured=false`, listas vazias, `GetEventContextAsync→null`).
- **Resultado:** `ZabbixIngestIntegrationTests` 4/4.

### 2.2 Falso N+1 no orçamento de queries (F46-T003)

- **Sintoma:** `ReadEndpoints_ShouldStayWithinQueryBudget` acusava 8–10 SQLs em rotas com orçamento 6 (`/notifications/rules`, `/knowledge/articles`).
- **Causa raiz:** **poll de 2s dos 7 hosted services da API** (workers de notificação + `HealthStatusNotifier`) rodam dentro do mesmo host E2E e o `DbCommandCounter` contava o SQL de background junto com o do request — ruído, não N+1.
- **Fix de teste:** `E2EHost.DisableApiWorkers()` remove os hosted services do namespace `InfraControl.Api.Workers` no `ConfigureTestServices` **apenas** quando a classe pede (`QuietE2EHost`); classes que exercitam tempo real (`DataIntegrityRealtimeE2ETests`) continuam com workers ligados. Nota: xUnit exige ctor único em fixture — a opção virou método, não parâmetro de ctor.
- **Mensagens de falha agora imprimem os command texts** (`DbCommandCounter.Recent`) para diagnóstico futuro.
- **Resultado:** classe `QueryCountRegressionE2ETests` 3/3.

## 3. Validação final

- `dotnet build InfraControl.slnx` → **0 erros** (129 avisos pré-existentes).
- Suíte de integração completa → **229/229 verdes** (era 197/229 na entrada da T008).
- Suíte unit permanece 3022/3024 (2 falhas Skia pré-existentes, fora do escopo).

## 4. Pendências

1. **Rate limit `Default` em produção** — decisão do usuário (120/min pode derrubar rajadas legítimas de dashboard).
2. **Push dos commits locais** (`451ab7b` F41 SMTP, `f32d5fc` F44) — nunca feito automaticamente.
3. `OperationsCenterPage.test.tsx` OOM/lento — dívida da sessão F44 (não tocado).
4. Garbage collection do LLD do Zabbix limpará os ~1166 itens órfãos de containers E2E (automaticamente; reavaliar na próxima checagem).

## 5. Relatórios F46

- [T002 — Varredura E2E](F46-T002-varredura-e2e.md)
- [T003 — Latência/N+1](F46-T003-latencia.md)
- [T004 — Integridade/SignalR](F46-T004-integridade-signalr.md)
- [T006 — Saúde dos serviços](F46-T006-saude-servicos.md)
- [T007 — Zabbix](F46-T007-zabbix.md)
- [Consolidado](F46-RELATORIO-CONSOLIDADO.md) · este: T008
