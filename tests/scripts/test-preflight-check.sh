#!/usr/bin/env bash
# ==============================================================================
# test-preflight-check.sh
# Testes automatizados para o script de pré-voo de deploy (preflight-check.sh)
# ==============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
PREFLIGHT_SCRIPT="${REPO_ROOT}/scripts/deploy/preflight-check.sh"
TEMP_DIR=$(mktemp -d -t infracontrol-preflight-test-XXXXXX)

cleanup() {
    rm -rf "${TEMP_DIR}"
}
trap cleanup EXIT

FAILED=0
TESTS_RUN=0

assert_exit_code() {
    local expected="$1"
    local actual="$2"
    local desc="$3"
    TESTS_RUN=$((TESTS_RUN + 1))
    if [ "${actual}" -eq "${expected}" ]; then
        echo "  [PASS] ${desc}"
    else
        echo "  [FAIL] ${desc} (esperado: ${expected}, obtido: ${actual})"
        FAILED=$((FAILED + 1))
    fi
}

echo "=== Executando testes de preflight-check.sh ==="

# Teste 1: Flag --help deve retornar 0
set +e
"${PREFLIGHT_SCRIPT}" --help > /dev/null 2>&1
EC=$?
set -e
assert_exit_code 0 "${EC}" "Flag --help deve retornar 0"

# Teste 2: Arquivo .env inexistente deve falhar com código 1
set +e
"${PREFLIGHT_SCRIPT}" --env-file "${TEMP_DIR}/nonexistent.env" --skip-compose-check > /dev/null 2>&1
EC=$?
set -e
assert_exit_code 1 "${EC}" "Arquivo .env inexistente deve retornar 1"

# Teste 3: Arquivo .env com senhas padrão de dev deve falhar
cat << 'EOF' > "${TEMP_DIR}/dev_defaults.env"
POSTGRES_USER=infracontrol
POSTGRES_PASSWORD=change_me_in_production
POSTGRES_MAX_POOL_SIZE=20
REDIS_PASSWORD=replace_with_url_safe_secret
RABBITMQ_USER=infracontrol
RABBITMQ_PASSWORD=change_me_in_production
API_IMAGE=infracontrol-api:v1.0.0
WORKERS_IMAGE=infracontrol-workers:v1.0.0
FRONTEND_IMAGE=infracontrol-frontend:v1.0.0
API_CPUS=2
API_MEMORY=2G
WORKERS_CPUS=1
WORKERS_MEMORY=1G
POSTGRES_CPUS=2
POSTGRES_MEMORY=2G
REDIS_CPUS=1
REDIS_MEMORY=1G
REDIS_SENTINEL_CPUS=0.5
REDIS_SENTINEL_MEMORY=256M
RABBITMQ_CPUS=1
RABBITMQ_MEMORY=1G
FRONTEND_CPUS=1
FRONTEND_MEMORY=512M
TLS_CERT_FILE=/tmp/nonexistent.crt
TLS_KEY_FILE=/tmp/nonexistent.key
SEQ_AUTH_USER=admin
SEQ_AUTH_PASSWORD=admin
MOCK_DATA_ENABLED=false
EOF

set +e
"${PREFLIGHT_SCRIPT}" --env-file "${TEMP_DIR}/dev_defaults.env" --skip-compose-check > /dev/null 2>&1
EC=$?
set -e
assert_exit_code 1 "${EC}" "Arquivo com senhas padrão de dev deve retornar 1"

# Teste 4: Arquivo com MOCK_DATA_ENABLED=true deve falhar
cat << 'EOF' > "${TEMP_DIR}/mock_enabled.env"
POSTGRES_USER=infracontrol
POSTGRES_PASSWORD=SuperSecretPassword123!
POSTGRES_MAX_POOL_SIZE=20
REDIS_PASSWORD=SecureRedisSecret123
RABBITMQ_USER=infracontrol
RABBITMQ_PASSWORD=SuperSecretRabbit123!
API_IMAGE=infracontrol-api:v1.0.0
WORKERS_IMAGE=infracontrol-workers:v1.0.0
FRONTEND_IMAGE=infracontrol-frontend:v1.0.0
API_CPUS=2
API_MEMORY=2G
WORKERS_CPUS=1
WORKERS_MEMORY=1G
POSTGRES_CPUS=2
POSTGRES_MEMORY=2G
REDIS_CPUS=1
REDIS_MEMORY=1G
REDIS_SENTINEL_CPUS=0.5
REDIS_SENTINEL_MEMORY=256M
RABBITMQ_CPUS=1
RABBITMQ_MEMORY=1G
FRONTEND_CPUS=1
FRONTEND_MEMORY=512M
TLS_CERT_FILE=/tmp/cert.pem
TLS_KEY_FILE=/tmp/key.pem
SEQ_AUTH_USER=admin
SEQ_AUTH_PASSWORD=SuperSecretSeqPassword123!
MOCK_DATA_ENABLED=true
EOF

set +e
"${PREFLIGHT_SCRIPT}" --env-file "${TEMP_DIR}/mock_enabled.env" --skip-compose-check > /dev/null 2>&1
EC=$?
set -e
assert_exit_code 1 "${EC}" "MOCK_DATA_ENABLED=true deve falhar com código 1"

# Teste 5: Arquivo válido com certificados fictícios gerados deve passar (com --skip-compose-check)
DUMMY_CERT="${TEMP_DIR}/dummy.crt"
DUMMY_KEY="${TEMP_DIR}/dummy.key"
echo "-----BEGIN CERTIFICATE-----" > "${DUMMY_CERT}"
echo "MIIB..." >> "${DUMMY_CERT}"
echo "-----END CERTIFICATE-----" >> "${DUMMY_CERT}"

echo "-----BEGIN PRIVATE KEY-----" > "${DUMMY_KEY}"
echo "MIIE..." >> "${DUMMY_KEY}"
echo "-----END PRIVATE KEY-----" >> "${DUMMY_KEY}"
chmod 600 "${DUMMY_KEY}"

cat << EOF > "${TEMP_DIR}/valid.env"
POSTGRES_USER=infracontrol_prod
POSTGRES_PASSWORD=SuperSecretPostgresPassword2026_Secure!
POSTGRES_MAX_POOL_SIZE=20
REDIS_PASSWORD=SecureRedisPasswordUrlSafe2026
RABBITMQ_USER=infracontrol_mq
RABBITMQ_PASSWORD=SuperSecretRabbitPassword2026_Secure!
API_IMAGE=ghcr.io/infracontrol/api:1.0.0
WORKERS_IMAGE=ghcr.io/infracontrol/workers:1.0.0
FRONTEND_IMAGE=ghcr.io/infracontrol/frontend:1.0.0
API_CPUS=2
API_MEMORY=2G
WORKERS_CPUS=1
WORKERS_MEMORY=1G
POSTGRES_CPUS=2
POSTGRES_MEMORY=2G
REDIS_CPUS=1
REDIS_MEMORY=1G
REDIS_SENTINEL_CPUS=0.5
REDIS_SENTINEL_MEMORY=256M
RABBITMQ_CPUS=1
RABBITMQ_MEMORY=1G
FRONTEND_CPUS=1
FRONTEND_MEMORY=512M
TLS_CERT_FILE=${DUMMY_CERT}
TLS_KEY_FILE=${DUMMY_KEY}
SEQ_AUTH_USER=secops
SEQ_AUTH_PASSWORD=SuperSecretSeqAuthPassword2026_Secure!
MOCK_DATA_ENABLED=false
MOCK_DATA_PURGE=false
HTTP_PORT=18080
HTTPS_PORT=18443
EOF
chmod 600 "${TEMP_DIR}/valid.env"

set +e
"${PREFLIGHT_SCRIPT}" --env-file "${TEMP_DIR}/valid.env" --skip-compose-check > /dev/null 2>&1
EC=$?
set -e
assert_exit_code 0 "${EC}" "Configuração completa e válida deve passar com código 0"

# Teste 6: .env.production.example como base deve ser válido ao apontar certificados fictícios
cp "${REPO_ROOT}/.env.production.example" "${TEMP_DIR}/from_template.env"
# Substituir caminhos de certificado pelos temporários gerados no teste
sed -i "s|TLS_CERT_FILE=.*|TLS_CERT_FILE=${DUMMY_CERT}|" "${TEMP_DIR}/from_template.env"
sed -i "s|TLS_KEY_FILE=.*|TLS_KEY_FILE=${DUMMY_KEY}|" "${TEMP_DIR}/from_template.env"
chmod 600 "${TEMP_DIR}/from_template.env"

set +e
"${PREFLIGHT_SCRIPT}" --env-file "${TEMP_DIR}/from_template.env" --skip-compose-check > /dev/null 2>&1
EC=$?
set -e
assert_exit_code 0 "${EC}" "Template .env.production.example com certificados válidos deve passar com código 0"

echo ""
if [ "${FAILED}" -eq 0 ]; then
    echo "Todos os ${TESTS_RUN} testes passaram com sucesso!"
    exit 0
else
    echo "FALHA: ${FAILED} de ${TESTS_RUN} testes falharam."
    exit 1
fi
