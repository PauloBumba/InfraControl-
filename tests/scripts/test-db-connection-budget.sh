#!/usr/bin/env bash
# ==============================================================================
# test-db-connection-budget.sh
# Orçamento de conexões do Postgres (D1 — docs/66): pool explícito por
# processo + max_connections elevado, com fórmula verificável.
#   bash tests/scripts/test-db-connection-budget.sh   → exit 0 (tudo ok) / 1
# ==============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
COMPOSE="${REPO_ROOT}/docker-compose.yml"
PROD_COMPOSE="${REPO_ROOT}/docker-compose.production.yml"
ENV_EXAMPLE="${REPO_ROOT}/.env.example"
ENV_PROD_EXAMPLE="${REPO_ROOT}/.env.production.example"
API_SETTINGS="${REPO_ROOT}/src/InfraControl.Api/appsettings.json"
WORKERS_SETTINGS="${REPO_ROOT}/src/InfraControl.Workers/appsettings.json"

FAILED=0
TESTS_RUN=0

pass() { TESTS_RUN=$((TESTS_RUN + 1)); echo "  [PASS] $1"; }
fail() { TESTS_RUN=$((TESTS_RUN + 1)); FAILED=$((FAILED + 1)); echo "  [FAIL] $1"; }

assert_grep() {
    local pattern="$1" file="$2" desc="$3"
    if grep -qE "${pattern}" "${file}"; then pass "${desc}"; else fail "${desc} (padrão '${pattern}' ausente em ${file##*/})"; fi
}

assert_not_grep() {
    local pattern="$1" file="$2" desc="$3"
    if grep -qE "${pattern}" "${file}"; then fail "${desc} (padrão '${pattern}' presente em ${file##*/})"; else pass "${desc}"; fi
}

echo "=== Orçamento de conexões Postgres (D1) ==="

# ── 1. Compose base: max_connections configurável ─────────────────────────────
assert_grep 'max_connections=\$\{POSTGRES_MAX_CONNECTIONS:-[0-9]+\}' "${COMPOSE}" \
    "compose base: postgres usa max_connections via POSTGRES_MAX_CONNECTIONS"

# ── 2. Compose base: pool explícito no anchor compartilhado (api + workers) ──
assert_grep 'ConnectionStrings__DefaultConnection:.*Maximum Pool Size=\$\{POSTGRES_MAX_POOL_SIZE:-[0-9]+\}' "${COMPOSE}" \
    "compose base: connection string do anchor tem Maximum Pool Size via POSTGRES_MAX_POOL_SIZE"
assert_grep '<<: \*app-env' "${COMPOSE}" \
    "compose base: workers herdam o anchor app-env (sem connection string própria sem pool)"

# ── 3. Overlay de produção: pool obrigatório (já existente — regressão) ──────
assert_grep 'ConnectionStrings__DefaultConnection:.*Maximum Pool Size=\$\{POSTGRES_MAX_POOL_SIZE:\?' "${PROD_COMPOSE}" \
    "overlay produção: Maximum Pool Size obrigatório via POSTGRES_MAX_POOL_SIZE"

# ── 4. Templates de env declaram as duas variáveis ───────────────────────────
assert_grep '^POSTGRES_MAX_POOL_SIZE=[0-9]+' "${ENV_EXAMPLE}" \
    ".env.example declara POSTGRES_MAX_POOL_SIZE"
assert_grep '^POSTGRES_MAX_CONNECTIONS=[0-9]+' "${ENV_EXAMPLE}" \
    ".env.example declara POSTGRES_MAX_CONNECTIONS"
assert_grep '^POSTGRES_MAX_POOL_SIZE=[0-9]+' "${ENV_PROD_EXAMPLE}" \
    ".env.production.example declara POSTGRES_MAX_POOL_SIZE"
assert_grep '^POSTGRES_MAX_CONNECTIONS=[0-9]+' "${ENV_PROD_EXAMPLE}" \
    ".env.production.example declara POSTGRES_MAX_CONNECTIONS"

# ── 5. appsettings (roda fora do compose) fixa pool ──────────────────────────
assert_grep 'Maximum Pool Size=[0-9]+' "${API_SETTINGS}" \
    "appsettings da API tem Maximum Pool Size local"
assert_grep 'Maximum Pool Size=[0-9]+' "${WORKERS_SETTINGS}" \
    "appsettings dos Workers tem Maximum Pool Size local"

# ── 6. Matemática do orçamento com os defaults do compose ────────────────────
# fórmula (docs/40): réplicas_api×pool + réplicas_workers×pool + folga
# ≤ max_connections. Defaults do compose: 1 api + 1 workers (profile app).
pool_default=$(grep -oE 'POSTGRES_MAX_POOL_SIZE:-[0-9]+' "${COMPOSE}" | head -1 | grep -oE '[0-9]+' || true)
maxconn_default=$(grep -oE 'POSTGRES_MAX_CONNECTIONS:-[0-9]+' "${COMPOSE}" | head -1 | grep -oE '[0-9]+' || true)
if [ -n "${pool_default}" ] && [ -n "${maxconn_default}" ]; then
    instances=2   # api + workers
    reserve=50    # migrations, backup (pg_dump), adminer, psql, guacamole-db não usa este PG
    needed=$((instances * pool_default + reserve))
    if [ "${maxconn_default}" -ge "${needed}" ]; then
        pass "defaults: max_connections(${maxconn_default}) ≥ 2×pool(${pool_default})+folga(${reserve}) = ${needed}"
    else
        fail "defaults: max_connections(${maxconn_default}) < necessário (${needed} = 2×${pool_default}+${reserve})"
    fi
else
    fail "não consegui extrair defaults de pool/max_connections do compose"
fi

# ── 7. Produção: .env.template coerente (pool ≥ 5 e max_connections > pool) ──
pool_prod=$(grep -oE '^POSTGRES_MAX_POOL_SIZE=[0-9]+' "${ENV_PROD_EXAMPLE}" | grep -oE '[0-9]+' || true)
maxconn_prod=$(grep -oE '^POSTGRES_MAX_CONNECTIONS=[0-9]+' "${ENV_PROD_EXAMPLE}" | grep -oE '[0-9]+' || true)
if [ -n "${pool_prod}" ] && [ -n "${maxconn_prod}" ]; then
    if [ "${pool_prod}" -ge 5 ] && [ "${maxconn_prod}" -gt "${pool_prod}" ]; then
        pass "produção: pool(${pool_prod}) ≥ 5 e max_connections(${maxconn_prod}) > pool"
    else
        fail "produção: pool=${pool_prod} / max_connections=${maxconn_prod} incoerentes (pool ≥ 5 e max_connections > pool)"
    fi
else
    fail "produção: POSTGRES_MAX_POOL_SIZE/POSTGRES_MAX_CONNECTIONS ausentes no template"
fi

# ── 8. Sem pool default escondido do Npgsql no compose base ──────────────────
# a connection string do anchor DEVE limitar o pool (bug D1: default 100 do Npgsql)
if grep -A2 'ConnectionStrings__DefaultConnection' "${COMPOSE}" | grep -q 'Maximum Pool Size'; then
    pass "compose base: nenhuma connection string sem Maximum Pool Size no anchor"
else
    fail "compose base: connection string do anchor sem Maximum Pool Size"
fi

echo
if [ "${FAILED}" -eq 0 ]; then
    echo "OK: ${TESTS_RUN} verificações aprovadas."
    exit 0
else
    echo "FALHA: ${FAILED} de ${TESTS_RUN} verificações falharam."
    exit 1
fi
