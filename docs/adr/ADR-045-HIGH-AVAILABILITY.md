# ADR-045 — Alta Disponibilidade em Docker Compose

- **Status**: aceito
- **Data**: 2026-10-10
- **Contexto**: após implementação de escala horizontal (ADR-044), eliminar SPOFs de Redis, RabbitMQ e PostgreSQL
- **Decisões tomadas**: 2026-10-10 (usuário: 1B, 2B, 3A)
- **Entrega incremental**: iniciar pelo Redis Sentinel; RabbitMQ e PostgreSQL ficam para etapas seguintes

## Problema

A arquitetura atual com escala horizontal (ADR-044) tem Single Points of Failure (SPOF):
- Redis: se cair, SignalR, cache, rate limiting e locks distribuídos falham
- RabbitMQ: se cair, outbox, notificações e IA param
- PostgreSQL: se cair, toda a aplicação para

Objetivo: implementar alta disponibilidade (HA) para estes três serviços usando Docker Compose.

## Restrições

- Manter Docker Compose (sem Kubernetes ou Swarm)
- Infraestrutura on-premises
- Preservar simplicidade operacional
- Evitar complexidade excessiva

## Opções por serviço

### 1. Redis High Availability

**Opção A: Redis Sentinel**
- 1 master + 2+ sentinels
- Sentinels monitoram master e promovem slave automaticamente
- Vantagem: simples, maduro, bem documentado
- Desvantagem: write replica manual (não automático)

**Opção B: Redis Cluster**
- 3+ masters com sharding
- Escala horizontal nativa
- Vantagem: escalável, write HA automático
- Desvantagem: complexo, overhead de sharding, overkill para pequena escala

**Opção C: Redis com failover manual**
- 2 containers Redis, switch manual no compose
- Vantagem: muito simples
- Desvantagem: downtime no failover, operacional

**Decisão**: Opção A (Redis Sentinel)
- Master + 2 sentinels
- 1 réplica e 3 sentinels para quorum de maioria
- Suficiente para 10-50 usuários

### 2. RabbitMQ High Availability

**Opção A: RabbitMQ Cluster**
- 3+ nodes com policy de quorum
- HA nativo para filas
- Vantagem: HA real, maduro
- Desvantagem: complexo, configuração de quorum

**Opção B: RabbitMQ com Mirror Queues**
- 2 nodes, filas espelhadas
- Vantagem: mais simples que cluster
- Desvantagem: mirror queues depreciados no RabbitMQ 4.0+

**Opção C: RabbitMQ com policy HA**
- 2+ nodes, policy classic ha-mode=all
- Vantagem: mais simples que cluster
- Desvantagem: menor performance que cluster

**Decisão**: Opção C (Policy HA classic)
- 2 nodes, ha-mode=all, ha-sync-mode=automatic
- Suficiente para pequena escala
- Mais simples que cluster

### 3. PostgreSQL High Availability

**Opção A: Streaming Replication (master + slave)**
- 1 master + 1 slave com pg_streaming
- Vantagem: simples, maduro
- Desvantagem: failover manual, slave read-only

**Opção B: Patroni + etcd**
- 3 nodes com patroni e etcd
- Failover automático
- Vantagem: HA real, failover automático
- Desvantagem: complexo, mais containers

**Opção C: Pgpool + 2 PostgreSQL**
- Pgpool como proxy + balanceador
- 2 PostgreSQL com replicação
- Vantagem: pgpool faz failover
- Desvantagem: pgpool como SPOF

**Decisão**: Opção A (Streaming Replication)
- 1 master + 1 slave
- Failover manual (aceitável para MVP)
- Escalável para Patroni no futuro

## Arquitetura proposta

```
              ┌─────────────┐
              │   Nginx     │
              └──────┬──────┘
                     │
        ┌────────────┴────────────┐
        │  API (N réplicas)      │
        └────────────┬────────────┘
                     │
        ┌────────────┴────────────┐
        │   Load Balancer DNS      │
        └────────────┬────────────┘
                     │
      ┌──────────────┼──────────────┐
      │              │              │
┌─────▼─────┐ ┌────▼────┐ ┌─────▼────┐
│ Redis     │ │RabbitMQ │ │PostgreSQL │
│ Sentinel   │ │ Node 1  │ │ Master    │
│ (1M+2S)   │ │ RabbitMQ │ │           │
│           │ │ Node 2  │ │           │
│           │ │         │ │           │
└───────────┘ └─────────┘ └───────────┘
                              │
                        ┌───────▼────────┐
                        │ PostgreSQL Slave │
                        │  (read-only)   │
                        └────────────────┘
```

## Configuração Docker Compose

### Redis Sentinel
```yaml
redis-master:
  image: redis:7-alpine
  command: redis-server --appendonly yes

redis-slave:
  image: redis:7-alpine
  command: redis-server --slaveof redis-master 6379 --appendonly yes
  depends_on:
    - redis-master

redis-sentinel-1:
  image: redis:7-alpine
  command: redis-sentinel /etc/redis/sentinel.conf
  depends_on:
    - redis-master

redis-sentinel-2:
  image: redis:7-alpine
  command: redis-sentinel /etc/redis/sentinel.conf
  depends_on:
    - redis-master
```

### RabbitMQ com HA
```yaml
rabbitmq-1:
  image: rabbitmq:3-management-alpine
  environment:
    RABBITMQ_ERLANG_COOKIE: secret-cookie
    RABBITMQ_NODENAME: rabbit@rabbitmq-1
  ports:
    - "5672:5672"
    - "15672:15672"

rabbitmq-2:
  image: rabbitmq:3-management-alpine
  environment:
    RABBITMQ_ERLANG_COOKIE: secret-cookie
    RABBITMQ_NODENAME: rabbit@rabbitmq-2
  links:
    - rabbitmq-1
```

### PostgreSQL com replicação
```yaml
postgres-master:
  image: postgres:17-alpine
  command: >
    postgres
    -c config_file=/etc/postgresql/postgresql.conf
    -c wal_level=replica
    -c max_wal_senders=3
    -c hot_standby=on
  environment:
    POSTGRES_REPLICATION_USER: replicator
    POSTGRES_REPLICATION_PASSWORD: replicator_password

postgres-slave:
  image: postgres:17-alpine
  command: >
    postgres
    -c config_file=/etc/postgresql/postgresql.conf
    -c hot_standby=on
  environment:
    POSTGRES_MASTER_HOST: postgres-master
    POSTGRES_REPLICATION_USER: replicator
    POSTGRES_REPLICATION_PASSWORD: replicator_password
  depends_on:
    - postgres-master
```

## Plano de implementação

A implementação é incremental, começando pelo Redis Sentinel. Nesta etapa:

- O Compose executa um primary Redis, uma réplica e três processos Sentinel
  autenticados, com estado persistido em volumes individuais.
- Redis e Sentinels usam endereços IPv4 estáveis em uma subnet Docker
  configurável; Sentinel monitora o IP do primary em vez de depender do DNS
  temporário do container. A réplica consulta o Sentinel ao iniciar para
  preservar o papel corrente após restart.
- API e Workers descobrem o primary usando `serviceName=mymaster`; cache,
  rate limiter, locks e SignalR usam a configuração Sentinel.
- Health checks consultam o primary atual e verificam quorum nos Sentinels.
- RabbitMQ permanece single-node. Alta disponibilidade PostgreSQL também fica
  fora desta etapa; antes de implementá-la, confirmar a escolha entre failover
  manual de streaming replication e Patroni + etcd, pois as escolhas registradas
  no cabeçalho não correspondem à decisão PostgreSQL descrita acima.
- Smoke test isolado confirmou promoção após parar o container primary,
  retenção de uma chave replicada, restart do Redis promovido sem reversão de
  papel e retorno do primary antigo como réplica autenticada. Alterar a subnet
  ou os IPs depois de persistir volumes Sentinel é uma migração operacional
  explícita; esses endereços são parte do estado do cluster.
- Na migração de instalações que já persistiram endereços Sentinel dinâmicos,
  identificar o primary atual e mapear seu endereço para o IP estável antes de
  recriar a rede. O entrypoint converte automaticamente somente o hostname
  legado `redis-master`; endereços numéricos anteriores não são reescritos às
  cegas porque podem representar uma réplica que foi promovida.

O endereço `redis-master:6379` publicado no host é apenas para diagnóstico e
desenvolvimento: aplicações devem usar os endereços Sentinel. A promoção é
automática, mas Redis Sentinel não oferece replicação síncrona nem garante RPO 0.

## Consequências

**Bom**
- Elimina SPOFs críticos
- Sistema mais resiliente
- Failover possível (manual ou automático)

**Ruim**
- Complexidade operacional aumentada
- Mais containers a gerenciar
- Maior consumo de recursos

**Riscos**
- Failover manual de PostgreSQL pode causar downtime
- Redis Sentinel precisa de 3 sentinels para quorum
- A subnet Redis deve ser única no host/rede; conflito de IP invalida o cluster
- RabbitMQ policy HA tem overhead de sincronização
