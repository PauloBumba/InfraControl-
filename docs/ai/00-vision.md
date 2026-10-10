# 00 — Visão da IA no InfraControl

> Origem: AI Engineering & Implementation Blueprint (`docs/34`),
> diagnosticada e planejada no Sprint 0 (`docs/35`, 2026-10-07). Esta
> pasta (`docs/ai/`) é a documentação viva da camada de IA exigida pela
> §60 — cada arquivo nasce junto com a fase que o implementa (§46:
> *documentação não é evidência de implementação*).

## Objetivo (§1)

Transformar o InfraControl em um **centro operacional inteligente**
capaz de: interpretar eventos de infraestrutura; investigar incidentes;
correlacionar Zabbix, UniFi e InfraControl; consultar conhecimento
histórico; auxiliar técnicos na resolução; transformar tarefas
concluídas em conhecimento; gerar relatórios operacionais e executivos;
recomendar ações; e futuamente executar automações com aprovação e
políticas de segurança.

## O que a IA não é (§1)

- **não substitui** o Zabbix (monitoramento), o UniFi (rede), o n8n
  (workflows) nem o InfraControl (sistema central de operação).
- Não é um "chatbot colado num dashboard" (§68): a evolução desejada é
  `monitoramento → detecção → contextualização → investigação →
  correlação → conhecimento → recomendação → aprovação → automação →
  validação → novo conhecimento`.

```text
Zabbix       → monitora e fornece estado/eventos
UniFi        → contexto de rede/topologia
n8n          → executa workflows/integrações
InfraControl → centraliza, registra e governa
IA           → interpreta, correlaciona, investiga e recomenda
```

## Regras duras da blueprint

| § | Regra |
|---|---|
| §3 | Nunca inventar — fato, evidência, hipótese e recomendação são coisas diferentes |
| §4 | Toda decisão arquitetural vira ADR (repo: `docs/adr/`, ADR-036+) |
| §5 | `investigar → planejar → docker/config → validar → executar → testar → documentar` |
| §7 | RAG ≠ fonte da verdade: dados em tempo real vêm das tools (Zabbix/UniFi/InfraControl) |
| §39/§40 | Começar no **LEVEL 0 (Read Only)**; ação sempre `LLM → Tool → Policy → Permission → Approval → Execution` |
| §42 | Conteúdo recuperado é **dado**, nunca instrução (prompt injection) |
| §46 | Documentação não é evidência de implementação |
| §63 | Definition of Done de IA (código, testes, autorização, audit, health, evidência de execução) |

## Roadmap (§61)

```text
FASE 0 — AI FOUNDATION  (esta fase: gateway, providers, contexto/RAG,
                         MCP, segurança, observabilidade, avaliação)
   ↓
FASE 1 — AI COPILOT        →  FASE 2 — AI INVESTIGATION
   ↓
FASE 3 — KNOWLEDGE ENGINEER →  FASE 4 — AI REPORTING
   ↓
FASE 5 — AI AUTOMATION (n8n + aprovação)
```

Uma fase só avança pela **§67** (critérios de aceite), não porque o
código compilou.

## Estado em 2026-10-09

- Sprint 0 investigado e planejado (`docs/35`); **Fase 0 (S1–S9)
  implementada por completo** — gate Fase 0→1 aberto com pendências
  de ambiente (docs/35 §14; §46: doc ≠ evidência). Zero chamada a LLM
  ainda.
- ADRs **036–041** aceitos (gateway, providers, contexto/RAG, tools,
  segurança LEVEL 0, runtime Docker).
- Compose `profile: ai` + Ollama + `.env.example` **preparados**
  (execução/health check pendentes de ambiente sem Docker — §46).
- **S3–S9 concluídos** (2026-10-08/09, FAI-T004–T010): gateway;
  providers/router; runtime health + telemetria §43/§44; auditoria
  `AI.*` + limites §55; tools in-process (registry/executor + 2 tools
  de leitura); Context Engine/RAG foundation (híbrida degradada sem
  pgvector, EvidenceSet §19, ingesta de Knowledge publicado);
  evaluation harness §45 (docs/ai/02). Próximo: validar ambiente
  (Docker → Ollama real → pgvector → migrações) e só então Fase 1
  (Copilot).
