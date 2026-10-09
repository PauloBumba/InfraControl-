# ADR-039: MCP & Tool Contracts — exposição de ferramentas para a IA

- **Status**: aceito
- **Data**: 2026-10-08
- **Tasks**: FAI-T001 (Fase AI — sprint S1 de `docs/35`; blueprint `docs/34` §20–§21, §52)

## Contexto

A blueprint define MCP como **mecanismo de exposição de ferramentas, não
como domínio** (§20), com preferência por
`MCP Tool → Application Service → Domain/Infrastructure` e a proibição
`MCP → Banco diretamente`. O repositório não tem nenhum código MCP e
nenhuma tool de IA; as queries MediatR existentes já são a superfície
legítima de leitura do sistema.

## Decisão

1. **v1 in-process (sem servidor MCP)**: contrato `IAiTool` +
   `AiToolDefinition` em `Application/AI/` com os campos da §52 —
   `Name`, `Description`, JSON schema de input/output,
   `RequiredPermission`, `Timeout`, `MaxResults`, `AuditPolicy`,
   `Source` — e um `IToolRegistry` consultado pelo Context Engine.
2. **Execução sempre por application services** (queries/commands
   MediatR existentes): a tool monta a query e delega — nunca
   repositório, `DbContext` ou SQL direto (§20).
3. **Servidor MCP real (sidecar/stdio, SDK) = evolução**: só quando um
   cliente MCP externo precisar consumir; nessa data **nenhum pacote
   MCP é adicionado**. A exposição reimplementa o registry, não o
   domínio (§20: mecanismo, não domínio).
4. **Ferramentas iniciais previstas** (§21, implementadas no S7+, uma
   por task, todas de leitura): `get_active_incidents`,
   `get_incident`, `get_asset`, `get_open_tasks`, `get_related_tasks`,
   `search_knowledge`, `get_sla_status`, `get_zabbix_problems`,
   `get_zabbix_host_status`, `get_unifi_device`, `get_unifi_network_status`.
5. **Checklist de aceite de toda tool** (§21/§52): contrato + autorização
   (ADR-040) + audit + timeout + erro estruturado pt-BR + limite de
   resultados + traceId + documentação (`docs/ai/05`).

## Consequências

- Zero dependência externa na v1 — o Context Engine ganha capacidade de
  ação sem infra nova.
- Autorização herda as policies existentes (ADR-016) por tool — mesma
  regra de quem chama a API.
- Trocar para servidor MCP depois não altera contratos das tools nem
  consumidores.
