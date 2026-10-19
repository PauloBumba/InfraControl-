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

No Docker Compose, o `Backup:Path` é `/app/backups`, montado no volume
nomeado `postgres_backups`; assim os dumps sobrevivem à recriação do container
Workers. O volume está no mesmo host do PostgreSQL e não é um backup externo:
copie os dumps para outro destino com política de acesso/retenção própria e
valide restores regularmente.

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
Backup:Path, Backup:FileNamePrefix, Backup:TimeoutSeconds,
Backup:AdditionalTargets (lista de { ConnectionString, FileNamePrefix })
```

### Alvos adicionais (F34-T003, 2026-10-18)

O mesmo worker também derrama o banco do gateway de acesso remoto
(Guacamole, ADR-060). `Backup:AdditionalTargets` aceita
`{ ConnectionString, FileNamePrefix }`: cada alvo ganha dump com o
próprio prefixo, poda independente (`retention` por prefixo) e heartbeat
próprio em `backups/latest-{prefix}.json`; o alvo primário preserva
`latest.json`. Alvo com `ConnectionString` vazio é pulado; falha em
qualquer alvo torna o resultado da execução `Success=false` (o dump dos
demais alvos é mantido — agregado `Error`, logs individuais).

No compose, o Workers participa da rede `remote_access` para alcançar
`guacamole-db` por `pg_dump` (server-to-server, senha só via env
`Backup__AdditionalTargets__0__ConnectionString`). `AdditionalTargets`
vazio = comportamento byte a byte do F12-T003.

#### Correção do carrier (F34-T003, 2026-10-18)

O BackupWorker **nunca tinha funcionado em Docker**: a imagem do
Workers não trazia `pg_dump` (`Win32Exception (2): ... No such file or
directory`) e o default da Ubuntu noble (pg_dump 16) falharia com
`server version mismatch` contra os `postgres:17` (main e guacamole).
Evidência: `/app/backups` ficou vazio de 2026-10-15 até 2026-10-18.
A imagem agora instala `postgresql-client-17` do repo oficial PGDG
(docker/Dockerfile, stage `workers`). Com o fix, a 1ª execução gerou
`infracontrol-*.dump` + `guacamole-*.dump` + os dois heartbeats.

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

### Restauração do banco do gateway (F34-T003, 2026-10-18 — TESTADO)

Round-trip ao vivo no `guacamole-db` (dev), espelhando o critério do
CI:

1. marker de estado: `CREATE TABLE t003_restore_marker (x int)` +
   `INSERT ... VALUES (42)` no banco do gateway;
2. backup do worker (execução imediata do `BackupWorker`):
   `guacamole-20261019-012233.dump` (74 890 bytes);
3. destruição do estado: `docker compose --profile remote stop
   guacamole` + remoção do container e do volume `guacamole_db_data` +
   `up -d guacamole-db` (o `guacamole-init` reexecuta → baseline com 23
   tabelas `guacamole_*`; marker **AUSENTE** confirmado via psql);
4. restore: `docker exec -i infracontrol-guacamole-db pg_restore -U
   guacamole -d guacamole --clean --if-exists --no-owner < dump`
   (exit 0, sem erro);
5. verificação: marker = **42** restaurado, 23 tabelas, seed
   `guacadmin` presente;
6. `docker compose --profile remote start guacamole` → container
   healthy + token REST via HTTPS
   (`POST /guacamole/api/tokens`) → `authToken`.

Kill switch executado no mesmo dia (detalhes e senha de break-glass em
`docs/57-ACESSO-REMOTO-OPERACAO.md`).