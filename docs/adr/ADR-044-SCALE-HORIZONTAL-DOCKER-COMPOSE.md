# ADR-044 — Escala Horizontal da API com Docker Compose

- **Status**: aceito
- **Data**: 2026-10-10
- **Contexto**: preparação para produção em infraestrutura própria, objetivo de 10-50 usuários simultâneos
- **Decisões tomadas**: 2026-10-10 (usuário)

## Problema

A aplicação atual roda em uma única instância da API atrás do nginx. Para suportar mais usuários e melhorar resiliência, é necessário implementar escala horizontal da API. Docker Compose não oferece autoscaling automático, mas permite múltiplas réplicas manuais (`docker compose up -d --scale api=N`).

O projeto já possui:
- Dockerfile multi-stage (api, workers, frontend)
- Nginx como proxy reverso
- Redis para cache distribuído e rate limiting
- SignalR para notificações em tempo real
- Workers com tarefas agendadas (cleanup, backup, polling, outbox)
- Ollama (profile `ai`) para runtime de LLM local (ADR-041)

Restrições:
- Não usar Kubernetes ou Swarm
- Manter infraestrutura própria on-premises
- Preservar arquitetura existente (modular monolith, CQRS, Vertical Slice)

## Decisões pendentes

### 1. Coordenação de Workers

**Opção A: Workers em única instância**
- Escalar apenas a API, manter workers=1
- Vantagem: simples, sem conflito de execução
- Desvantagem: workers se tornam bottleneck sob carga

**Opção B: Lock distribuído no Redis**
- Cada worker adquire lock antes de executar tarefa
- Vantagem: workers podem escalar
- Desvantagem: complexidade, latência adicional

**Opção C: Particionamento por tipo**
- CleanupWorker em instância 1, BackupWorker em instância 2, etc.
- Vantagem: sem conflito, escala controlada
- Desvantagem: configuração manual por tipo

**Opção D: Execução idempotente + tolerância a duplicação**
- Tarefas são idempotentes, duplicação é aceitável
- Vantagem: mais simples, sem coordenação
- Desvantagem: pode causar trabalho duplicado (backup, cleanup)

### 2. Storage de Uploads (PDI Evidence)

**Opção A: Volume compartilhado (NFS/SSHFS)**
- Todos os containers montam o mesmo volume
- Vantagem: consistência imediata
- Desvantagem: SPOP no servidor NFS, latência de rede

**Opção B: Storage S3-compatible (MinIO)**
- Novo serviço MinIO no compose
- Vantagem: escalável, API padrão, replicação futura
- Desvantagem: novo serviço a gerenciar

**Opção C: Manter local + aceitar inconsistência**
- Cada instância tem seu volume
- Vantagem: simples, sem novo serviço
- Desvantagem: uploads ficam em instâncias diferentes, inconsistência

**Opção D: Push para storage externo (R2, S3)**
- Uploads vão direto para cloud storage
- Vantagem: sem estado local, escalável
- Desvantagem: depende de serviço externo, custo

### 3. Migrations e Bootstrap

**Opção A: Idempotência completa**
- Bootstrap Admin e migrations são idempotentes
- Vantagem: múltiplas instâncias podem boot simultaneamente
- Desvantagem: migrations destrutivas precisam de cuidado

**Opção B: Migração em instância dedicada**
- Um container só executa migrations no startup
- Vantagem: controle total
- Desvantagem: complexidade adicional

**Opção C: Script externo de migration**
- `docker compose run api dotnet ef database update`
- Vantagem: separado do runtime
- Desvantagem: passo manual no deploy

### 4. SignalR Sticky Sessions

**Opção A: ip_hash no nginx**
- `ip_hash` directive do nginx
- Vantagem: simples, nativo
- Desvantagem: não funciona bem com proxies/CDN

**Opção B: Redis backplane para SignalR**
- SignalR usa Redis para coordenar conexões
- Vantagem: real scale-out, sem sticky sessions
- Desvantagem: latência adicional, complexidade

**Opção C: Cookie-based sticky sessions**
- `hash $cookie_jsessionid` no nginx
- Vantagem: funciona atrás de proxy
- Desvantagem: precisa configurar cookie

### 5. Nginx Load Balancing

**Opção A: Múltiplas réplicas com DNS round-robin**
- Docker Compose scale, nginx lista todos os containers
- Vantagem: balanceamento simples
- Desvantagem: docker-compose scale não expõe IPs fixos

**Opção B: Upstream dinâmico via DNS interno**
- Nginx resolve nome do serviço DNS Docker
- Vantagem: round-robin automático
- Desvantagem: nginx precisa resolver a cada request

**Opção C: Contêiner dedicado de load balancing**
- HAProxy ou nginx dedicado
- Vantagem: mais controle, health checks avançados
- Desvantagem: mais um container

### 6. Escala do Ollama (Runtime de IA)

O Ollama roda em um único container (profile `ai`) e é acessado via HTTP. Para escalar a capacidade de inferência:

**Opção A: Escalar Ollama com fila no RabbitMQ**
- Requests de IA vão para fila RabbitMQ, workers consomem e chamam Ollama
- Vantagem: desacopla API do runtime, filas controlam backlog, múltiplos Ollamas podem escalar
- Desvantagem: latência adicional, complexidade de implementação
- Implementação: novo worker `AiInferenceWorker` que consome fila e chama Ollama

**Opção B: Ollama único com caching de respostas**
- Cache de respostas no Redis para prompts idênticos
- Vantagem: simples, reduz carga no Ollama
- Desvantagem: não escala throughput real, só reduz duplicação

**Opção C: Escalar Ollama com round-robin na API**
- API chama múltiplas instâncias de Ollama diretamente
- Vantagem: latência mínima
- Desvantagem: API precisa saber endereços dos Ollamas, sem fila

**Opção D: Manter Ollama único (aceitar bottleneck)**
- Para 10-50 usuários, Ollama único pode ser suficiente
- Vantagem: sem complexidade
- Desvantagem: bottleneck sob carga pesada de IA

## Proposta inicial (Recomendação)

Mínimo viável para 10-50 usuários simultâneos:

1. **Workers**: Opção A (única instância)
   - Risco baixo: cleanup/backup/polling não são críticos sob carga
   - OutboxDispatcher: precisa de distribuição ou tolerância a duplicação

2. **Uploads**: Opção C (local) ou Opção B (MinIO)
   - PDI evidence não é crítico para MVP
   - MinIO é mais robusto para produção

3. **Migrations**: Opção A (idempotência)
   - Já documentado que Bootstrap Admin é idempotente
   - EF migrations podem ter conflito em schema change simultâneo

4. **SignalR**: Opção A (ip_hash) ou Opção B (Redis backplane)
   - ip_hash é mais simples para início
   - Redis backplane para scale real

5. **Nginx**: Opção B (DNS round-robin nativo)
   - Nginx usa nome do serviço Docker
   - Docker Compose escala com `--scale api=N`

6. **Ollama (IA)**: Opção D (único) ou Opção A (fila RabbitMQ)
   - Para 10-50 usuários, Ollama único pode ser suficiente
   - Se houver uso intensivo de IA, fila RabbitMQ é mais robusto

## Perguntas para o usuário

1. **Ollama (IA)**:
   - Aceita Ollama único (bottleneck, simples)?
   - Ou prefere fila RabbitMQ para escalar (complexo, robusto)?

2. Workers:
   - Aceita workers em única instância?
   - Ou prefere lock distribuído ou tolerância a duplicação?

3. Storage de uploads:
   - Aceita inconsistência temporária (local)?
   - Prefere MinIO (S3-compatible)?
   - Ou storage externo (R2/S3)?

4. SignalR:
   - ip_hash (simples, não funciona bem com proxy)?
   - Ou Redis backplane (robusto, mais complexo)?

5. Prioridade geral:
   - MVP simples vs produção robusta desde o início?

## Decisões tomadas (2026-10-10)

Usuário escolheu abordagem de **produção robusta desde o início, sem duplicação**:

1. **Ollama (IA)**: Opção A - Escalar Ollama com fila no RabbitMQ
   - Implementar worker `AiInferenceWorker` que consome fila RabbitMQ e chama Ollama
   - Requests de IA vão para fila, workers processam assincronamente
   - Permite escalar múltiplos Ollamas se necessário

2. **Workers**: Opção B - Lock distribuído no Redis
   - Cada worker adquire lock no Redis antes de executar tarefa
   - Previne duplicação de execução (cleanup, backup, polling)
   - Workers podem escalar sem conflito

3. **Storage de uploads**: Opção C - Manter local, conforme decisão inicial
   - Substituída pela decisão de 2026-10-10 registrada abaixo: volume Docker nomeado compartilhado no host único.

4. **SignalR**: Opção B - Redis backplane
   - SignalR usa Redis para coordenar conexões entre instâncias
   - Real scale-out sem sticky sessions
   - Necessário configurar `AddStackExchangeRedis` no SignalR

5. **Prioridade geral**: Produção robusta desde o início
   - Sem duplicação de execução
   - Escala controlada e coordenada
   - Complexidade aceita em favor de robustez

## Consequências

**Bom**
- Escala horizontal da API sem duplicação de trabalho
- Workers podem escalar com coordenação via Redis
- SignalR funciona corretamente com múltiplas instâncias
- IA pode escalar via fila RabbitMQ

**Ruim**
- Complexidade adicional (lock distribuído, Redis backplane, worker de IA)
- Latência adicional (fila RabbitMQ para IA, Redis para SignalR)
- Mais serviços a monitorar e manter

**Riscos**
- Redis torna-se SPOF crítico (já era para cache, agora também para lock e SignalR)
- RabbitMQ torna-se SPOF crítico (já era para outbox, agora também para IA)
- Configuração de Redis backplane para SignalR precisa ser testada

## Implementação

### Atualização para escala no host único (2026-10-10)

Por decisão do usuário para a execução completa deste plano, as evidências de
PDI e Projetos usam volumes Docker nomeados compartilhados entre as réplicas da
API. Isso permite que qualquer réplica leia os arquivos e que os dados sobrevivam
a recriações no mesmo host. Não é armazenamento replicado nem protege contra
falha do host; storage de objeto/HA continua fora desta decisão.

O key ring do ASP.NET Core Data Protection também é persistido em volume comum e
usa um nome de aplicação estável. Sem isso, cookies de autenticação emitidos por
uma réplica poderiam falhar ao serem validados por outra.

O upstream Nginx resolve novamente o DNS do serviço `api` e mantém uma zona
compartilhada; a descoberta acompanha mudanças de réplicas sem IPs fixos.
Workers concorrentes coordenam tarefas periódicas e lotes do Outbox com locks
Redis. A entrega do Outbox continua at-least-once: Inbox/consumidores devem
permanecer idempotentes para cobrir falha entre publicação e confirmação.

Migrations EF não são executadas por cada réplica no startup. O deploy deve
aplicar migrations separadamente e validar o backup/restore. Backups PostgreSQL
evidenciados pelo worker persistem em volume próprio.

`docker-compose.production.yml` fornece overlay de publicação: segredos e
certificados são fornecidos fora do repositório, somente Nginx publica HTTP/S,
e o operador define limites de recursos/imagens para o host. O Compose reinicia
processos encerrados, mas não substitui monitoramento nem autoscaling.

O runbook está em `docs/40-SCALE-HORIZONTAL-OPERATIONS.md`; os cenários k6 estão
em `load-tests/api.js`. Os números de capacidade 10/25/50 precisam ser medidos
no hardware de destino e não são garantidos pela configuração.
