# ADR-047: Escala Horizontal do Ollama com Load Balancer

- **Status**: aceito
- **Data**: 2026-10-11
- **Contexto**: ADR-041 atualizado — o serviço Ollama agora suporta escalabilidade horizontal com 3 réplicas + nginx load balancer

## Contexto

O ADR-041 original definia um único container do Ollama com profile `ai`. Para ambientes de produção e carga maior de inferência de LLM, é necessário:
- Escalabilidade horizontal (múltiplas réplicas)
- Load balancing entre réplicas
- Alta disponibilidade (se uma réplica cair, as outras continuam atendendo)
- DNS interno consistente para a API

## Decisão

1. **Arquitetura de 3 réplicas + nginx load balancer**:
   - `ollama-1`, `ollama-2`, `ollama-3`: réplicas do Ollama com volumes persistentes separados
   - `ollama-lb`: nginx 1.27-alpine como load balancer round-robin
   - IPs estáveis configuráveis via env: `OLLAMA_1_IP`, `OLLAMA_2_IP`, `OLLAMA_3_IP`

2. **Configuração do nginx** (`docker/ollama-lb.conf`):
   - Upstream `ollama_backend` com round-robin
   - `max_fails=3 fail_timeout=30s`: health check passivo (marcando réplicas como down)
   - Keepalive connections (32) para melhor performance
   - Timeout de 300s para inferência de LLM
   - Buffering desabilitado para streaming de respostas

3. **Dependências e health checks**:
   - `ollama-lb` depende das 3 réplicas estarem healthy
   - Cada réplica tem healthcheck em `GET /api/tags` via `/dev/tcp`
   - Load balancer tem healthcheck em `GET /api/tags` via wget

4. **DNS interno**:
   - API usa `OLLAMA_BASE_URL=http://ollama-lb` (nome do serviço load balancer)
   - Réplica individual: `ollama-1:11434`, `ollama-2:11434`, `ollama-3:11434`
   - Porta publicada no host: `OLLAMA_PORT=11434` (aponta para ollama-lb)

5. **Modelos LLM**:
   - Cada réplica tem seu volume persistente (`ollama_1_data`, `ollama_2_data`, `ollama_3_data`)
   - Modelo deve ser baixado em cada réplica:
     ```bash
     docker exec infracontrol-ollama-1 ollama pull qwen2.5-coder:1.5b
     docker exec infracontrol-ollama-2 ollama pull qwen2.5-coder:1.5b
     docker exec infracontrol-ollama-3 ollama pull qwen2.5-coder:1.5b
     ```

6. **Escala manual**:
   - Para aumentar réplicas, alterar o docker-compose.yml adicionando novos serviços `ollama-4`, `ollama-5`, etc.
   - Atualizar o nginx upstream com os novos backends
   - Atribuir IPs estáveis únicos na subnet

## Consequências

- **Benefícios**:
  - Escalabilidade horizontal: múltiplas réplicas distribuem a carga de inferência
  - Alta disponibilidade: se uma réplica cair, o nginx marca como down e distribui para as outras
  - Performance: keepalive connections e buffering otimizado para streaming
  - Isolamento: cada réplica tem seu volume de modelos

- **Trade-offs**:
  - Maior consumo de recursos (3x memória para modelos carregados)
  - Complexidade de configuração (3 IPs estáveis, nginx upstream)
  - Modelos devem ser baixados em cada réplica manualmente

- **Impacto no ADR-041**:
  - Substitui o serviço único `ollama` por 3 réplicas + load balancer
  - Mantém o profile `ai` e a natureza opcional (API funciona sem IA)
  - Atualiza a URL padrão de `http://ollama:11434` para `http://ollama-lb`

## Validação

```bash
# Validar configuração
docker compose --profile ai config

# Subir o stack de IA
docker compose --profile ai up -d

# Verificar status
docker compose --profile ai ps

# Baixar modelo em cada réplica
docker exec infracontrol-ollama-1 ollama pull qwen2.5-coder:1.5b
docker exec infracontrol-ollama-2 ollama pull qwen2.5-coder:1.5b
docker exec infracontrol-ollama-3 ollama pull qwen2.5-coder:1.5b

# Testar load balancer
curl http://localhost:11434/api/tags

# Testar inferência
curl http://localhost:11434/api/generate -d '{
  "model": "qwen2.5-coder:1.5b",
  "prompt": "Hello, world!"
}'
```

## Referências

- ADR-041: AI Docker Runtime — serviço Ollama no profile `ai`
- ADR-022: Compose único com profiles
- nginx upstream documentation: https://nginx.org/en/docs/http/ngx_http_upstream_module.html
