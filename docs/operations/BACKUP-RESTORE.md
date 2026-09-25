# Backup e Restore

Backup não é Cleanup.

## Deve existir

- backup periódico do PostgreSQL;
- armazenamento protegido;
- política de retenção;
- monitoramento do backup;
- teste periódico de restore.

## Critério

Um backup só deve ser considerado confiável quando o restore tiver sido validado.

## Implementação (F12-T003, 2026-09-25)

Backup periódico via `BackupWorker` (InfraControl.Workers) que chama o
`PostgresBackupService` — decide o usuário: **worker .NET**, diretório
local, retenção default 7 dias.

- `pg_dump -Fc` (formato custom comprimido) — a senha vai **só** no
  ambiente (`PGPASSWORD`, vinda do `ConnectionStrings:DefaultConnection`/
  env), nunca em argv nem em config nova no git.
- Arquivos: `{prefix}-{aaaammdd-hhmmss}.dump` em `Backup:Path`
  (default `backups/`), prefixo `Backup:FileNamePrefix`
  (default `infracontrol`).
- Retenção: arquivos do prefixo com mais de `Backup:RetentionDays`
  (default 7) são apagados a cada execução (contagem vai no log).
- Monitoramento: log `Information` no sucesso (arquivo, bytes, podados)
  e `Warning`/`Error` na falha; heartbeat em `backups/latest.json`
  (`file`, `completedAtUtc`, `durationMs`, `sizeBytes`).
- Falha/timeout (`Backup:TimeoutSeconds`, default 3600): arquivo parcial
  é apagado, `latest.json` não é tocado — o último status válido fica.
- `Backup:Enabled=false` desliga o worker; `Backup:IntervalHours`
  (default 24) define o período (1ª execução imediata).

Config (Workers appsettings, sobrescrever por env em produção):

```text
Backup:Enabled, Backup:IntervalHours, Backup:RetentionDays,
Backup:Path, Backup:FileNamePrefix, Backup:TimeoutSeconds
```

### Runbook — backup falhando

- **Diagnóstico**: log do Workers ("Backup run failed"/"Backup failed");
  `backups/latest.json` com `completedAtUtc` antigo; tentar
  `pg_dump -Fc -f /tmp/test.dump` manualmente com a mesma connection.
- **Impacto**: sem backup novo; o último arquivo válido continua em
  disco (nada é apagado sem backup novo bem-sucedido — a poda só roda
  no sucesso).
- **Ações seguras**: verificar Postgres de pé e credenciais
  (env `ConnectionStrings__DefaultConnection`); verificar espaço em
  disco e existência do binário `pg_dump` no host do Workers; reiniciar
  o Workers.
- **Validação**: `latest.json` com `completedAtUtc` recente + log
  `Backup completed` + arquivo `.dump` com tamanho > 0.

## Restauração

`pg_restore` a partir do `.dump` (F12-T004 — teste periódico de restore
ainda pendente; **um backup só é confiável depois que o restore for
validado**).