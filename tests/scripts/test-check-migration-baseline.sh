#!/usr/bin/env bash
# ==============================================================================
# test-check-migration-baseline.sh
# Testes automatizados para scripts/db/check-migration-baseline.sh (AI-ENV-003)
#
# Sem ambiente (Postgres do compose de pé) os testes de integração são PULADOS
# com aviso — não falham em CI sem docker. A sintaxe e o comportamento de
# skip (77) são sempre verificados.
# ==============================================================================
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
CHECK_SCRIPT="${REPO_ROOT}/scripts/db/check-migration-baseline.sh"

FAILED=0
TESTS_RUN=0

assert_exit_code() {
    local expected="$1" actual="$2" desc="$3"
    TESTS_RUN=$((TESTS_RUN + 1))
    if [ "${actual}" -eq "${expected}" ]; then
        echo "  [PASS] ${desc}"
    else
        echo "  [FAIL] ${desc} (esperado: ${expected}, obtido: ${actual})"
        FAILED=$((FAILED + 1))
    fi
}

echo "=== Executando testes de check-migration-baseline.sh ==="

# Teste 1: sintaxe válida
set +e
bash -n "${CHECK_SCRIPT}" 2>/dev/null
EC=$?
set -e
assert_exit_code 0 "${EC}" "sintaxe bash válida (bash -n)"

# Teste 2: banco inacessível → skip (77), nunca crash
set +e
"${CHECK_SCRIPT}" --conn "Host=localhost;Port=5999;Database=nao_existe;Username=x;Password=x" >/dev/null 2>&1
EC=$?
set -e
assert_exit_code 77 "${EC}" "banco inacessível retorna 77 (skip)"

# Teste 3: argumento desconhecido → 1
set +e
"${CHECK_SCRIPT}" --flag-invalida >/dev/null 2>&1
EC=$?
set -e
assert_exit_code 1 "${EC}" "argumento desconhecido retorna 1"

# ── integração (ambiente dev de pé) ───────────────────────────────────────────
CONN="${CHECK_BASELINE_CONN:-Host=localhost;Port=5433;Database=infracontrol;Username=infracontrol;Password=infracontrol_dev_2026}"
PGPW="$(printf '%s' "$CONN" | sed -n 's/.*Password=\([^;]*\).*/\1/p')"
PGURL="$(printf '%s' "$CONN" | sed -e 's/Password=[^;]*;*//' -e 's/[;,]/\n/g' -e 's/^Host=/host=/' -e 's/Port=/port=/' -e 's/Database=/dbname=/' -e 's/Username=/user=/' | tr '\n' ' ' | sed 's/ $//')"
if command -v psql >/dev/null 2>&1 && PGPASSWORD="${PGPW}" psql "${PGURL}" -tAc "SELECT 1" >/dev/null 2>&1; then
    # Teste 4: baseline do ambiente dev real → 0
    set +e
    "${CHECK_SCRIPT}" >/dev/null 2>&1
    EC=$?
    set -e
    assert_exit_code 0 "${EC}" "ambiente dev sem drift (history+modelo) retorna 0"

    # Teste 5: drift artificial detectado → 1 (usa DB descartável, regride sempre)
    BASE_CONN="Host=localhost;Port=5433;Database=infracontrol_baseline_check;Username=infracontrol;Password=infracontrol_dev_2026"
    BASE_PGPW="$(printf '%s' "${BASE_CONN}" | sed -n 's/.*Password=\([^;]*\).*/\1/p')"
    BASE_PGURL="$(printf '%s' "${BASE_CONN}" | sed -e 's/Password=[^;]*;*//' -e 's/[;,]/\n/g' -e 's/^Host=/host=/' -e 's/Port=/port=/' -e 's/Database=/dbname=/' -e 's/Username=/user=/' | tr '\n' ' ' | sed 's/ $//')"
    if PGPASSWORD="${BASE_PGPW}" psql "${BASE_PGURL}" -tAc "SELECT 1" >/dev/null 2>&1; then
        PGPASSWORD="${BASE_PGPW}" psql "${BASE_PGURL}" -c \
            "ALTER TABLE devices ADD COLUMN _baseline_test_drift text;" >/dev/null 2>&1
        set +e
        "${CHECK_SCRIPT}" --baseline-conn "${BASE_CONN}" >/dev/null 2>&1
        EC=$?
        set -e
        assert_exit_code 1 "${EC}" "drift de schema vs baseline retorna 1"
        PGPASSWORD="${BASE_PGPW}" psql "${BASE_PGURL}" -c \
            "ALTER TABLE devices DROP COLUMN _baseline_test_drift;" >/dev/null 2>&1
    else
        echo "  [SKIP] baseline descartável indisponível — teste de drift pulado"
    fi
else
    echo "  [SKIP] Postgres do compose indisponível — testes de integração pulados"
fi

echo ""
echo "=== Resultado: ${TESTS_RUN} teste(s), ${FAILED} falha(s) ==="
if [ "${FAILED}" -gt 0 ]; then
    exit 1
fi
exit 0
