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

## Restauração (F12-T004, 2026-09-25)

`pg_restore` a partir do `.dump`:

```text
pg_restore -h <host> -p <porta> -U <user> -d <banco> --no-owner <arquivo.dump>
```

**Critério do documento atendido:** o round-trip é validado
automaticamente por `RestoreRoundTripTests`
(`tests/InfraControl.IntegrationTests/Backup/`) a cada build — marker é
gravado no PostgreSQL real (Testcontainers), dumpado pelo
`PostgresBackupService` de verdade (`pg_dump -Fc`), restaurado em um
banco novo (`infracontrol_restore_test`) e verificado (linha marker,
contagem de linhas e contagem de tabelas do schema público iguais).
Assim o CI executa o "teste periódico de restore" em toda alteração.

Requisitos do host de teste/CI (decisão do usuário, 2026-09-30: "usa
banco com docker"): **Docker** — as ferramentas rodam de dentro da mesma
imagem `postgres:17-alpine` do Testcontainers via `docker run --rm`
(`DockerPgToolRunner`, traduz `-h localhost` → `host.docker.internal` e
o caminho do dump → `/backup` com bind mount). Binários locais no PATH
não são mais necessários. Se o Docker faltar, o teste **falha com
instrução** — nunca skip escondido.

Restauração manual em produção (mesmo caminho do runbook acima):
sempre validar com consulta de contagem ao final.