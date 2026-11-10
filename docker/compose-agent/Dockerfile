# Agente privilegiado de escala Compose (docs/65 Fase 3 — Decisão C:
# container dedicado). Expõe uma API HTTP mínima na rede interna do
# compose; a InfraControl.Api chama http://compose-agent:8080 com o
# header X-Agent-Token. Monta o socket Docker RW e o diretório do
# projeto read-only — é o ÚNICO componente que executa
# `docker compose up --scale`.
FROM python:3.12-alpine

# docker-cli (Engine API via socket) + plugin compose + buildx.
# Versões flutuam dentro do release do Alpine — o agente é interno e
# não é imagem de produção de terceiros.
RUN apk add --no-cache docker-cli docker-cli-compose docker-cli-buildx

WORKDIR /agent
COPY agent.py /agent/agent.py

ENV AGENT_PORT=8080 \
    PROJECT_DIR=/project \
    COMPOSE_PROJECT_NAME=infracontrol- \
    COMPOSE_FILE=docker-compose.yml

EXPOSE 8080
CMD ["python", "-u", "/agent/agent.py"]
