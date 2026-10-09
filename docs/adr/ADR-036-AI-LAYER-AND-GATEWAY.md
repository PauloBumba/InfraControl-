# ADR-036: AI Layer & Gateway — colocação da camada de IA no monólito

- **Status**: aceito
- **Data**: 2026-10-08
- **Tasks**: FAI-T001 (Fase AI — sprint S1 de `docs/35`; blueprint `docs/34` §9–§10, §50)

## Contexto

A blueprint exige uma camada central (**AI Gateway**) como único caminho
para LLM e proíbe chamadas espalhadas de provider (§9: "nunca Copilot →
OpenAI SDK diretamente"; §10: "não espalhar chamadas específicas de
provider pelo domínio"). O repositório **não tem nenhum código de IA**
(`docs/35` §1) e segue Clean Architecture + DDD + CQRS + Vertical Slice
com módulos como **pastas dentro dos projetos** (Knowledge, Pdi,
Monitoring, …), sustentados por 115 arch tests. A §50 avisa: "Não criar
essa estrutura cegamente. Reutilizar os padrões já existentes."

## Decisão

1. **Camada de IA = pastas por convenção nos projetos existentes**
   (mesmo padrão dos módulos), **sem projeto/csproj novo**:
   - `Application/AI/` — contratos (`IAiGateway`, `IAiProvider`,
     contratos de contexto/tools), commands/queries de IA, policies;
   - `Infrastructure/AI/` — adaptadores (Ollama, vector store,
     embeddings) — único lugar com `HttpClient` de IA;
   - `Api/Controllers/AI/` — endpoints;
   - `Workers/` apenas se houver job assíncrono de IA.
2. **AI Gateway é o único caminho para LLM**: handler/application
   service → `IAiGateway` → router de provider → `IAiProvider` →
   provedor. Pipeline por chamada:
   `capability → política (limites/permissão, ADR-040) → contexto
   (ADR-038, se houver) → provider → normalização/erros → audit +
   telemetry (provider, modelo, latência, tokens, traceId — §43/§55)
   → fallback (§54, ADR-037)`.
3. **O gateway não conhece o domínio**: recebe/devolve DTOs/primitivos
   da própria camada Application — sem referência de domínio dentro de
   `AI/Infrastructure`.
4. **Relação com o ADR-035**: a geração de Knowledge continua sendo
   montagem determinística até um ADR próprio aprovar LLM no fluxo; o
   gateway é por onde esse LLM entraria, sem mudar o workflow de
   revisão (§33, ADR-034).

## Consequências

- Reuso imediato de autorização (ADR-016), auditoria (F0-T014),
  OpenTelemetry (ADR-009) e health checks existentes.
- Separação de assembly continua garantida pelas regras de camada
  atuais (Application não referencia Infrastructure) — nenhum arch
  test precisa mudar; um teste de convenção de pasta pode ser
  acrescentado no S3.
- Se a superfície crescer (ex.: servidor MCP como processo separado),
  a criação de projeto próprio vira **amend deste ADR** — não decisão
  silenciosa.
