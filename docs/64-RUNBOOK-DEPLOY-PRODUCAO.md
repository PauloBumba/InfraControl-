# Runbook: Implantação e Operação em Produção (ADR-044 / ADR-045)

Este documento estabelece o procedimento operacional padrão (SOP) para implantação, atualização e manutenção do **InfraControl** em ambientes de produção on-premises utilizando Docker Compose com o overlay de produção (`docker-compose.production.yml`).

---

## 1. Requisitos Mínimos do Servidor (Host)

| Recurso | Mínimo Recomendado | Notas |
| :--- | :--- | :--- |
| **Sistema Operacional** | Linux x86_64 (Debian 12, Ubuntu 24.04 LTS ou RHEL 9+) | Kernel recente com suporte a cgroups v2 |
| **CPU** | 4 núcleos (mínimo 2 núcleos) | Para stack completa (API réplicas + Workers + Postgres + Redis HA) |
| **Memória RAM** | 8 GB (mínimo 4 GB) | Evitar OOM em picos de ingestão e inferência de IA |
| **Armazenamento** | 50 GB SSD / NVMe | Espaço para volumes persistentes e retenção de backups |
| **Docker** | Docker Engine >= 24.0 | Suporte ao Docker Compose v2 (plugin `docker compose`) |
| **Portas de Rede** | 80/TCP e 443/TCP abertas no firewall | O overlay expõe somente o proxy Nginx para a rede externa |

---

## 2. Preparação dos Certificados TLS/HTTPS

O Nginx exige certificados TLS válidos em produção para proteger cookies de sessão `Secure` e tokens Anti-CSRF (`ADR-017`).

### Opção A: Certificado Let's Encrypt (Certbot)
Se o servidor possuir FQDN público apontado:
```bash
sudo certbot certonly --standalone -d infracontrol.suaempresa.com.br
```
Os arquivos ficarão disponíveis em:
- Certificado completo: `/etc/letsencrypt/live/infracontrol.suaempresa.com.br/fullchain.pem`
- Chave privada: `/etc/letsencrypt/live/infracontrol.suaempresa.com.br/privkey.pem`

### Opção B: Certificado Corporativo / Autoridade Interna (PKI)
Copie o certificado e a chave para um diretório seguro do host:
```bash
sudo mkdir -p /etc/ssl/infracontrol
sudo cp seu_certificado_fullchain.crt /etc/ssl/infracontrol/server.crt
sudo cp sua_chave_privada.key /etc/ssl/infracontrol/server.key
sudo chmod 600 /etc/ssl/infracontrol/server.key
```

---

## 3. Configuração do Ambiente e Segredos

1. No diretório raiz da aplicação, gere o `.env` a partir do template de produção:
   ```bash
   cp .env.production.example .env
   chmod 600 .env
   ```

2. Edite o `.env` e preencha os segredos obrigatórios:
   - **`POSTGRES_PASSWORD`**: Senha forte única para o banco de produção.
   - **`REDIS_PASSWORD`**: Segredo forte URL-safe (apenas letras, números, `_` e `-`). Gere com:
     ```bash
     openssl rand -base64 24 | tr '+/' '_-' | tr -d '='
     ```
   - **`RABBITMQ_PASSWORD`**: Senha forte para mensageria.
   - **`SEQ_AUTH_PASSWORD`**: Senha para acesso autenticado aos logs no Nginx.
   - **`TLS_CERT_FILE`** e **`TLS_KEY_FILE`**: Caminhos absolutos para os arquivos do passo 2.
   - **`API_IMAGE`**, **`WORKERS_IMAGE`**, **`FRONTEND_IMAGE`**: Tags versionadas (ex.: `:v1.0.0`).
   - **`PRODUCTION_BOOTSTRAP_ADMIN_PASSWORD`**: Senha inicial do primeiro superadmin.
   - **`POSTGRES_MAX_POOL_SIZE`** (ex.: `20`): conexões máximas **por processo**
     da aplicação (api/workers). **`POSTGRES_MAX_CONNECTIONS`** (ex.: `200`):
     `max_connections` do servidor — o compose aplica via `command` do Postgres.
     Mantenha `réplicas × POSTGRES_MAX_POOL_SIZE + folga (migrations, backup,
     adminer, psql) ≤ POSTGRES_MAX_CONNECTIONS` — fórmula em
     `docs/40-SCALE-HORIZONTAL-OPERATIONS.md`; o pré-voo valida as variáveis e
     `tests/scripts/test-db-connection-budget.sh` cobre o wiring do compose.
   - **`MOCK_DATA_ENABLED=false`** e **`MOCK_DATA_PURGE=false`**: Obrigatório em produção.

---

## 4. Verificação Pré-Voo Automatizada

Antes de qualquer ação de deploy, execute o script de pré-voo:

```bash
./scripts/deploy/preflight-check.sh
```

O script validará:
- Disponibilidade do Docker e Compose v2;
- Recursos do host (CPU, RAM, disco);
- Restrição de permissões no arquivo `.env`;
- Não uso de credenciais de exemplo de desenvolvimento;
- Existência e permissões da chave TLS;
- Disponibilidade das portas 80/443;
- Validação sintática do Compose via `docker compose config`.

> [!IMPORTANT]
> Prossiga apenas se o preflight reportar **0 Erros**.

---

## 5. Execução do Deploy

Para realizar a implantação automatizada (com backup preventivo, migrations e subida escalada):

```bash
./scripts/deploy/deploy.sh --scale-api 2 --scale-workers 1
```

O script realizará em sequência:
1. Chamada automática do `preflight-check.sh`;
2. Dump preventivo do PostgreSQL (caso já exista base em execução);
3. Aplicação de migrations do EF Core;
4. Subida dos containers com overlay de produção e isolamento de portas internas;
5. Sondagem do health check em `https://localhost:443/health/ready` até obter HTTP 200.

---

## 6. Verificação Pós-Deploy e Saúde

Após o deploy, confirme o estado e a saúde dos componentes:

```bash
# 1. Status de todos os containers
docker compose -f docker-compose.yml -f docker-compose.production.yml ps

# 2. Resposta dos health checks
curl -k https://localhost/health/ready
curl -k https://localhost/health/live

# 3. Status do quorum do Redis Sentinel (ADR-045)
docker compose exec redis-sentinel-1 redis-cli -a "$REDIS_PASSWORD" sentinel ckquorum mymaster

# 4. Logs das réplicas da API
docker compose logs --tail=100 -f api
```

---

## 7. Procedimento de Atualização (Upgrade)

Quando houver uma nova versão das imagens:
1. Atualize as variáveis `API_IMAGE`, `WORKERS_IMAGE` e `FRONTEND_IMAGE` no `.env` para as novas tags.
2. Execute o deploy novamente:
   ```bash
   ./scripts/deploy/deploy.sh --scale-api 2
   ```
O script gerará um backup preventivo compactado em `backups/deploy/` antes de reiniciar os serviços.

---

## 8. Procedimento de Rollback e Contingência

Se uma atualização apresentar anomalia crítica:

1. **Restaurar versão anterior das imagens**:
   Edite o `.env` retornando as tags de imagem para a versão estável anterior.
2. **Restaurar banco de dados (se houve migration com quebra)**:
   ```bash
   # Parar réplicas da aplicação
   docker compose stop api workers

   # Restaurar o backup pré-deploy gerado
   gunzip -c backups/deploy/infracontrol_pre_deploy_TIMESTAMP.sql.gz | \
     docker compose exec -T postgres psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"
   ```
3. **Subir a versão anterior**:
   ```bash
   ./scripts/deploy/deploy.sh --skip-backup --skip-migration
   ```

---

## 9. Política de Backup e Rotação de Volumes

Os seguintes volumes nomeados armazenam o estado persistente do sistema:
- `postgres_data`: Dados do PostgreSQL
- `postgres_backups`: Backups automatizados do Worker
- `pdi-evidence` e `project-evidence`: Documentos anexados
- `api_data_protection`: Chaves de criptografia de sessão e cookies
- `zabbix_db_data`: Banco do Zabbix (se ativado)
- `guacamole_db_data`: Banco do Gateway de Acesso Remoto

Recomenda-se realizar snapshot ou cópia externa diária do diretório `/var/lib/docker/volumes/` para servidor de backup ou bucket seguro.
