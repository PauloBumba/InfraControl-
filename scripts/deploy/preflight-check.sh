#!/usr/bin/env bash
# ==============================================================================
# preflight-check.sh — Verificação Pré-Voo para Deploy em Produção
# InfraControl — Plataforma de Gerenciamento de Infraestrutura de TI
# ==============================================================================
set -u

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

# Cores para saída no terminal
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # Sem cor

ERRORS=0
WARNINGS=0
CHECKS=0

log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_pass() {
    CHECKS=$((CHECKS + 1))
    echo -e "  ${GREEN}✓ PASS:${NC} $1"
}

log_warn() {
    CHECKS=$((CHECKS + 1))
    WARNINGS=$((WARNINGS + 1))
    echo -e "  ${YELLOW}⚠ WARN:${NC} $1"
}

log_fail() {
    CHECKS=$((CHECKS + 1))
    ERRORS=$((ERRORS + 1))
    echo -e "  ${RED}✗ FAIL:${NC} $1"
}

show_help() {
    cat << EOF
Uso: $(basename "$0") [OPÇÕES]

Executa verificações prévias de ambiente, configurações, segredos e conectividade
antes de iniciar o deploy de produção do InfraControl via Docker Compose.

Opções:
  -e, --env-file CAMINHO    Caminho do arquivo .env (padrão: .env na raiz do projeto)
  -s, --skip-compose-check  Pula a validação estrutural via 'docker compose config'
  -h, --help                Exibe esta ajuda e sai

Exemplos:
  $(basename "$0")
  $(basename "$0") --env-file /etc/infracontrol/.env.production
  $(basename "$0") --skip-compose-check
EOF
    exit 0
}

ENV_FILE="${REPO_ROOT}/.env"
SKIP_COMPOSE_CHECK=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        -e|--env-file)
            ENV_FILE="$2"
            shift 2
            ;;
        -s|--skip-compose-check)
            SKIP_COMPOSE_CHECK=true
            shift
            ;;
        -h|--help)
            show_help
            ;;
        *)
            echo "Opção inválida: $1" >&2
            echo "Use '$0 --help' para opções disponíveis." >&2
            exit 1
            ;;
    esac
done

echo "=============================================================================="
echo "         InfraControl — Verificação Pré-Voo de Deploy em Produção             "
echo "=============================================================================="
echo "Data: $(date '+%Y-%m-%d %H:%M:%S')"
echo "Repositório: ${REPO_ROOT}"
echo "Arquivo de ambiente: ${ENV_FILE}"
echo "------------------------------------------------------------------------------"

# ------------------------------------------------------------------------------
# 1. Verificação de Ferramentas e Recursos do Host
# ------------------------------------------------------------------------------
log_info "1. Verificando ferramentas e recursos do host..."

if command -v docker > /dev/null 2>&1; then
    DOCKER_VER=$(docker --version 2>/dev/null || echo "detectado")
    log_pass "Docker instalado: ${DOCKER_VER}"
else
    log_fail "Docker não está instalado no host."
fi

if docker compose version > /dev/null 2>&1; then
    COMPOSE_VER=$(docker compose version --short 2>/dev/null || echo "v2")
    log_pass "Docker Compose v2 disponível: ${COMPOSE_VER}"
else
    log_fail "Docker Compose (plugin v2) não está disponível."
fi

# Checagem de CPU
CPU_CORES=$(nproc 2>/dev/null || echo "1")
if [ "${CPU_CORES}" -ge 2 ]; then
    log_pass "Núcleos de CPU disponíveis: ${CPU_CORES} (mínimo recomendado: 2)"
else
    log_warn "Host possui apenas ${CPU_CORES} núcleo(s) de CPU. Recomendado: >= 2 para stack completa."
fi

# Checagem de Memória RAM (em MB)
if [ -f /proc/meminfo ]; then
    MEM_TOTAL_KB=$(grep MemTotal /proc/meminfo | awk '{print $2}')
    MEM_TOTAL_MB=$((MEM_TOTAL_KB / 1024))
    if [ "${MEM_TOTAL_MB}" -ge 3800 ]; then
        log_pass "Memória RAM total: ${MEM_TOTAL_MB} MB (recomendado: >= 4096 MB)"
    else
        log_warn "Memória RAM total (${MEM_TOTAL_MB} MB) está abaixo do recomendado (4096 MB)."
    fi
fi

# Checagem de Espaço em Disco
DISK_AVAIL_KB=$(df -P "${REPO_ROOT}" | awk 'NR==2 {print $4}')
DISK_AVAIL_GB=$((DISK_AVAIL_KB / 1024 / 1024))
if [ "${DISK_AVAIL_GB}" -ge 10 ]; then
    log_pass "Espaço em disco disponível no volume do projeto: ${DISK_AVAIL_GB} GB (mínimo: 10 GB)"
else
    log_warn "Espaço livre em disco (${DISK_AVAIL_GB} GB) é inferior ao recomendado (10 GB)."
fi

# ------------------------------------------------------------------------------
# 2. Verificação do Arquivo .env e Permissões
# ------------------------------------------------------------------------------
log_info "2. Verificando arquivo de variáveis de ambiente..."

if [ ! -f "${ENV_FILE}" ]; then
    log_fail "Arquivo .env não encontrado em: ${ENV_FILE}"
    echo ""
    echo "=============================================================================="
    echo "RESULTADO: 1 falha crítica. Abortando verificações subsequentes."
    echo "Crie o arquivo ${ENV_FILE} a partir de .env.example ou .env.production.example."
    echo "=============================================================================="
    exit 1
fi

log_pass "Arquivo .env encontrado."

# Checagem de permissões
PERMS=$(stat -c "%a" "${ENV_FILE}" 2>/dev/null || stat -f "%OLp" "${ENV_FILE}" 2>/dev/null || echo "")
if [ -n "${PERMS}" ]; then
    if [ "${PERMS}" = "600" ] || [ "${PERMS}" = "400" ]; then
        log_pass "Permissões do .env estão restritas (${PERMS})."
    else
        log_warn "Permissões do .env estão em ${PERMS}. Recomenda-se executar: chmod 600 ${ENV_FILE}"
    fi
fi

# Carregar variáveis do .env com segurança (subshell para extrair valores)
get_env_val() {
    local var_name="$1"
    grep -E "^${var_name}=" "${ENV_FILE}" 2>/dev/null | head -n 1 | cut -d '=' -f 2- | tr -d '\r"' || echo ""
}

# ------------------------------------------------------------------------------
# 3. Verificação de Variáveis e Segredos de Produção (ADR-044)
# ------------------------------------------------------------------------------
log_info "3. Verificando segredos e parâmetros de produção..."

# Função de checagem de variável obrigatória
check_required_var() {
    local var="$1"
    local val
    val=$(get_env_val "${var}")
    if [ -z "${val}" ]; then
        log_fail "Variável obrigatória ausente ou vazia: ${var}"
        return 1
    else
        return 0
    fi
}

# PostgreSQL
if check_required_var "POSTGRES_USER"; then
    PG_USER=$(get_env_val "POSTGRES_USER")
    log_pass "POSTGRES_USER configurado (${PG_USER})."
fi

if check_required_var "POSTGRES_PASSWORD"; then
    PG_PASS=$(get_env_val "POSTGRES_PASSWORD")
    if [ "${PG_PASS}" = "change_me_in_production" ] || [ "${PG_PASS}" = "postgres" ] || [ "${PG_PASS}" = "password" ]; then
        log_fail "POSTGRES_PASSWORD utiliza senha insegura ou padrão de desenvolvimento."
    elif [ "${#PG_PASS}" -lt 12 ]; then
        log_warn "POSTGRES_PASSWORD possui menos de 12 caracteres. Recomendado >= 16 caracteres."
    else
        log_pass "POSTGRES_PASSWORD possui tamanho e complexidade adequados."
    fi
fi

PG_POOL=$(get_env_val "POSTGRES_MAX_POOL_SIZE")
if [ -n "${PG_POOL}" ]; then
    if [[ "${PG_POOL}" =~ ^[0-9]+$ ]] && [ "${PG_POOL}" -ge 5 ]; then
        log_pass "POSTGRES_MAX_POOL_SIZE configurado (${PG_POOL})."
    else
        log_fail "POSTGRES_MAX_POOL_SIZE inválido (${PG_POOL}). Deve ser um número inteiro >= 5."
    fi
else
    log_fail "POSTGRES_MAX_POOL_SIZE não configurado."
fi

# D1 (docs/66): max_connections do servidor deve existir e cobrir o pool.
# Ausente = o compose usa o default 200 (ok para 1 api + 1 workers com pool 20);
# presente precisa ser válido e maior que o pool por processo.
PG_MAXCONN=$(get_env_val "POSTGRES_MAX_CONNECTIONS")
if [ -z "${PG_MAXCONN}" ]; then
    log_warn "POSTGRES_MAX_CONNECTIONS não configurado — compose usará o default 200. Recomendado declarar explicitamente."
elif [[ "${PG_MAXCONN}" =~ ^[0-9]+$ ]] && [ "${PG_MAXCONN}" -gt "${PG_POOL:-0}" ]; then
    log_pass "POSTGRES_MAX_CONNECTIONS configurado (${PG_MAXCONN}) > pool por processo."
else
    log_fail "POSTGRES_MAX_CONNECTIONS inválido (${PG_MAXCONN}). Deve ser inteiro > POSTGRES_MAX_POOL_SIZE."
fi

# Redis (ADR-045: Sentinel URL-safe)
if check_required_var "REDIS_PASSWORD"; then
    REDIS_PASS=$(get_env_val "REDIS_PASSWORD")
    if [ "${REDIS_PASS}" = "replace_with_url_safe_secret" ] || [ "${REDIS_PASS}" = "redis" ]; then
        log_fail "REDIS_PASSWORD utiliza valor de exemplo."
    elif [[ ! "${REDIS_PASS}" =~ ^[a-zA-Z0-9_-]+$ ]]; then
        log_fail "REDIS_PASSWORD deve conter apenas caracteres URL-safe (letras, números, '_' e '-') para suporte ao Sentinel."
    elif [ "${#REDIS_PASS}" -lt 16 ]; then
        log_warn "REDIS_PASSWORD possui menos de 16 caracteres."
    else
        log_pass "REDIS_PASSWORD configurado com segredo forte URL-safe."
    fi
fi

# RabbitMQ
if check_required_var "RABBITMQ_USER"; then
    log_pass "RABBITMQ_USER configurado."
fi

if check_required_var "RABBITMQ_PASSWORD"; then
    MQ_PASS=$(get_env_val "RABBITMQ_PASSWORD")
    if [ "${MQ_PASS}" = "change_me_in_production" ] || [ "${MQ_PASS}" = "guest" ]; then
        log_fail "RABBITMQ_PASSWORD utiliza senha padrão de desenvolvimento."
    else
        log_pass "RABBITMQ_PASSWORD configurado."
    fi
fi

# Seq Basic Auth
if check_required_var "SEQ_AUTH_USER"; then
    log_pass "SEQ_AUTH_USER configurado."
fi

if check_required_var "SEQ_AUTH_PASSWORD"; then
    SEQ_PASS=$(get_env_val "SEQ_AUTH_PASSWORD")
    if [ "${SEQ_PASS}" = "admin" ]; then
        log_fail "SEQ_AUTH_PASSWORD não pode ser 'admin' em produção."
    else
        log_pass "SEQ_AUTH_PASSWORD configurado."
    fi
fi

# Imagens de Produção
for IMG_VAR in "API_IMAGE" "WORKERS_IMAGE" "FRONTEND_IMAGE"; do
    if check_required_var "${IMG_VAR}"; then
        IMG_VAL=$(get_env_val "${IMG_VAR}")
        if [[ "${IMG_VAL}" == *":latest"* ]] || [[ "${IMG_VAL}" != *":"* ]]; then
            log_warn "${IMG_VAR} está usando tag 'latest' ou sem tag explícita (${IMG_VAL}). Recomenda-se tag versionada imutável (ex.: :v1.0.0)."
        else
            log_pass "${IMG_VAR} versionada: ${IMG_VAL}"
        fi
    fi
done

# Limites de Recursos
RES_VARS=(
    "API_CPUS" "API_MEMORY"
    "WORKERS_CPUS" "WORKERS_MEMORY"
    "POSTGRES_CPUS" "POSTGRES_MEMORY"
    "REDIS_CPUS" "REDIS_MEMORY"
    "REDIS_SENTINEL_CPUS" "REDIS_SENTINEL_MEMORY"
    "RABBITMQ_CPUS" "RABBITMQ_MEMORY"
    "FRONTEND_CPUS" "FRONTEND_MEMORY"
)

RES_OK=true
for RV in "${RES_VARS[@]}"; do
    VAL=$(get_env_val "${RV}")
    if [ -z "${VAL}" ]; then
        log_fail "Limite de recurso não definido no .env: ${RV}"
        RES_OK=false
    fi
done
if [ "${RES_OK}" = true ]; then
    log_pass "Todos os limites de CPU e memória dos containers estão definidos."
fi

# Dados Mock (F46-T001)
MOCK_ENABLED=$(get_env_val "MOCK_DATA_ENABLED")
if [ "${MOCK_ENABLED}" = "true" ]; then
    log_fail "MOCK_DATA_ENABLED está como 'true'. Deve ser 'false' em produção para não gerar dados falsos."
else
    log_pass "MOCK_DATA_ENABLED desativado."
fi

MOCK_PURGE=$(get_env_val "MOCK_DATA_PURGE")
if [ "${MOCK_PURGE}" = "true" ]; then
    log_fail "MOCK_DATA_PURGE está como 'true'. Deve ser 'false' em produção."
else
    log_pass "MOCK_DATA_PURGE desativado."
fi

# ------------------------------------------------------------------------------
# 4. Verificação de Certificados TLS / HTTPS
# ------------------------------------------------------------------------------
log_info "4. Verificando certificados TLS..."

TLS_CERT=$(get_env_val "TLS_CERT_FILE")
TLS_KEY=$(get_env_val "TLS_KEY_FILE")

if [ -z "${TLS_CERT}" ]; then
    log_fail "TLS_CERT_FILE não está definido no .env."
elif [ ! -f "${TLS_CERT}" ]; then
    log_fail "Arquivo de certificado TLS não existe: ${TLS_CERT}"
elif [ ! -s "${TLS_CERT}" ]; then
    log_fail "Arquivo de certificado TLS está vazio: ${TLS_CERT}"
else
    log_pass "Arquivo de certificado TLS encontrado: ${TLS_CERT}"
fi

if [ -z "${TLS_KEY}" ]; then
    log_fail "TLS_KEY_FILE não está definido no .env."
elif [ ! -f "${TLS_KEY}" ]; then
    log_fail "Arquivo de chave privada TLS não existe: ${TLS_KEY}"
elif [ ! -s "${TLS_KEY}" ]; then
    log_fail "Arquivo de chave privada TLS está vazio: ${TLS_KEY}"
else
    KEY_PERM=$(stat -c "%a" "${TLS_KEY}" 2>/dev/null || stat -f "%OLp" "${TLS_KEY}" 2>/dev/null || echo "")
    if [ -n "${KEY_PERM}" ] && [ "${KEY_PERM}" != "600" ] && [ "${KEY_PERM}" != "400" ]; then
        log_warn "Chave privada TLS com permissões abertas (${KEY_PERM}). Recomenda-se: chmod 600 ${TLS_KEY}"
    else
        log_pass "Arquivo de chave privada TLS validado: ${TLS_KEY}"
    fi
fi

# Validação do certificado com openssl se disponível
if command -v openssl > /dev/null 2>&1 && [ -n "${TLS_CERT}" ] && [ -f "${TLS_CERT}" ] && [ -s "${TLS_CERT}" ]; then
    CERT_END=$(openssl x509 -enddate -noout -in "${TLS_CERT}" 2>/dev/null | cut -d '=' -f 2- || echo "")
    if [ -n "${CERT_END}" ]; then
        log_pass "Validade do certificado: ${CERT_END}"
        if ! openssl x509 -checkend 864000 -noout -in "${TLS_CERT}" 2>/dev/null; then
            log_warn "Certificado TLS expira em menos de 10 dias (${CERT_END})!"
        fi
    fi
fi

# ------------------------------------------------------------------------------
# 5. Verificação de Portas de Binding do Host
# ------------------------------------------------------------------------------
log_info "5. Verificando disponibilidade de portas de rede..."

HTTP_P=$(get_env_val "HTTP_PORT")
HTTP_P=${HTTP_P:-80}
HTTPS_P=$(get_env_val "HTTPS_PORT")
HTTPS_P=${HTTPS_P:-443}

check_port_free() {
    local port="$1"
    local desc="$2"
    if command -v ss > /dev/null 2>&1; then
        if ss -tulpn 2>/dev/null | grep -q ":${port} "; then
            log_warn "Porta ${port} (${desc}) parece já estar em uso por outro processo no host."
            return 1
        else
            log_pass "Porta ${port} (${desc}) livre."
            return 0
        fi
    fi
    return 0
}

check_port_free "${HTTP_P}" "HTTP Nginx"
check_port_free "${HTTPS_P}" "HTTPS Nginx"

# ------------------------------------------------------------------------------
# 6. Validação Estrutural do Docker Compose
# ------------------------------------------------------------------------------
log_info "6. Validando modelo Docker Compose com overlay de produção..."

if [ "${SKIP_COMPOSE_CHECK}" = true ]; then
    log_info "Validação do Compose ignorada por solicitação (--skip-compose-check)."
else
    COMPOSE_CMD="docker compose --env-file ${ENV_FILE} -f ${REPO_ROOT}/docker-compose.yml -f ${REPO_ROOT}/docker-compose.production.yml --profile app config --quiet"
    if COMPOSE_OUT=$(eval "${COMPOSE_CMD}" 2>&1); then
        log_pass "Modelo Docker Compose e substituição de variáveis validados com sucesso."
    else
        log_fail "Falha na validação do Docker Compose:"
        echo -e "${RED}${COMPOSE_OUT}${NC}"
    fi
fi

# ------------------------------------------------------------------------------
# Relatório Final
# ------------------------------------------------------------------------------
echo "------------------------------------------------------------------------------"
echo "RESUMO DA VERIFICAÇÃO PRÉ-VOO:"
echo "  Total de checagens: ${CHECKS}"
echo -e "  Aprovadas: ${GREEN}$((CHECKS - ERRORS - WARNINGS))${NC}"
echo -e "  Avisos:    ${YELLOW}${WARNINGS}${NC}"
echo -e "  Erros:     ${RED}${ERRORS}${NC}"
echo "=============================================================================="

if [ "${ERRORS}" -gt 0 ]; then
    echo -e "${RED}ERRO: O ambiente NÃO está pronto para deploy em produção.${NC}"
    echo "Corrija os ${ERRORS} erro(s) listados acima antes de prosseguir."
    exit 1
elif [ "${WARNINGS}" -gt 0 ]; then
    echo -e "${YELLOW}ATENÇÃO: O ambiente está apto para deploy, mas possui ${WARNINGS} aviso(s).${NC}"
    echo "Revise os avisos acima para garantir conformidade total com as boas práticas."
    exit 0
else
    echo -e "${GREEN}SUCESSO: Todas as verificações pré-voo foram aprovadas!${NC}"
    echo "O ambiente está 100% pronto para o deploy de produção."
    exit 0
fi
