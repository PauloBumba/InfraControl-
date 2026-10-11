# Operações de Escala Horizontal e Redis HA (ADR-044/045)

## Visão geral

Este documento descreve como operar a aplicação com escala horizontal usando Docker Compose, conforme ADR-044.

## Arquitetura de escala

```

Redis usa um primary, uma réplica e três Sentinels (ADR-045). Nesta etapa,
RabbitMQ e PostgreSQL continuam single-node; suas estratégias HA ficam para
etapas posteriores. O host `redis-master:6379` publicado serve para
diagnóstico/desenvolvimento, não para consumidores HA: API e Workers descobrem
o primary pelo serviço Sentinel `mymaster`.
Usuários → Nginx (load balancing) → API (N réplicas) → PostgreSQL, Redis, RabbitMQ
                                        ↓
                                    Workers (lock distribuído no Redis)
                                        ↓
                                    SignalR (Redis backplane)
                                        ↓
                                    IA (fila RabbitMQ → AiInferenceWorker → Ollama)
```

## Comandos de escala

### Escalar a API

```bash
# Escalar para 2 réplicas da API
docker compose --profile app up -d --scale api=2

# Escalar para 3 réplicas da API
docker compose --profile app up -d --scale api=3

# Escalar para 5 réplicas da API
docker compose --profile app up -d --scale api=5

# Voltar para 1 réplica
docker compose --profile app up -d --scale api=1
```

### Escalar os Workers

```bash
# Escalar workers para 2 réplicas (com lock distribuído no Redis)
docker compose --profile app up -d --scale workers=2

# Escalar workers para 3 réplicas
docker compose --profile app up -d --scale workers=3
```

### Escalar simultaneamente

```bash
# 3 réplicas da API + 2 workers
docker compose --profile app up -d --scale api=3 --scale workers=2
```

## Verificação do estado

```bash
# Ver containers e réplicas
docker compose ps

# Ver primary e quorum descobertos pelos Sentinels
docker compose --profile app exec redis-sentinel-1 redis-cli -a "$REDIS_PASSWORD" sentinel get-master-addr-by-name mymaster
docker compose --profile app exec redis-sentinel-1 redis-cli -a "$REDIS_PASSWORD" sentinel ckquorum mymaster

# Ver logs de todas as réplicas e os IDs individuais
docker compose --profile app logs api
docker compose --profile app ps -q api

# Ver logs do nginx (load balancing)
docker compose logs frontend

# Ver health checks
curl -k https://localhost:8443/health
curl -k https://localhost:8443/health/ready
```

## Produção em host único

O Compose base mantém portas úteis para desenvolvimento. Para implantação,
use o overlay de produção, que remove a publicação direta de PostgreSQL,
Redis/Sentinel, RabbitMQ, Adminer e endpoints internos de observabilidade.
Somente Nginx publica HTTP/HTTPS.

1. Copie `.env.example` para `.env`, restrinja o arquivo (`chmod 600 .env`) e
   preencha senhas, tags versionadas das imagens, caminhos absolutos para um
   certificado válido/chave correspondente e limites de CPU/memória medidos
   para o host. Não reutilize credenciais de exemplo.
2. Valide o modelo sem subir/recriar serviços:

   ```bash
   docker compose -f docker-compose.yml -f docker-compose.production.yml \
     --profile app config --quiet
   ```

3. Construa e suba, por exemplo, duas réplicas:

   ```bash
   docker compose -f docker-compose.yml -f docker-compose.production.yml \
     --profile app up -d --build --scale api=2
   ```

   Para incluir Seq/OTel, adicione `--profile obs`; os serviços de banco,
   broker e Sentinel continuam sem portas publicadas no host. O Adminer só é
   ativado explicitamente com `--profile devtools` e não publica porta no
   overlay.

Os valores `*_CPUS` e `*_MEMORY` são limites por container, não limites do
conjunto. Para dimensionar pools, considere
`réplicas API × POSTGRES_MAX_POOL_SIZE + réplicas Workers ×
POSTGRES_MAX_POOL_SIZE` e reserve conexões para migrations/administração. O
default de produção é pool máximo 20 por processo; reduza se a configuração do
PostgreSQL ou a escala exigir.

O Compose reinicia processos que terminam (`unless-stopped`), mas não reinicia
um container apenas por estar `unhealthy`; readiness, logs e alertas continuam
necessários. `docker compose ps`, `docker compose logs` e `docker stats` mostram
estado, reinícios e consumo. Escala permanece manual.

### TLS, segredos e atualização

- O certificado autoassinado da imagem serve apenas para desenvolvimento.
  Em produção, monte arquivos externos já emitidos em `TLS_CERT_FILE` e
  `TLS_KEY_FILE`; eles são montados read-only no Nginx.
- Segredos devem estar no `.env` fora do git, com permissões restritas. O
  overlay exige credenciais e limites não vazios; `docker-compose config`
  deve passar antes do deploy.
- Use tags versionadas para `API_IMAGE`, `WORKERS_IMAGE` e `FRONTEND_IMAGE`.
  Construa/valide a nova versão antes de atualizar. Compose não fornece
  rolling update nem rollback transacional; planeje janela e backup/restore.
- API, Redis, Sentinel, PostgreSQL e RabbitMQ não publicam portas na rede do
  host com o overlay. Restrinja também as portas HTTP/S no firewall do host.

### Estado persistente compartilhado

No host único, estes volumes nomeados são compartilhados por todas as
réplicas API e sobrevivem a recriações:

- `pdi-evidence` e `project-evidence`: arquivos de evidência de PDI/Projetos;
- `api_data_protection`: chaves usadas para validar cookies e tokens
  protegidos entre réplicas;
- `postgres_backups`: dumps PostgreSQL e `latest.json` do BackupWorker;
- `postgres_data`, volumes Redis/Sentinel e `rabbitmq_data`: dados dos serviços.

Volumes locais não protegem contra falha/perda do host. Inclua-os em uma
estratégia de cópia externa; os dumps PostgreSQL devem ser copiados para um
destino fora deste host e o restore deve ser testado. Não remova volumes com
`docker compose down -v` em ambientes com dados.

Data Protection usa application name estável `InfraControl`. Não apague ou
substitua `api_data_protection` durante atualização: cookies existentes deixam
de ser decifráveis. O key ring também não está criptografado por um serviço
externo; proteja o host e as permissões do volume.

EF migrations não rodam no startup de cada réplica. Antes de subir a nova
versão, faça backup validado e aplique a migration uma vez conforme o
procedimento de [README.md](../README.md#banco-de-dados-e-migrations); depois
verifique `/health/ready` e `docker compose ps`.

## Redis Sentinel (ADR-045)

O Compose sobe `redis-master`, `redis-slave` e três serviços Sentinel. O
`ConnectionStrings__Redis` da API/Workers aponta para os três Sentinels e usa
`serviceName=mymaster`; o mesmo multiplexer atende cache, rate limiting e locks.
SignalR também usa a factory Sentinel. A health check Redis verifica o primary
descoberto pelo multiplexer; cada health check de Sentinel exige quorum.

Configure `REDIS_PASSWORD` no `.env` real com valor forte URL-safe (letras,
números, `_` e `-`). Não use o valor de exemplo em produção. Os arquivos de
estado dos três Sentinels têm volumes persistentes para preservar a descoberta
do primary promovido após reinicialização de containers. A rede Compose reserva
IPs estáveis para Redis e Sentinel; confirme que `INFRACONTROL_NETWORK_SUBNET`
não conflita com LAN, VPN ou outras redes Docker do host. Os IPs individuais
devem ser únicos, pertencer à subnet e ficar fora de
`INFRACONTROL_DYNAMIC_IP_RANGE`. A réplica consulta o Sentinel no startup para
recuperar a função atual, inclusive se tiver sido promovida antes de reiniciar.
Não altere a subnet nem os IPs após inicializar volumes sem um procedimento de
migração planejado para a configuração persistida do Sentinel.
Na atualização de uma instalação que já tenha volumes Sentinel, confirme o
endereço do primary atual antes de recriar a rede. Configurações persistidas
com IP dinâmico anterior exigem mapear esse primary para o IP estável correto;
o entrypoint só migra automaticamente a configuração legada que ainda contém
o hostname literal `redis-master`.

Simulação de failover em ambiente de desenvolvimento:

```bash
# Confirmar primary inicial e replicação
docker compose --profile app exec redis-sentinel-1 redis-cli -a "$REDIS_PASSWORD" sentinel get-master-addr-by-name mymaster
docker compose --profile app exec redis-slave redis-cli -a "$REDIS_PASSWORD" info replication
docker compose --profile app exec redis-master redis-cli -a "$REDIS_PASSWORD" SET ha-smoke-failover probe
docker compose --profile app exec redis-master redis-cli -a "$REDIS_PASSWORD" WAIT 1 5000

# Derrubar o container primary; a réplica deve ser promovida por Sentinel
docker compose --profile app stop redis-master
sleep 15
docker compose --profile app exec redis-sentinel-1 redis-cli -a "$REDIS_PASSWORD" sentinel get-master-addr-by-name mymaster
docker compose --profile app exec redis-sentinel-1 redis-cli -a "$REDIS_PASSWORD" sentinel ckquorum mymaster
docker compose --profile app exec redis-slave redis-cli -a "$REDIS_PASSWORD" info replication
docker compose --profile app exec redis-slave redis-cli -a "$REDIS_PASSWORD" GET ha-smoke-failover

# Reiniciar a réplica promovida; o startup deve recuperar o primary pelo Sentinel
docker compose --profile app restart redis-slave
docker compose --profile app exec redis-slave redis-cli -a "$REDIS_PASSWORD" info replication

# Restaurar o container antigo; ele deve voltar como réplica após a reconciliação
docker compose --profile app start redis-master
docker compose --profile app exec redis-master redis-cli -a "$REDIS_PASSWORD" info replication
```

O smoke test isolado validou a promoção mesmo com o container primary parado,
a persistência de uma chave replicada, o reinício da réplica já promovida sem
reversão de papel e o retorno do primary antigo como réplica autenticada.
Repita o teste primeiro em homologação; ele provoca indisponibilidade temporária
do Redis e não deve ser executado contra produção sem janela aprovada.

Para CLI no host, use a porta Sentinel anunciada no `.env` e informe o nome do
serviço `mymaster`; não fixe o endereço do master em aplicações externas.
O failover redistribui o papel de primary, mas não oferece replicação síncrona
sem perda: considere o RPO do Redis para dados de cache/locks e não o use como
fonte de verdade.

## Mecanismos de coordenação

### 1. Lock distribuído no Redis (Workers)

Workers usam `IDistributedLockService` para prevenir execução duplicada:

- `CleanupWorker`: lock `worker:cleanup` (expira em 1h)
- `BackupWorker`: lock `worker:backup` (expira em 2h)
- `MonitoringPollingWorker`: lock `worker:monitoring-polling` (expira em 10min)
- `OutboxDispatcherWorker`: lock `worker:outbox-dispatcher` (expira em 5min).
  Consumers continuam idempotentes: Outbox é entrega at-least-once e pode
  republicar após falha entre publish e confirmação.

Se uma instância não conseguir adquirir o lock, ela pula a execução e registra log de debug.

### 2. Redis backplane para SignalR

SignalR usa Redis para coordenar conexões entre múltiplas instâncias da API:

- Qualquer instância pode atender conexões WebSocket
- Mensagens são sincronizadas via Redis
- Configuração: `AddStackExchangeRedis` com prefixo `infracontrol_signalr`

### 3. Fila RabbitMQ para IA (Ollama)

Requests de IA vão para fila RabbitMQ:

- API publica `AiInferenceMessage` na fila
- `AiInferenceWorker` consome e chama Ollama
- Permite escalar múltiplos workers de IA
- Permite escalar múltiplos Ollamas no futuro

## Monitoramento

### Logs centralizados

Seq coleta logs de todas as instâncias:

```bash
# Subir com observabilidade
docker compose --profile app --profile obs up -d

# Acessar Seq (com basic auth)
http://localhost:5341
# Usuário: SEQ_AUTH_USER do .env
# Senha: SEQ_AUTH_PASSWORD do .env
```

### Métricas OpenTelemetry

Coletor OpenTelemetry recebe métricas de todas as instâncias:

```bash
# OTLP endpoint
localhost:4317
```

## Troubleshooting

### API não responde após escala

1. Verificar se todas as réplicas estão healthy:
   ```bash
   docker compose ps
   ```

2. Verificar logs agregados e identificar os IDs das réplicas:
   ```bash
   docker compose --profile app logs api
   docker compose --profile app ps -q api
   ```

3. Verificar health checks:
   ```bash
   curl -k https://localhost:8443/health
   ```

### SignalR não funciona

1. Verificar primary e quorum:
   ```bash
   docker compose --profile app ps redis-master redis-slave redis-sentinel-1 redis-sentinel-2 redis-sentinel-3
   docker compose --profile app exec redis-sentinel-1 redis-cli -a "$REDIS_PASSWORD" sentinel ckquorum mymaster
   ```

2. Verificar logs da API para erros de conexão com Redis backplane

3. Verificar se o prefixo do canal está correto: `infracontrol_signalr`

### Workers executando duplicado

1. Verificar conexão do worker ao primary descoberto pelo Sentinel

2. Verificar logs para mensagens de lock já adquirido por outra instância

3. Verificar expiração dos locks (cleanup 1h, backup 2h, polling 10min e
   Outbox 5min)

### IA não responde

1. Verificar se RabbitMQ está healthy:
   ```bash
   docker compose ps rabbitmq
   ```

2. Verificar se Ollama está healthy (profile `ai`):
   ```bash
   docker compose --profile ai up -d ollama
   docker compose ps ollama
   ```

3. Verificar logs do `AiInferenceWorker`

## Limitações

- Docker Compose não oferece autoscaling automático
- Escala é manual via `--scale`
- Sem rolling updates automático (requer parar/iniciar)
- Redis tem failover Sentinel, mas replicação assíncrona (não promete RPO 0)
- RabbitMQ e PostgreSQL continuam single-node/SPOF nesta etapa
- volumes nomeados protegem contra recriação de container, não perda do host
- a API não tem limite de réplicas automático; valide o pool de banco e os
  recursos do host ao escalar

## Testes de carga e falha

O repositório não tinha ferramenta de carga; `load-tests/api.js` usa k6
versionado (`grafana/k6:0.54.0`) com fases de 10, 25 e 50 VUs e consultas
read-only de readiness e inventário. Use somente um ambiente de teste e uma
conta/cookie de usuário de teste; não exponha cookie real no terminal ou nos
logs:

```bash
docker run --rm -i \
  -e K6_BASE_URL \
  -e K6_AUTH_COOKIE \
  -v "$PWD/load-tests:/scripts:ro" \
  grafana/k6:0.54.0 run /scripts/api.js
```

Defina `K6_BASE_URL` (origem HTTPS pelo Nginx) e `K6_AUTH_COOKIE` no ambiente
antes de rodar. Opcionalmente `K6_READ_PATH` pode escolher outro GET autenticado
read-only. O sumário traz p50/p95/p99 por fluxo, duração, throughput e taxa de
checks; acompanhe simultaneamente CPU/memória com `docker stats` e conexões/
latência do PostgreSQL. O teste mede o ambiente escolhido, não garante 10–50
usuários em hardware diferente. Registre configuração do host, réplica count e
resultados no estado da task; os limites de aceitação precisam corresponder aos
SLOs definidos para o ambiente.

Teste de perda de uma réplica somente com pelo menos duas API e carga de teste,
nunca no ambiente compartilhado/produção sem janela aprovada:

```bash
docker compose -f docker-compose.yml -f docker-compose.production.yml \
  --profile app ps -q api
# Escolha um ID da lista; pare apenas esse container e verifique requisições.
docker stop <container-id>
# Após observar o tráfego durante pelo menos um ciclo DNS do Nginx (30 s):
docker start <container-id>
```

Para testar recriação, migre para uma stack isolada de homologação e recrie
uma réplica; confirme leitura/escrita de uma evidência de teste, login em outra
réplica, `/health/ready`, e persistência dos volumes. Não executar comandos
`stop`, `rm` ou `down -v` contra stack existente sem autorização explícita.

## Próximos passos

Para produção real, considerar:

- Alta disponibilidade do RabbitMQ (cluster)
- Failover automático do PostgreSQL (Patroni + etcd) ou failover manual da réplica, após decisão operacional específica
- Rolling updates (blue/green ou canary)
- Autoscaling automático (Kubernetes ou orquestrador específico)
