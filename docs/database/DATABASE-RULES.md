# Regras de Banco

- PostgreSQL é source of truth.
- Usar constraints para integridade.
- Evitar FK polimórfica quando relações específicas forem possíveis.
- Migrations versionadas.
- Transactions para mudanças relacionadas.
- `FOR UPDATE SKIP LOCKED` para workers concorrentes quando aplicável.
- Índices devem ser criados com base em consultas reais.
- Não apagar histórico sem política de retenção.

## Baseline de schema (AI-ENV-003)

Política real de baseline — verificada em 2026-11-06 (dev: history 97/97,
850 colunas idênticas a um banco criado do zero, modelo sem mudanças
pendentes):

- **Ambiente persistente (dev, staging, produção) é gerenciado por
  migrations**: `dotnet ef database update`. Nunca `EnsureCreatedAsync` fora
  de bancos descartáveis.
- **Bancos de teste** (`BaseIntegrationTest`, `E2EHost`) usam
  `EnsureCreatedAsync` e são **descartáveis** — o schema vem do modelo, não
  é baseline de nada. Não sincronizar nem comparar com ambientes reais.
- **Novo ambiente**: criar o DB e rodar `database update` (cria
  `__EFMigrationsHistory` do zero) + seeds em `docs/database/`
  (`seed-demo-data.sql`, `seed-fictitious-devices.sql`).
- **Armadilha de porta**: o compose mapeia Postgres na **5433**; a 5432 pode
  ser outro Postgres do host. Conferir `docker port <container>` antes de
  qualquer `database update`/`psql`.
- **Nunca editar migração já aplicada**; histórico nunca é removido sem
  política de retenção. Drift corrigido com nova migração ou remendo
  aditivo idempotente documentado.
- **Checagem automatizada**: `scripts/db/check-migration-baseline.sh`
  (history == código + modelo == snapshot + opcional
  `--baseline-conn` para diff completo de colunas/tabelas contra um banco
  recém-criado do zero — pega history forjado). Testes:
  `bash tests/scripts/test-check-migration-baseline.sh` (5/5).
