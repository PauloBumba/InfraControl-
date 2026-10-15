# ADR-055: Automation Agent — LEVEL 1 (recomendação) somente

- **Status**: aceito
- **Data**: 2026-10-14
- **Tasks**: Fase F do plano de execução da IA (`docs/36-PLANO-AI-EXECUCAO.md`);
  roadmap Fase 5 (blueprint `docs/34` §38–§40)

## Contexto

A blueprint exige que a automação comece obrigatoriamente em LEVEL 0 e
que **toda elevação de autonomia tenha ADR** (ADR-040 §"elevação").
O fluxo da §38 é Detectar → Investigar → **Recomendar** → humano →
workflow; o nível correspondente é o **LEVEL 1 — Recommend** (§39):
Consultar, Analisar, Recomendar. Guardrails da §40: a IA não pode
(apagar dados, alterar infraestrutura/rede/VLAN, comandos arbitrários,
alterar usuários/permissões) sem Policy/Permission/Approval/Tool/Audit —
e `LLM → shell` é proibido.

## Decisão

1. **Elevação mínima: LEVEL 0 → LEVEL 1 (recomendar)** — única
   elevação desta fase. Sem escrita, sem aprovação, sem execução:
   `POST /api/v1/automation/recommendations` (Operate) devolve uma lista
   de recomendações para o humano decidir e executar por conta própria.
2. **Sinal determinístico v1**: incidentes ativos (Open/Investigating/
   Identified/Monitoring via `SearchIncidentsQuery`, top 8 por abertura)
   + knowledge recuperado (ADR-038) sobre os títulos. **Sem sinais →
   lista vazia sem chamar o LLM** (nada de recomendação sem base).
3. **Capacidade `Automation`** (ADR-037 §5) no gateway; contrato JSON
   `{"items":[{title,rationale,priority}]}` (≤5 itens) com parse
   tolerante e fallback (texto bruto vira 1 item) — espelho do ADR-054
   §5. `priority` normalizada para `alta|média|baixa`.
4. **System prompt LEVEL 1 explícito**: recomendar citando os dados;
   proibido executar, solicitar aprovação ou descrever comandos a rodar
   (LEVEL 2+); cada item carrega a verificação prévia que o humano deve
   fazer; guardrails da §40 reafirmados no prompt.
5. **Superfície**: bloco "Assistente de automação (LEVEL 1)" na
   CopilotPage (`/ai/copilot` — hub de IA). O card exibe
   título/prioridade/justificativa e a etiqueta "o humano decide".
   Sem botão de aceitar/executar — executar é LEVEL 2 (não implementado).
6. **Auditoria**: capacidade `Automation` já é registrada por execução
   (§43/§55); sem persistência de recomendação (v1 volátil, como o
   resumo do Relatório).

## Consequências

- A plataforma opera explicitamente em LEVEL 1 para automação; LEVEL 2
  (aprovação + execução via tool explícita com Policy/Permission/Approval)
  exige ADR próprio e a infraestrutura de approval — fora desta fase.
- Sinais futuros (Zabbix, UniFi, métricas) entram como novas fontes do
  prompt — com ADR quando mudarem a confiabilidade do que se recomenda.
- Recomendação errada é mitigada pela verificação prévia obrigatória no
  item + revisão humana — nunca pela execução automática.
