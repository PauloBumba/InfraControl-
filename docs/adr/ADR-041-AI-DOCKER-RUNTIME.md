# ADR-041: AI Docker Runtime — serviço Ollama no profile `ai`

- **Status**: aceito
- **Data**: 2026-10-08
- **Tasks**: FAI-T001/T002 (Fase AI — sprint S2 de `docs/35`; blueprint `docs/34` §11, §47–§49)

## Contexto

A blueprint é Docker First (§47): toda dependência nova de
infraestrutura é tratada primeiro no ambiente Docker, com validação
(`docker compose config`, `up`, health check, conectividade por
`service-name:port` — §48) antes de integrar a aplicação. O ADR-022 do
repo decide **compose único com profiles** (`infra`/`app`/`obs`/
`zabbix`); a própria §49 manda "antes de criar outro Compose, verificar
os arquivos existentes". O workstation **não tem Docker** (decisão do
usuário) — a execução e o health check ficam com evidência pendente,
como as 12 migrações (§46: documentação ≠ evidência). Porta 11434 está
livre no mapa de portas do compose.

## Decisão

1. **Serviço `ollama` no `docker-compose.yml` único**, sob
   **`profiles: ["ai"]`** — nenhum arquivo de compose novo (ADR-022; a
   §49 concorda em verificar o que já existe antes de criar).
2. **Especificação do serviço** (seguindo o padrão dos demais):
   - imagem `ollama/ollama:latest` (padrão do repo para seq/otel/adminer
     — pinar tag depois da 1ª execução validada, §48);
   - `container_name: infracontrol-ollama`, volume `ollama_data`,
     `${OLLAMA_PORT:-11434}:11434`, rede `infracontrol`,
     `restart: unless-stopped`;
   - healthcheck HTTP em `GET /api/tags` via `bash -c` + `/dev/tcp`
     (mesmo padrão da `api` — a imagem não garante curl);
   - **sem `depends_on` da API**: IA é opcional e a aplicação sobe
     normalmente com o runtime "não configurado" (§44).
3. **Conectividade por nome de serviço** (§11/§48): a API recebe
   `Ai__Ollama__BaseUrl` com default `http://ollama:11434` — **nunca
   `localhost` entre containers**. Dev local fora do compose usa
   `localhost:11434`.
4. **`.env.example`**: `OLLAMA_PORT`, `OLLAMA_BASE_URL`,
   `OLLAMA_MODEL` (default `qwen2.5-coder:1.5b` — §11 literal) e
   `OLLAMA_EMBED_MODEL` **vazio** — a blueprint não nomeia modelo de
   embedding; a escolha é do ADR-038/S8 (não inventar).
5. **Ordem de validação quando houver ambiente** (§48):
   `docker compose config` → `docker compose --profile ai up -d ollama`
   → `ps`/`logs --tail=100` → `docker exec infracontrol-ollama ollama
   pull <modelo>` → `GET /api/tags` → `GET /api/generate` →
   healthcheck verde → só então subir a aplicação apontando para o
   gateway. **Nesta data: validação por revisão + checklist da §5;
   evidência marcada como pendente.**

## Consequências

- Stacks existentes não são afetadas (profile só sobe com
  `--profile ai`).
- A Fase 0 fica explicitamente incompleta até a execução real (DoD
  §63/§46) — registrada em `docs/35` §12/§13.
- Sem Ollama em execução, todo o código da IA roda em estado
  "não configurado" (health degradado, erro 404/503 amigável) — nunca
  resposta inventada (§3/§65).
