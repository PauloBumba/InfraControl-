# ADR-053: AI Investigation — enriquecimento de incidente

- **Status**: aceito
- **Data**: 2026-10-14
- **Tasks**: Fase C do plano de execução da IA (`docs/36-PLANO-AI-EXECUCAO.md`);
  roadmap Fase 2 (blueprint `docs/34` §26/§27 — Investigation Engine)

## Contexto

O Copilot (ADR-052) cobre perguntas soltas; a blueprint pede, para a
Fase 2, investigação dirigida de um incidente: coletar evidências do
próprio incidente, correlacionar com knowledge/histórico e produzir
análise com hipóteses e confiança — enriquecimento operacional (§26/§27).
O Investigation Engine completo (timeline, múltiplos passes, validação)
é evolução; esta fase entrega o passe v1 grounded e auditável.

## Decisão

1. **`POST /api/v1/incidents/{id}/investigate`** (Operate — ADR-030 §3):
   ação operacional sobre o incidente; Read não cobre "pedir análise".
2. **Passe v1 grounded** (§26): dados do incidente (via
   `GetIncidentQuery` existente — Tool → Application Service, §20) +
   busca híbrida do Context Engine (ADR-038) sobre título+descrição →
   `EvidenceSet` → capacidade `IncidentAnalysis` no gateway (ADR-036/§15)
   → análise com hipóteses classificadas por confiança (§19:
   `Probable/Possible/Unknown` — nunca `Confirmed` sem verificação).
3. **LEVEL 0 mantido** (ADR-040 §1): a análise EXPLICA e apresenta
   hipóteses; não recomenda ações de escrita nem executa — elevação
   (recomendação/automação) é a Fase F/ADRs próprios.
4. **Tool `get_incident`** no registry MCP (ADR-039 · §52): leitura de
   1 incidente por id via `GetIncidentQuery`; mesma autorização Read do
   pipeline MediatR — disponível para o Copilot/futuro tool-calling.
5. **Resposta enriquece a UI**: análise + evidências (fonte/citação/
   confiança) + provider/modelo/usedFallback (transparência §15/§43);
   a tela Incidentes abre o resultado num painel — sem persistir
   resultado no incidente (histórico de investigação = evolução).
6. **Estado degradado** (§44/§57): mesma casca do Copilot —
   `AiProviderNotConfigured` → 503 amigável (GlobalExceptionHandler,
   ADR-052 §6); falha do gateway → 502.

## Consequências

- Primeira ferramenta de Investigação com evidência real; o pipeline
  Fase 0 ganha o segundo caso de uso (Copilot + Investigation).
- Sem provider configurado o botão mostra o estado degradado — nunca
  análise inventada.
- Timeline, múltiplos passes, correlação com Zabbix/UniFi em tempo real
  (tools) e persistência de investigação ficam para evolução com ADR.
