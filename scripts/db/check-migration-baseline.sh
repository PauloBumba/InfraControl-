#!/usr/bin/env bash
# ==============================================================================
# check-migration-baseline.sh — AI-ENV-003
# Verifica que o schema de um ambiente bate com o código:
#   1) __EFMigrationsHistory == migrations do código (nenhuma faltando/orfã)
#   2) modelo == último snapshot (has-pending-model-changes)
#   3) opcional (--baseline-conn): diff completo de colunas/tabelas contra um
#      banco recém-criado do zero só com `dotnet ef database update`
#      (pega history "forjado" — migration registrada mas coluna ausente).
#
# Uso:
#   scripts/db/check-migration-baseline.sh
#   scripts/db/check-migration-baseline.sh \
#     --conn "Host=localhost;Port=5433;Database=infracontrol;Username=infracontrol;Password=..." \
#     --baseline-conn "Host=localhost;Port=5433;Database=infracontrol_baseline;..."
#
# SAI 0 = ok · 1 = drift · 77 = ambiente indisponível (skip).
# ATENÇÃO (armadilha real de dev): o compose mapeia Postgres na porta 5433;
# a 5432 pode ser OUTRO Postgres do host — conferir `docker port <container>`.
# ==============================================================================
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

CONN="${CHECK_BASELINE_CONN:-Host=localhost;Port=5433;Database=infracontrol;Username=infracontrol;Password=infracontrol_dev_2026}"
BASELINE_CONN=""
while [ $# -gt 0 ]; do
  case "$1" in
    --conn) CONN="$2"; shift 2 ;;
    --baseline-conn) BASELINE_CONN="$2"; shift 2 ;;
    *) echo "Argumento desconhecido: $1" >&2; exit 1 ;;
  esac
done

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; NC='\033[0m'
FAILURES=0

pass() { echo -e "${GREEN}[PASS]${NC} $1"; }
fail() { echo -e "${RED}[FAIL]${NC} $1"; FAILURES=$((FAILURES + 1)); }
skip() { echo -e "${YELLOW}[SKIP]${NC} $1"; exit 77; }

# ── pré-requisitos ────────────────────────────────────────────────────────────
command -v dotnet >/dev/null 2>&1 || skip "dotnet CLI indisponível"
command -v psql >/dev/null 2>&1 || skip "psql indisponível"

pgpw() { printf '%s' "$1" | sed -n 's/.*Password=\([^;]*\).*/\1/p'; }
to_pgconn() { printf '%s' "$1" | sed -e 's/Password=[^;]*;*//' -e 's/[;,]/\n/g' -e 's/^Host=/host=/' -e 's/Port=/port=/' -e 's/Database=/dbname=/' -e 's/Username=/user=/' | tr '\n' ' ' | sed 's/ $//'; }

db_ok() {
  PGPASSWORD="$(pgpw "$1")" psql "$(to_pgconn "$1")" -tAc "SELECT 1" 2>/dev/null
}
[ "$(db_ok "$CONN")" = "1" ] || skip "banco do ambiente não responde ($CONN)"

# ── 1) history == código ─────────────────────────────────────────────────────
cd "$REPO_ROOT" || exit 1
dotnet ef migrations list -p src/InfraControl.Infrastructure -s src/InfraControl.Api 2>/dev/null \
  | grep -oE "[0-9]{14}_[A-Za-z0-9]+" | LC_ALL=C sort -u > /tmp/.baseline_code.$$ || true
psql_conn() { PGPASSWORD="$(pgpw "$1")" psql "$(to_pgconn "$1")" -tAc "$2"; }
psql_conn "$CONN" 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY 1;' \
  | grep -oE "[0-9]{14}_[A-Za-z0-9]+" | LC_ALL=C sort -u > /tmp/.baseline_db.$$ || true

MISSING=$(LC_ALL=C comm -23 /tmp/.baseline_code.$$ /tmp/.baseline_db.$$ | tr '\n' ' ')
ORPHAN=$(LC_ALL=C comm -13 /tmp/.baseline_code.$$ /tmp/.baseline_db.$$ | tr '\n' ' ')
if [ -z "$MISSING" ] && [ -z "$ORPHAN" ]; then
  pass "history == código ($(wc -l < /tmp/.baseline_db.$$) migrações)"
else
  [ -n "$MISSING" ] && fail "migrações no código e AUSENTES no banco: $MISSING"
  [ -n "$ORPHAN" ] && fail "migrações no banco e INEXISTENTES no código: $ORPHAN"
fi
rm -f /tmp/.baseline_code.$$ /tmp/.baseline_db.$$

# ── 2) modelo == snapshot ────────────────────────────────────────────────────
if dotnet ef migrations has-pending-model-changes \
    -p src/InfraControl.Infrastructure -s src/InfraControl.Api >/dev/null 2>&1; then
  pass "modelo sem mudanças pendentes desde a última migração"
else
  fail "modelo difere do snapshot — rode 'dotnet ef migrations add' antes de commitar"
fi

# ── 3) diff opcional de schema vs baseline recém-criado ──────────────────────
if [ -n "$BASELINE_CONN" ]; then
  if [ "$(db_ok "$BASELINE_CONN")" != "1" ]; then
    fail "baseline não responde — crie com: docker exec infracontrol-postgres psql -U infracontrol -c 'CREATE DATABASE infracontrol_baseline OWNER infracontrol;' && dotnet ef database update --connection \"...\""
  else
    Q="SELECT table_name||'.'||column_name FROM information_schema.columns WHERE table_schema='public' ORDER BY 1;"
    psql_conn "$CONN" "$Q" | LC_ALL=C sort -u > /tmp/.cols_env.$$ || true
    psql_conn "$BASELINE_CONN" "$Q" | LC_ALL=C sort -u > /tmp/.cols_base.$$ || true
    QT="SELECT table_name FROM information_schema.tables WHERE table_schema='public' ORDER BY 1;"
    psql_conn "$CONN" "$QT" | LC_ALL=C sort -u > /tmp/.tabs_env.$$ || true
    psql_conn "$BASELINE_CONN" "$QT" | LC_ALL=C sort -u > /tmp/.tabs_base.$$ || true
    if diff -q /tmp/.cols_env.$$ /tmp/.cols_base.$$ >/dev/null \
      && diff -q /tmp/.tabs_env.$$ /tmp/.tabs_base.$$ >/dev/null; then
      pass "schema do ambiente == baseline do zero ($(wc -l < /tmp/.cols_env.$$) colunas)"
    else
      fail "drift de schema vs baseline:"
      LC_ALL=C comm -3 /tmp/.cols_env.$$ /tmp/.cols_base.$$ | sed 's/^/    /' | head -20
      LC_ALL=C comm -3 /tmp/.tabs_env.$$ /tmp/.tabs_base.$$ | sed 's/^/    tabela: /' | head -20
    fi
    rm -f /tmp/.cols_env.$$ /tmp/.cols_base.$$ /tmp/.tabs_env.$$ /tmp/.tabs_base.$$
  fi
fi

if [ "$FAILURES" -gt 0 ]; then
  echo -e "${RED}Drift detectado em $FAILURES checagem(ns).${NC}"
  exit 1
fi
echo -e "${GREEN}Baseline OK.${NC}"
