# Cleanup, Retention, Testes e CI/CD

## Cleanup

Worker separado (`CleanupWorker`, intervalo `Cleanup:IntervalHours`,
default 24h; desligável via `Cleanup:Enabled=false`).

Exemplos de retenção e cobertura implementada (F12-T001, 2026-09-25):

```text
Outbox processado: 7–30 dias      -> 30 dias (implementado)
ProcessedMessages: 30 dias        -> 30 dias (implementado)
NotificationDelivery: 90 dias     -> Notifications: 90 dias (implementado)
Logs: 30–90 dias                  -> pendente (tabela de logs não existe)
Métricas: 90–365 dias             -> MonitoringResults: 180 dias (implementado)
Histórico: 1–5 anos               -> RemoteSessions: 90 dias; DiscoveryRuns:
                                      180 dias, só quando FinishedAt != null
                                      (implementado; ativos nunca são tocados)
AuditEntries: 90 dias             -> implementado
```

Config em `Retention:*` (appsettings); exclusão em batch de `BatchSize`
(500) por tabela por execução.

### RetentionPolicy (F12-T002, 2026-09-25)

A tabela `retention_policies` (entidade `RetentionPolicy`, migração
`AddRetentionPolicies`) sobrescreve esses defaults **em runtime**: uma
linha por tipo (`data_type` único, `days` 1–3650). O `CleanupService`
lê as políticas a cada execução e mescla sobre a config — ausente a
linha, vale `Retention:*`.

API (`[Authorize(Roles="Admin")]` + `RequiredPermissions =
RetentionManage` — nova permissão `operations.retain.manage`,
Admin-only):

```text
GET    /api/v1/operations/retention-policies          # 7 tipos, valor efetivo
GET    /api/v1/operations/retention-policies/{type}   # um tipo
PUT    /api/v1/operations/retention-policies/{type}   # upsert {days}
DELETE /api/v1/operations/retention-policies/{type}   # reset p/ config (404 se não existe)
```

Resposta por tipo: `{ dataType, days, source }` com `source` =
`"Policy"` (linha na tabela) ou `"Config"` (default). GET sem política
nunca é 404 — todo tipo tem valor efetivo.

Não apagar inventário atual automaticamente — coberto por teste
(`..._KeepRecentActiveAndInventory`): devices/locations nunca entram no
cleanup.

## Testes

- Unit
- Integration
- Architecture
- Contract
- E2E
- Concurrency
- Security

Também testar:

- Outbox;
- idempotência;
- cache fallback;
- rate limit;
- retry;
- circuit breaker;
- DLQ;
- notificações.

## CI/CD

```text
PR
→ Build
→ Unit
→ Integration
→ Architecture
→ Security
→ Lint/Format
→ E2E
→ Approval
→ Docker
→ Staging
→ Production
```

Falhas críticas bloqueiam o processo.

## Implementação inicial da F0-T010

O workflow `.github/workflows/ci.yml` executa em pull requests para `main`, pushes em `main` e manualmente. A primeira versão cobre restore, build e testes da solução .NET, com PostgreSQL, Redis e RabbitMQ como serviços do job; também executa `npm ci` e o build do frontend React.

A validação de lint/format, Docker e ambientes de staging/produção permanece separada desta Task porque ainda não existem configuração de lint/format do frontend, imagens Docker versionadas ou ambientes de implantação definidos no repositório. A F0-T010 só deve ser marcada como concluída após uma execução remota bem-sucedida no GitHub Actions. Em 2026-09-21, as execuções foram encerradas antes dos steps, com `runner_name` vazio e sem logs. O aviso do GitHub confirmou o motivo: a conta atingiu 2.000/2.000 minutos incluídos, com bloqueio de uso excedente e renovação prevista para 01/10/2026.
