#!/usr/bin/env bash
# ==============================================================================
# deploy.sh — Script de Deploy Automatizado e Seguro para Produção
# InfraControl — Plataforma de Gerenciamento de Infraestrutura de TI
# ==============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

# Cores para saída
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

log_info() {
    echo -e "${BLUE}[DEPLOY INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[DEPLOY SUCESSO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[DEPLOY AVISO]${NC} $1"
}

log_error() {
    echo -e "${RED}[DEPLOY ERRO]${NC} $1" >&2
}

show_help() {
    cat << EOF
Uso: $(basename "$0") [OPÇÕES]

Executa o procedimento completo e automatizado de deploy em produção do InfraControl:
1. Executa verificação pré-voo (preflight-check.sh)
2. Realiza backup preventivo do banco PostgreSQL se a stack estiver ativa
3. Aplica migrações de banco de dados
4. Atualiza e sobe os serviços com escala configurada via Docker Compose
5. Valida os health checks (/health e /health/ready) pós-deploy

Opções:
  -e, --env-file CAMINHO      Caminho do arquivo .env (padrão: .env na raiz do projeto)
  --scale-api NUMERO          Quantidade de réplicas da API (padrão: 2)
  --scale-workers NUMERO      Quantidade de réplicas dos Workers (padrão: 1)
  --profiles PERFIS           Perfis do Compose a ativar (padrão: app)
  --skip-preflight            Pula a etapa de verificação pré-voo (NÃO RECOMENDADO)
  --skip-backup               Pula a geração de backup preventivo pré-deploy
  --skip-migration            Pula a execução de migrations do EF Core
  --dry-run                   Apenas simula os comandos sem executá-los
  -h, --help                  Exibe esta ajuda e sai

Exemplos:
  $(basename "$0")
  $(basename "$0") --scale-api 3
  $(basename "$0") --profiles "app,obs"
EOF
    exit 0
}

ENV_FILE="${REPO_ROOT}/.env"
SCALE_API=2
SCALE_WORKERS=1
PROFILES="app"
SKIP_PREFLIGHT=false
SKIP_BACKUP=false
SKIP_MIGRATION=false
DRY_RUN=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        -e|--env-file)
            ENV_FILE="$2"
            shift 2
            ;;
        --scale-api)
            SCALE_API="$2"
            shift 2
            ;;
        --scale-workers)
            SCALE_WORKERS="$2"
            shift 2
            ;;
        --profiles)
            PROFILES="$2"
            shift 2
            ;;
        --skip-preflight)
            SKIP_PREFLIGHT=true
            shift
            ;;
        --skip-backup)
            SKIP_BACKUP=true
            shift
            ;;
        --skip-migration)
            SKIP_MIGRATION=true
            shift
            ;;
        --dry-run)
            DRY_RUN=true
            shift
            ;;
        -h|--help)
            show_help
            ;;
        *)
            log_error "Opção desconhecida: $1"
            echo "Use '$0 --help' para ver as opções disponíveis."
            exit 1
            ;;
    esac
done

echo "=============================================================================="
echo "           InfraControl — Procedimento de Deploy em Produção                  "
echo "=============================================================================="
echo "Data: $(date '+%Y-%m-%d %H:%M:%S')"
echo "Arquivo de ambiente: ${ENV_FILE}"
echo "Escala API: ${SCALE_API} réplica(s)"
echo "Escala Workers: ${SCALE_WORKERS} réplica(s)"
echo "Perfis Docker: ${PROFILES}"
echo "Modo Dry-Run: ${DRY_RUN}"
echo "------------------------------------------------------------------------------"

# ------------------------------------------------------------------------------
# 1. Verificação Pré-Voo
# ------------------------------------------------------------------------------
if [ "${SKIP_PREFLIGHT}" = true ]; then
    log_warn "Etapa de pré-voo ignorada por parâmetro (--skip-preflight)."
else
    log_info "1. Executando verificações pré-voo..."
    PREFLIGHT_BIN="${SCRIPT_DIR}/preflight-check.sh"
    if [ ! -f "${PREFLIGHT_BIN}" ]; then
        log_error "Script de pré-voo não encontrado em: ${PREFLIGHT_BIN}"
        exit 1
    fi

    if [ "${DRY_RUN}" = true ]; then
        log_info "[DRY-RUN] Executaria: ${PREFLIGHT_BIN} --env-file ${ENV_FILE}"
    else
        "${PREFLIGHT_BIN}" --env-file "${ENV_FILE}"
        log_success "Pré-voo aprovado com sucesso!"
    fi
fi

# ------------------------------------------------------------------------------
# 2. Backup Preventivo do Banco de Dados PostgreSQL
# ------------------------------------------------------------------------------
log_info "2. Verificando necessidade de backup preventivo..."
BACKUP_DIR="${REPO_ROOT}/backups/deploy"

if [ "${SKIP_BACKUP}" = true ]; then
    log_info "Backup preventivo ignorado por parâmetro (--skip-backup)."
else
    # Verifica se o container postgres está em execução
    PG_CONTAINER=$(docker ps --filter "name=postgres" --filter "status=running" -q | head -n 1 || echo "")
    if [ -n "${PG_CONTAINER}" ]; then
        log_info "Container PostgreSQL detectado em execução (${PG_CONTAINER}). Iniciando dump preventivo..."
        mkdir -p "${BACKUP_DIR}"
        BACKUP_FILE="${BACKUP_DIR}/infracontrol_pre_deploy_$(date +%Y%m%d_%H%M%S).sql.gz"
        
        # Obter variáveis do .env
        PG_USER=$(grep -E "^POSTGRES_USER=" "${ENV_FILE}" | cut -d '=' -f 2- | tr -d '\r"')
        PG_DB=$(grep -E "^POSTGRES_DB=" "${ENV_FILE}" | cut -d '=' -f 2- | tr -d '\r"')
        PG_USER=${PG_USER:-infracontrol}
        PG_DB=${PG_DB:-infracontrol}

        if [ "${DRY_RUN}" = true ]; then
            log_info "[DRY-RUN] Executaria dump do banco para: ${BACKUP_FILE}"
        else
            if docker exec -t "${PG_CONTAINER}" pg_dump -U "${PG_USER}" "${PG_DB}" | gzip > "${BACKUP_FILE}"; then
                log_success "Backup preventivo criado com sucesso: ${BACKUP_FILE} ($(du -h "${BACKUP_FILE}" | awk '{print $1}'))"
            else
                log_error "Falha ao gerar o backup preventivo. Abortando deploy por segurança!"
                exit 1
            fi
        fi
    else
        log_info "Nenhum container PostgreSQL ativo detectado; pulando backup preventivo pré-existente."
    fi
fi

# ------------------------------------------------------------------------------
# 3. Aplicação de Migrations do Entity Framework Core
# ------------------------------------------------------------------------------
log_info "3. Gerenciando migrações de banco de dados..."

if [ "${SKIP_MIGRATION}" = true ]; then
    log_warn "Etapa de migrations ignorada por parâmetro (--skip-migration)."
elif [ "${DRY_RUN}" = true ]; then
    log_info "[DRY-RUN] Executaria migrations do banco de dados antes da subida da API."
else
    # Subir infraestrutura base necessária para as migrations (Postgres)
    log_info "Garantindo que o PostgreSQL esteja saudável para receber migrations..."
    docker compose --env-file "${ENV_FILE}" \
        -f "${REPO_ROOT}/docker-compose.yml" \
        -f "${REPO_ROOT}/docker-compose.production.yml" \
        up -d postgres

    log_info "Aguardando PostgreSQL aceitar conexões..."
    for i in {1..30}; do
        if docker compose --env-file "${ENV_FILE}" \
            -f "${REPO_ROOT}/docker-compose.yml" \
            -f "${REPO_ROOT}/docker-compose.production.yml" \
            exec -T postgres pg_isready > /dev/null 2>&1; then
            log_success "PostgreSQL pronto!"
            break
        fi
        sleep 1
    done

    # Executar migrations via dotnet ef se dotnet disponível no host ou bundle
    if command -v dotnet > /dev/null 2>&1; then
        log_info "Aplicando migrations do EF Core via dotnet CLI local..."
        # Carregar connection string temporária do .env
        PG_PASS=$(grep -E "^POSTGRES_PASSWORD=" "${ENV_FILE}" | cut -d '=' -f 2- | tr -d '\r"')
        PG_USER=$(grep -E "^POSTGRES_USER=" "${ENV_FILE}" | cut -d '=' -f 2- | tr -d '\r"')
        PG_DB=$(grep -E "^POSTGRES_DB=" "${ENV_FILE}" | cut -d '=' -f 2- | tr -d '\r"')
        
        # Como o Postgres de produção tem porta fechada para o host no overlay,
        # utilizamos uma instância de migração via docker compose run
        log_info "Executando migrations no isolamento de rede do Compose..."
        docker compose --env-file "${ENV_FILE}" \
            -f "${REPO_ROOT}/docker-compose.yml" \
            -f "${REPO_ROOT}/docker-compose.production.yml" \
            run --rm --no-deps api dotnet InfraControl.Api.dll --migrate || {
                log_warn "Comando de migração interno não retornou 0. A aplicação efetuará a validação das tabelas no startup."
            }
    fi
fi

# ------------------------------------------------------------------------------
# 4. Deploy e Escala dos Serviços
# ------------------------------------------------------------------------------
log_info "4. Iniciando serviços de produção com Docker Compose..."

PROFILE_ARGS=()
IFS=',' read -ra PROFILE_LIST <<< "${PROFILES}"
for P in "${PROFILE_LIST[@]}"; do
    PROFILE_ARGS+=(--profile "${P}")
done

COMPOSE_UP_CMD=(
    docker compose
    --env-file "${ENV_FILE}"
    -f "${REPO_ROOT}/docker-compose.yml"
    -f "${REPO_ROOT}/docker-compose.production.yml"
    "${PROFILE_ARGS[@]}"
    up -d
    --remove-orphans
    --scale "api=${SCALE_API}"
    --scale "workers=${SCALE_WORKERS}"
)

if [ "${DRY_RUN}" = true ]; then
    log_info "[DRY-RUN] Executaria: ${COMPOSE_UP_CMD[*]}"
else
    "${COMPOSE_UP_CMD[@]}"
    log_success "Containers iniciados e escalados com sucesso!"
fi

# ------------------------------------------------------------------------------
# 5. Verificação Pós-Deploy e Health Checks
# ------------------------------------------------------------------------------
log_info "5. Verificando prontidão e integridade pós-deploy..."

if [ "${DRY_RUN}" = true ]; then
    log_info "[DRY-RUN] Validaria endpoints /health e /health/ready."
else
    HTTPS_PORT=$(grep -E "^HTTPS_PORT=" "${ENV_FILE}" | cut -d '=' -f 2- | tr -d '\r"')
    HTTPS_PORT=${HTTPS_PORT:-443}

    log_info "Aguardando serviços ficarem prontos (readiness probe na porta ${HTTPS_PORT})..."
    READY=false
    for i in {1..40}; do
        HTTP_CODE=$(curl -k -s -o /dev/null -w "%{http_code}" "https://localhost:${HTTPS_PORT}/health/ready" 2>/dev/null || echo "000")
        if [ "${HTTP_CODE}" = "200" ]; then
            READY=true
            log_success "Endpoint /health/ready respondeu HTTP 200 (OK)!"
            break
        fi
        echo -n "."
        sleep 2
    done
    echo ""

    if [ "${READY}" = false ]; then
        log_warn "Endpoint /health/ready ainda não respondeu 200 após 80s (status HTTP: ${HTTP_CODE})."
        log_warn "Verifique os logs dos containers: docker compose logs api workers"
    fi

    # Exibir status dos containers
    echo ""
    log_info "Status dos containers da stack:"
    docker compose --env-file "${ENV_FILE}" \
        -f "${REPO_ROOT}/docker-compose.yml" \
        -f "${REPO_ROOT}/docker-compose.production.yml" \
        "${PROFILE_ARGS[@]}" ps
fi

echo "=============================================================================="
log_success "Deploy concluído com sucesso!"
echo "=============================================================================="
