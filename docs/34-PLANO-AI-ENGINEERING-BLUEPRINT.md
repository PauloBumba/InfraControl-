# AI Engineering & Implementation Blueprint (InfraControl)

> Especificação recebida do usuário em 2026-10-07 e arquivada
> integralmente (mesmo tratamento de docs/32 e docs/33). Toda alteração
> arquitetural derivada desta blueprint exige ADR; a numeração de ADRs
> deste repo segue a existente (último: ADR-035 → próximos ADR-036+),
> conforme a própria §59 manda verificar antes de criar.
> Regras de ambiente: Docker indisponível neste posto de trabalho
> (decisão do usuário "não roda o docker") — dependências Docker da
> blueprint (Ollama etc.) ficam com Compose/configuração preparados e
> execução/health check pendentes de ambiente, como as 12 migrações.

---

# NFRACONTROL — AI ENGINEERING & IMPLEMENTATION BLUEPRINT

Documento operacional para implementação.

Regra principal: investigar → planejar → preparar Docker/configuração → validar → executar → testar → documentar.

Nenhum agente deve sair implementando ou executando infraestrutura sem primeiro entender o ambiente e preparar/validar o ambiente Docker necessário.

## 1. Objetivo

Adicionar uma camada de Inteligência Artificial ao InfraControl, transformando a plataforma em um centro operacional inteligente capaz de:

- interpretar eventos de infraestrutura;
- investigar incidentes;
- correlacionar dados do Zabbix, UniFi e InfraControl;
- consultar conhecimento histórico;
- auxiliar técnicos durante a resolução;
- transformar tarefas concluídas em conhecimento;
- gerar relatórios operacionais e executivos;
- recomendar ações;
- futuramente executar ações automatizadas com aprovação e políticas de segurança.

A IA não substituirá:

- Zabbix como ferramenta de monitoramento;
- UniFi como fonte de informações da infraestrutura de rede;
- n8n como ferramenta de automação/integrador;
- InfraControl como sistema central de operação, ITSM, inventário, conhecimento e governança.

### Princípio arquitetural

```text
Zabbix       → monitora e fornece estado/eventos de monitoramento
UniFi        → fornece contexto de rede/topologia/dispositivos
n8n          → executa workflows e integrações
InfraControl → centraliza, registra e governa
AI           → interpreta, correlaciona, investiga e recomenda
```

## 2. Estado arquitetural esperado

O projeto atual continuará sendo a base.

Stack principal:

- .NET 10
- Modular Monolith
- Clean Architecture
- DDD
- CQRS
- MediatR
- Vertical Slice
- PostgreSQL
- Redis
- RabbitMQ/MassTransit
- React
- TypeScript
- Vite
- OpenTelemetry
- Serilog/Seq
- SignalR
- Docker Compose

Os módulos existentes devem ser preservados.

A IA será adicionada como uma capacidade transversal da plataforma.

Não criar um "AI System" isolado que replique o InfraControl.

## 3. Arquitetura geral

```text
                         INFRA CONTROL
                              │
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
       ITSM                INVENTORY              PEOPLE
        │                     │                     │
     Tasks                  Assets                Users
     SLA/SLO                Network               Roles
     Incidents              Servers
        │
        ├────────────── Knowledge
        ├────────────── Monitoring / Alerts
        └────────────── Integrations
                              │
                  ┌───────────┴───────────┐
                  │                       │
                ZABBIX                  UNIFI
                  │                       │
              Problems                Devices
              Metrics                 Clients
              Triggers                APs/Switches
                  │                       │
                  └───────────┬───────────┘
                              │
                         INFRA CONTROL
                              │
                         AI GATEWAY
                              │
                    AI CONTEXT ENGINE
                              │
             ┌────────────────┼────────────────┐
             │                │                │
            RAG             TOOLS            LLM
             │                │                │
        Knowledge        MCP/Internal      Provider Router
        Documents        Services          / Model Manager
        History          Zabbix
        Embeddings       UniFi
        Vector Store     InfraControl
             │                │
             └────────────────┼────────────────┘
                              │
                    INVESTIGATION ENGINE
                              │
       ┌────────────┬─────────┼──────────┬────────────┐
       │            │         │          │            │
    Copilot   Incident AI  Knowledge  Reporting  Automation
```

## 4. Regra de ouro para Vibe Coders

Os agentes de código possuem autonomia para:

- investigar o código;
- pesquisar documentação;
- analisar dependências;
- estudar APIs;
- estudar MCP;
- estudar Zabbix;
- estudar UniFi;
- estudar os módulos existentes;
- propor soluções;
- criar protótipos;
- implementar;
- escrever testes;
- corrigir problemas;
- melhorar a implementação.

Porém:

### NÃO podem alterar sem justificativa

- arquitetura principal;
- domínio existente;
- contratos públicos;
- autenticação/autorização;
- modelo de eventos;
- fluxo Incident → Task → SLA;
- estratégia de integração;
- banco de dados;
- escolha do provider de IA;
- política de segurança;
- permissões da IA.

Toda mudança arquitetural relevante precisa gerar um ADR:

- Problema
- Contexto
- Alternativas investigadas
- Solução escolhida
- Motivo
- Impactos
- Riscos

## 5. Protocolo obrigatório de execução

Todo agente deverá seguir esta ordem:

1. INVESTIGATE
2. UNDERSTAND
3. PROPOSE
4. VALIDATE AGAINST ARCHITECTURE
5. PREPARE DOCKER / ENVIRONMENT
6. VALIDATE DOCKER / ENVIRONMENT
7. IMPLEMENT
8. TEST
9. EXECUTE
10. OBSERVE
11. DOCUMENT

### Regra crítica

Preparar Docker/configuração antes de executar.

Exemplo:

ERRADO

```text
Implementar
↓
Executar
↓
Descobrir que Ollama/Rabbit/Postgres/Redis não existe
```

Correto:

```text
Investigar
↓
Planejar
↓
Criar/ajustar Docker Compose
↓
Validar configuração
↓
Subir dependências
↓
Health checks
↓
Implementar
↓
Testar
↓
Executar fluxo
↓
Validar resultado
```

### Antes de executar qualquer serviço

O agente deve verificar:

- [ ] Docker instalado
- [ ] Docker Compose disponível
- [ ] Containers existentes
- [ ] Portas utilizadas
- [ ] Networks existentes
- [ ] Volumes existentes
- [ ] Variáveis de ambiente
- [ ] Secrets
- [ ] Health checks
- [ ] Dependências externas
- [ ] Conectividade entre containers
- [ ] Recursos disponíveis

Nunca assumir que uma infraestrutura existe.

## 6. Regra de investigação antes de criar

O agente não deve assumir:

> "Não encontrei isso, então vou criar."

Primeiro deve procurar:

- código existente;
- interfaces;
- handlers;
- eventos;
- entidades;
- migrations;
- endpoints;
- componentes React;
- serviços;
- integrações;
- testes;
- documentação;
- Docker Compose;
- containers;
- configurações;
- ADRs.

Regra:

- Reutilizar antes de criar.
- Integrar antes de duplicar.
- Investigar antes de alterar.

## 7. Source of Truth

| Informação | Fonte principal |
|---|---|
| Problemas/monitoramento | Zabbix |
| Estado de rede/topologia | UniFi |
| ITSM | InfraControl |
| Inventário | InfraControl |
| Usuários/permissões | InfraControl |
| Tasks/TESC | InfraControl |
| PDI | InfraControl |
| Knowledge publicado | InfraControl + Vector Store |
| Conhecimento semântico | RAG / Vector Store |
| Workflows | n8n |
| Interpretação | AI |
| Investigação | AI Investigation Engine + evidências |

Regra:

- RAG não é a fonte universal da verdade.

Dados operacionais em tempo real devem vir de ferramentas e serviços especializados.

RAG fornece:

- conhecimento;
- histórico;
- documentação;
- contexto semântico.

Tools fornecem:

- estado atual;
- métricas;
- eventos;
- dados estruturados;
- informações operacionais.

## 8. FASE 0 — AI FOUNDATION

A Fase 0 é a fundação obrigatória.

Não criar uma fase separada para Context/RAG.

A capacidade de contexto é transversal e deve nascer aqui.

```text
FASE 0 — AI FOUNDATION

├── AI Gateway
├── Provider Abstraction
├── Provider & Model Manager
├── Ollama Docker
├── External Providers
├── Model Discovery
├── AI Context Engine
│   ├── RAG
│   ├── Embeddings
│   ├── Vector Store
│   ├── Semantic Search
│   ├── Keyword Search
│   ├── Hybrid Search
│   ├── Structured Search
│   ├── Metadata Filtering
│   ├── Reranking
│   ├── Context Builder
│   ├── Evidence Model
│   └── Source Ranking
├── MCP Foundation
├── Internal AI Tools
├── Security
├── Permissions
├── Audit
├── Observability
├── Runtime Health
└── AI Evaluation Foundation
```

## 9. AI Gateway

Criar uma camada central:

### AI Gateway

Responsável por:

- seleção de provider;
- seleção de modelo;
- prompts;
- tool calling;
- context;
- token management;
- retries;
- timeout;
- fallback;
- logging;
- tracing;
- policy;
- audit;
- telemetria;
- controle de custo;
- structured output.

Fluxo:

```text
AI Service
    ↓
AI Gateway
    ↓
Provider Router
    ↓
Provider Adapter
    ↓
LLM
```

Nunca:

```text
Copilot
  ↓
OpenAI SDK diretamente
```

## 10. AI Provider Abstraction

Criar uma abstração central.

Exemplo:

```csharp
IAiProvider
```

ou, caso o projeto já possua contrato equivalente:

```csharp
ILlmClient
```

O agente deve reutilizar o contrato existente se ele já existir.

Possíveis adapters:

- OllamaProvider
- OpenAICompatibleProvider
- GeminiProvider
- CustomHttpProvider

Não espalhar chamadas específicas de provider pelo domínio.

## 11. AI Provider & Model Manager

O InfraControl não deverá ficar preso a uma lista fixa de empresas ou modelos.

O administrador deverá conseguir configurar Providers e modelos pela aplicação.

### Ollama

Ollama será o provider local padrão.

Provisionamento:

```text
Docker
  ↓
Ollama
  ↓
qwen2.5-coder:1.5b
```

Quando InfraControl e Ollama estiverem no mesmo Docker Compose:

```text
http://ollama:11434
```

Não utilizar localhost entre containers.

### Docker Compose inicial

Exemplo conceitual:

```yaml
services:
  ollama:
    image: ollama/ollama:latest
    container_name: infracontrol-ollama
    ports:
      - "11434:11434"
    volumes:
      - ollama_data:/root/.ollama
    restart: unless-stopped

volumes:
  ollama_data:
```

O agente deverá:

1. verificar a versão adequada da imagem;
2. verificar recursos disponíveis;
3. criar/ajustar o Compose;
4. validar `docker compose config`;
5. subir o serviço;
6. executar health check;
7. validar API;
8. instalar/puxar o modelo;
9. validar geração;
10. só então integrar o InfraControl.

Não executar `ollama pull` ou chamadas de produção antes da infraestrutura estar validada.

## 12. Providers externos

Providers externos não devem ficar obrigatoriamente hardcoded.

Admin poderá cadastrar:

- Nome
- Tipo / Protocolo
- Base URL
- API Key
- Status

Exemplo:

```text
Provider: Minha IA
Tipo: OpenAI Compatible
Base URL: https://ia.interna/api
API Key: ********
```

### Tipos iniciais

- Ollama
- OpenAI Compatible
- Google Gemini
- Custom HTTP

OpenRouter pode ser configurado através do adapter OpenAI Compatible quando o contrato da API for compatível.

Futuros providers devem ser adicionáveis sem alterar módulos de negócio.

## 13. Teste de Provider

A interface administrativa deverá possuir:

```text
[ Testar conexão ]
```

Validar:

- conectividade;
- autenticação;
- disponibilidade;
- endpoint;
- resposta;
- modelo;
- latência;
- erro retornado.

Resultado:

```text
CONNECTED
AUTHENTICATED
MODEL_AVAILABLE
READY
```

ou:

```text
FAILED
```

com erro técnico rastreável.

## 14. Descoberta de modelos

Quando o provider suportar descoberta:

```text
InfraControl
    ↓
Provider API
    ↓
Models Endpoint
    ↓
Model Registry
    ↓
Admin UI
```

Quando não suportar:

```text
Admin
 ↓
Cadastrar modelo manualmente
```

Pesquisa na Web pode ser utilizada para consultar documentação oficial, mas não deve substituir uma API oficial de descoberta quando esta existir.

## 15. Modelos por funcionalidade

Permitir configuração:

```text
Copilot
    → Provider / Modelo

Incident Analyst
    → Provider / Modelo

Knowledge Engineer
    → Provider / Modelo

Reporting
    → Provider / Modelo
```

Também:

- Default Model
- Fallback Model

Registrar em cada execução:

- Provider
- Model
- Capability
- Timestamp
- Latency
- Tokens
- TraceId

## 16. AI Context Engine

Esta é uma das partes mais importantes do sistema.

O Context Engine será uma capacidade transversal.

Não pertence exclusivamente ao Knowledge.

Não pertence exclusivamente ao Incident Analyst.

Será utilizado por:

- Copilot;
- Incident Analyst;
- Knowledge Engineer;
- Reporting;
- Automation.

Arquitetura:

```text
AI Request
    ↓
Query Analyzer
    ↓
AI Context Engine
    ├── Semantic Search
    ├── Keyword Search
    ├── Structured Queries
    ├── Metadata Filters
    ├── Vector Search
    ├── Tool Calls
    ├── Source Ranking
    ├── Reranking
    └── Context Builder
    ↓
Evidence Set
    ↓
LLM
```

## 17. Hybrid RAG

Não utilizar somente busca vetorial.

O mecanismo deverá evoluir para:

```text
Pergunta
   ↓
Query Analyzer
   ↓
┌─────────────────────┐
│ Semantic Search     │
│ Keyword Search      │
│ Structured Search   │
│ Metadata Filtering  │
└─────────────────────┘
   ↓
Merge
   ↓
Reranker
   ↓
Context Builder
   ↓
Evidence Set
   ↓
LLM
```

### RAG deve consultar

- Knowledge;
- documentos;
- procedimentos;
- histórico de incidentes;
- histórico de tasks;
- TESC;
- PDI quando autorizado;
- documentação;
- soluções anteriores;
- registros operacionais apropriados.

## 18. Contexto operacional em tempo real

Para dados em tempo real:

```text
AI Context Engine
       ↓
Tools
       ├── InfraControl
       ├── Zabbix
       ├── UniFi
       └── outros sistemas autorizados
```

Exemplo:

Pergunta:

> "Por que o servidor APP-02 está indisponível?"

O Context Engine pode combinar:

- Zabbix: problema atual
- InfraControl: incidentes abertos
- Knowledge: casos anteriores
- Inventory: informações do servidor
- UniFi: contexto de rede
- Tasks: mudanças recentes

## 19. Evidence Model

Todo contexto relevante deve poder ser transformado em evidência.

Modelo conceitual:

```text
Evidence
├── Id
├── Source
├── SourceType
├── Timestamp
├── Data
├── Confidence
├── Citation
├── Tool
└── TraceId
```

### Tipos de fonte

- Zabbix
- UniFi
- InfraControl
- Knowledge
- Task
- Incident
- PDI
- TESC
- Document
- API

### Classificação

- Confirmed
- Probable
- Possible
- Unknown

A IA nunca deve apresentar uma hipótese como fato confirmado.

## 20. MCP

MCP será tratado como mecanismo de exposição de ferramentas.

Não como domínio.

Arquitetura:

```text
InfraControl
     │
     ├── Application Services
     │
     └── MCP Tools
            ├── Zabbix
            ├── UniFi
            ├── Knowledge
            ├── ITSM
            ├── Inventory
            └── Reports
```

Preferir:

```text
MCP Tool
    ↓
Application Service
    ↓
Domain / Infrastructure
```

Nunca:

```text
MCP
 ↓
Banco diretamente
```

## 21. Tools iniciais

Criar contratos bem definidos.

```text
get_active_incidents
get_incident
get_asset
get_asset_history

get_zabbix_problems
get_zabbix_host_status
get_zabbix_history

get_unifi_device
get_unifi_client
get_unifi_network_status

get_open_tasks
get_related_tasks

search_knowledge
get_sla_status
```

Cada tool deve possuir:

- contrato;
- autorização;
- logs;
- timeout;
- tratamento de erro;
- dados estruturados;
- limite de resultados;
- rastreabilidade;
- política de acesso.

## 22. FASE 1 — AI COPILOT

### Objetivo

Criar o assistente operacional do InfraControl.

Exemplos:

> "O que está acontecendo na infraestrutura?"

> "Quais servidores estão com problemas?"

> "Quais incidentes críticos estão abertos?"

> "Esse problema já aconteceu antes?"

> "Quais chamados estão relacionados ao SW-04?"

> "Como resolvemos esse problema anteriormente?"

A IA deverá buscar dados reais.

Nunca responder com conhecimento inventado.

## 23. Copilot Architecture

```text
React
  ↓
AI Chat API
  ↓
AI Orchestrator
  ↓
AI Context Engine
  ↓
Tool Selection
  ↓
MCP / Internal Tools
  ↓
InfraControl / Zabbix / UniFi
  ↓
Evidence Set
  ↓
LLM
  ↓
Grounded Response
```

## 24. Copilot UI

Criar AI Copilot global:

```text
┌──────────────────────────────────────────────┐
│ Logo       Search              AI ✦   User   │
└──────────────────────────────────────────────┘
```

Também contextual:

```text
Incident #1821                    [Ask AI]
```

Contexto enviado:

- incidentId
- assetId
- events
- alerts
- history
- tasks
- knowledge

Não transformar toda tela em chatbot.

## 25. FASE 2 — AI INCIDENT ANALYST

Esta é uma das funcionalidades mais importantes.

### Objetivo

Permitir que a IA investigue um incidente de forma estruturada.

Exemplo:

```text
Incident #1821
Servidor APP-02 indisponível
```

A IA poderá investigar:

1. Estado atual
2. Histórico
3. Alertas relacionados
4. Outros hosts
5. Rede
6. UniFi
7. Incidentes anteriores
8. Tasks
9. Knowledge
10. Alterações recentes

## 26. Investigation Engine

Fluxo:

```text
Incident
   ↓
AI Investigation
   ↓
Collect Evidence
   ↓
Correlate
   ↓
Search Knowledge
   ↓
Analyze
   ↓
Hypotheses
   ↓
Validate
   ↓
Conclusion
   ↓
Recommendation
```

A investigação deve ser limitada por:

- timeout
- max tool calls
- max tokens
- max context
- permissions
- policy

## 27. Investigação auditável

Toda investigação deve possuir:

- InvestigationId
- StartedAt
- FinishedAt
- Actor
- Question
- IncidentId
- AssetId
- ToolsUsed
- Queries
- Evidence
- Findings
- Hypotheses
- Conclusion
- Recommendation
- Provider
- Model
- TraceId

## 28. Investigation Timeline

Interface:

```text
AI Investigation #1042

12:41:02 Consultando Zabbix...
12:41:03 Encontrados 3 problemas relacionados.
12:41:05 Consultando UniFi...
12:41:07 Switch SW-04 apresenta perda de conectividade.
12:41:09 Buscando Knowledge...
12:41:10 Encontrados 4 casos semelhantes.
12:41:14 Correlação concluída.
12:41:16 Hipótese gerada.
```

O técnico deve conseguir enxergar:

```text
Pergunta
↓
Ferramentas utilizadas
↓
Dados encontrados
↓
Evidências
↓
Correlação
↓
Hipóteses
↓
Conclusão
```

## 29. Resultado da investigação

A UI poderá mostrar:

- Resumo
- Evidências
- Hipótese principal
- Confiança
- Fontes
- Recomendação
- Próximas ações

Classificação:

- CONFIRMED
- PROBABLE
- POSSIBLE
- UNKNOWN

Exemplo:

```text
Hipótese principal:
Configuração do serviço X.

Confiança:
Média.

Status:
PROBABLE.

Recomendação:
Verificar configuração do serviço X.
```

## 30. Incident Enrichment

Antes ou durante a investigação, o Incident Analyst poderá enriquecer o incidente:

```text
Incident
 ↓
Identify Asset
 ↓
Zabbix Context
 ↓
UniFi Context
 ↓
Related Incidents
 ↓
Related Tasks
 ↓
Knowledge
 ↓
Timeline
```

O enriquecimento deve ser persistido quando apropriado, sempre com origem e rastreabilidade.

## 31. FASE 3 — AI KNOWLEDGE ENGINEER

### Objetivo

Transformar o trabalho operacional em conhecimento reutilizável.

Fluxo:

```text
Task concluída
      ↓
AI analisa task
      ↓
Extrai conhecimento
      ↓
Normaliza
      ↓
Busca duplicidade
      ↓
Sugere Knowledge
      ↓
Humano aprova
      ↓
Knowledge publicado
      ↓
Chunking
      ↓
Embedding
      ↓
Vector Store
```

### Fontes possíveis

- Task
- TESC
- PDI
- Incident
- Postmortem
- Justificativa
- Procedimento
- Documentação

## 32. Knowledge automático

Exemplo de Task:

> SW-04 sem comunicação.
> Foi identificado problema na porta GE1/0/20.
> Após correção, comunicação normalizada.

IA pode gerar:

```text
Título:
Switch SW-04 — perda de conectividade na porta GE1/0/20

Sintomas:
Dispositivos conectados à porta ficaram indisponíveis.

Diagnóstico:
Falha de conectividade na porta.

Procedimento:
Verificar estado da porta, VLAN e conectividade.

Resultado:
Conectividade restaurada.

Tags:
Switch
VLAN
Network
UniFi
```

## 33. Knowledge Review

Inicialmente:

```text
AI Generated
     ↓
Human Review
     ↓
Approved
     ↓
Published
```

Não publicar automaticamente na primeira versão.

Posteriormente:

- Confidence
- Source Quality
- Human Approval History

poderá determinar níveis de automação.

## 34. Knowledge + RAG

O Knowledge publicado alimentará o RAG:

```text
Knowledge
   ↓
Chunking
   ↓
Embedding
   ↓
Vector Database
   ↓
Index
   ↓
Hybrid Search
   ↓
Reranking
   ↓
Relevant Context
```

O RAG poderá encontrar:

```text
Knowledge #231
Task #1821
Task #1740
Incident #812
Document #91
```

## 35. Embeddings e Vector Store

A implementação deverá ser desacoplada do provider.

Componentes:

- IEmbeddingProvider
- IVectorStore
- IRetrievalService
- IReranker
- IContextBuilder

Não acoplar o domínio ao banco vetorial.

Se PostgreSQL + pgvector já fizer parte da arquitetura, avaliar seu uso antes de introduzir outro banco.

Regra:

- Reutilizar infraestrutura existente antes de adicionar outra tecnologia.

## 36. FASE 4 — AI REPORTING

### Objetivo

Gerar relatórios automaticamente usando dados calculados pelo backend.

Tipos:

- Daily Infrastructure Report
- Weekly Operations Report
- Monthly IT Report
- Incident Postmortem
- Executive IT Summary

## 37. Indicadores

A plataforma poderá trabalhar com:

- quantidade de incidentes;
- incidentes críticos;
- MTTR;
- MTBF;
- SLA;
- SLO;
- reincidência;
- ativos problemáticos;
- categorias de incidentes;
- volume de tasks;
- backlog;
- tendências;
- principais causas;
- disponibilidade;
- alertas;
- capacidade;
- evolução do Knowledge;
- tarefas concluídas;
- PDI;
- TESC.

Regra:

- A IA não calcula indicadores críticos sozinha.

O backend deve fornecer:

- valor
- período
- filtros
- fonte
- metodologia

A IA interpreta e explica.

## 38. FASE 5 — AI AUTOMATION AGENT

Não começar por aqui.

Inicialmente:

```text
Detectar
   ↓
Investigar
   ↓
Recomendar
   ↓
Humano aprova
   ↓
Workflow
```

Exemplo:

```text
Servidor sem serviço
       ↓
IA identifica problema
       ↓
Knowledge encontra procedimento
       ↓
IA recomenda reiniciar serviço
       ↓
[ APROVAR ]
       ↓
n8n / workflow
       ↓
executa
       ↓
Zabbix verifica
       ↓
resultado
       ↓
Task atualizada
       ↓
Conhecimento
```

## 39. Autonomia operacional

Criar níveis:

```text
LEVEL 0 — Read Only
Consultar
Analisar
Explicar

LEVEL 1 — Recommend
Consultar
Analisar
Recomendar

LEVEL 2 — Human Approval
Consultar
Analisar
Recomendar
Solicitar aprovação
Executar

LEVEL 3 — Controlled Autonomous
Detectar
Analisar
Executar ações previamente autorizadas
Validar resultado
Registrar
```

Começar obrigatoriamente no:

```text
LEVEL 0
```

## 40. Guardrails

A IA não pode, diretamente:

- apagar dados;
- alterar infraestrutura;
- reiniciar produção;
- modificar rede;
- alterar VLAN;
- executar comandos arbitrários;
- alterar usuários;
- alterar permissões.

Sem:

- Policy
- Permission
- Approval
- Explicit Tool
- Audit

Nunca:

```text
LLM
 ↓
Shell arbitrário
```

Preferir:

```text
LLM
 ↓
RestartServiceTool
 ↓
Policy
 ↓
Permission
 ↓
Approval
 ↓
Execution
 ↓
Validation
```

## 41. Segurança

A IA deverá respeitar:

```text
User
 ↓
Role
 ↓
Permission
 ↓
AI Tool
 ↓
Data
```

Um Viewer não poderá utilizar a IA para acessar informações que ele não poderia acessar diretamente.

Registrar:

- quem perguntou
- qual ferramenta foi utilizada
- quais dados foram consultados
- qual provider
- qual modelo
- qual ação foi executada
- qual política autorizou
- qual resultado ocorreu

## 42. Prompt Injection e Tool Security

Todo conteúdo recuperado deve ser tratado como dados, não como instruções confiáveis.

O agente deve considerar:

- Knowledge
- Documents
- Logs
- Incidents
- Tasks
- External APIs

como fontes potencialmente não confiáveis.

Nunca permitir que texto recuperado altere automaticamente:

- permissions
- system policies
- tool permissions
- execution level
- security rules

## 43. Observabilidade da IA

Registrar:

- AI Request
- AI Response
- Provider
- Model
- Latency
- Tokens
- Tools
- Errors
- Retries
- Cost
- Trace ID
- Investigation ID

OpenTelemetry:

```text
User Request
    ↓
AI Gateway
    ↓
Context Engine
    ↓
LLM
    ↓
MCP
    ↓
Zabbix / UniFi / InfraControl
    ↓
Response
```

Deve ser possível responder:

> "Por que essa investigação demorou 14 segundos?"

## 44. Runtime Health

Criar monitoramento da própria camada de IA.

Exemplos:

- Provider status
- Model status
- Ollama status
- Embedding status
- Vector Store status
- MCP status
- Tool health
- Latency
- Error rate
- Token usage

Exemplo:

```text
AI Runtime

Ollama              HEALTHY
Model               READY
Embedding           HEALTHY
Vector Store        HEALTHY
Zabbix Tool         HEALTHY
UniFi Tool          HEALTHY
MCP                 HEALTHY
```

## 45. Testes

Cada fase deverá possuir:

### Unit Tests

- prompts;
- policies;
- services;
- parsers;
- tool contracts;
- context builders;
- provider adapters.

### Integration Tests

- AI Gateway;
- Ollama;
- Providers;
- Zabbix;
- UniFi;
- Knowledge;
- Vector Store;
- MCP;
- PostgreSQL;
- Redis;
- RabbitMQ quando aplicável.

### AI Evaluation Tests

```text
Pergunta
→ contexto esperado
→ ferramentas esperadas
→ evidências esperadas
→ resposta esperada
```

Testar:

- hallucination;
- tool selection;
- authorization;
- grounding;
- confidence;
- citations;
- context quality;
- provider fallback;
- timeout;
- prompt injection;
- structured output.

## 46. Regra: documentação não é evidência de implementação

Uma capacidade só pode ser marcada como concluída quando houver evidência executável.

```text
Documentação
      ≠
Implementação
```

Para considerar uma feature pronta, exigir:

- Código executável
- Configuração
- Testes
- Execução validada
- Logs
- Evidência de sucesso

README dizendo que algo existe não comprova que existe.

## 47. Docker First

Toda nova dependência de infraestrutura deve ser tratada primeiro no ambiente Docker.

Exemplos:

- Ollama
- PostgreSQL
- Redis
- RabbitMQ
- Vector Store
- Observabilidade
- MCP Server

Processo:

1. Identificar dependência
2. Verificar se já existe
3. Verificar porta
4. Verificar volume
5. Verificar network
6. Verificar recursos
7. Criar/alterar Compose
8. `docker compose config`
9. `docker compose up -d <service>`
10. Health check
11. Testar conectividade
12. Registrar resultado
13. Só então integrar aplicação

Não instalar serviços aleatoriamente no host sem justificativa arquitetural.

## 48. Comandos de validação Docker

O agente deverá adaptar os comandos ao projeto, mas o padrão é:

```text
docker --version
docker compose version

docker compose config

docker compose ps

docker compose up -d <service>

docker compose ps

docker compose logs --tail=100 <service>
```

Health check:

```text
docker inspect <container>
```

Conectividade entre containers deve usar:

```text
service-name:port
```

e não:

```text
localhost:port
```

quando a comunicação ocorre dentro da mesma rede Docker.

## 49. InfraControl AI Docker

A estrutura deverá evoluir sem duplicar infraestrutura existente.

Exemplo conceitual:

```text
docker-compose.yml
docker-compose.infra.yml
docker-compose.observability.yml
```

Antes de criar outro Compose, verificar os arquivos existentes.

O agente deve primeiro mapear:

- Postgres
- Redis
- RabbitMQ
- Seq
- OpenTelemetry
- InfraControl API
- InfraControl Worker
- Frontend
- Nginx
- Zabbix
- Ollama

e somente adicionar o necessário.

## 50. Estrutura sugerida de código

Antes de criar qualquer pasta, verificar a estrutura real do repositório.

Conceitualmente:

```text
src/
  Modules/
  BuildingBlocks/
  AI/
    Domain/
    Application/
    Infrastructure/
    Api/
    Workers/
```

Possíveis componentes:

```text
AI
├── Provider
├── Gateway
├── Context
├── RAG
├── Embeddings
├── Tools
├── MCP
├── Investigation
├── Knowledge
├── Reporting
├── Policies
├── Audit
└── Evaluation
```

Não criar essa estrutura cegamente.

Reutilizar os padrões já existentes no InfraControl.

## 51. AI Context Engine — contratos conceituais

Possíveis contratos:

- IAiContextEngine
- IContextSource
- IRetrievalService
- ISemanticSearch
- IKeywordSearch
- IStructuredSearch
- IVectorStore
- IEmbeddingProvider
- IReranker
- IEvidenceCollector
- IContextBuilder
- ISourceRanker

Os nomes finais devem seguir as convenções existentes do projeto.

## 52. Investigation Tool Contract

Toda tool deverá ter:

- Name
- Description
- Input Schema
- Output Schema
- Required Permission
- Timeout
- Max Results
- Audit Policy
- Source

Exemplo:

```text
Tool:
get_zabbix_problems

Permission:
Monitoring.Read

Input:
{
  hostId?,
  severity?,
  timeRange?
}

Output:
{
  problems[],
  total,
  source,
  timestamp
}
```

## 53. Structured AI Output

Quando possível, preferir respostas estruturadas.

Exemplo:

```json
{
  "summary": "...",
  "findings": [],
  "evidence": [],
  "hypotheses": [],
  "confidence": "PROBABLE",
  "recommendations": [],
  "sources": []
}
```

Evitar depender exclusivamente de texto livre para dados que serão processados pelo sistema.

## 54. Fallback de Provider

Fluxo:

```text
Primary Provider
      ↓
Timeout/Error
      ↓
Policy
      ↓
Fallback Provider
      ↓
Retry
      ↓
Response
```

O fallback deve respeitar:

- disponibilidade;
- capacidade do modelo;
- permissões;
- política;
- custo;
- contexto;
- compatibilidade.

Registrar o motivo da troca.

## 55. AI Cost Control

Registrar:

- Provider
- Model
- Tokens Input
- Tokens Output
- Total Tokens
- Estimated Cost
- Request
- User
- Module
- Capability

Para Ollama:

```text
Estimated Cost = local / infrastructure cost
```

quando aplicável.

Criar limites:

- max tokens
- max tool calls
- max context size
- max investigation duration

## 56. AI Command Center

Criar posteriormente:

```text
AI COMMAND CENTER

Dashboard:
┌────────────────────────────────────────────┐
│ AI COMMAND CENTER                          │
├────────────────────────────────────────────┤
│                                            │
│ Active Investigations          4           │
│ AI Recommendations             7           │
│ Knowledge Pending Review        5           │
│ Correlated Incidents            3           │
│ Automation Pending Approval     2           │
│                                            │
├────────────────────────────────────────────┤
│ Recent AI Activity                         │
│                                            │
│ 12:40 Incident investigation               │
│ 12:31 Knowledge generated                  │
│ 11:52 Incident correlation                 │
│ 10:20 Report generated                     │
└────────────────────────────────────────────┘
```

## 57. Onde a IA aparece visualmente

### Global

- Topbar → AI Copilot

### Incident

- Incident → AI Investigation → Ask AI

### Task

- Task → Summarize
- Task → Generate Knowledge
- Task → Similar Tasks

### Knowledge

- Knowledge → Improve
- Knowledge → Find Similar
- Knowledge → Generate Embedding
- Knowledge → Related Incidents

### Asset

- Asset → AI Analysis
- Asset → History
- Asset → Related Incidents
- Asset → Recommendations

### Dashboard

- Dashboard → AI Summary

### Reports

- Reports → Generate with AI

## 58. Design da IA

### Evitar

- ChatGPT clone

Criar interface operacional.

Priorizar:

- cards;
- evidências;
- timeline;
- badges;
- confidence;
- recommendations;
- actions;
- citations;
- tool activity;
- investigation steps;
- sources.

A IA deve parecer parte do sistema operacional de TI.

## 59. ADRs obrigatórios

Criar/atualizar em `/docs/adr/`:

- ADR-001-ai-layer.md
- ADR-002-mcp-strategy.md
- ADR-003-rag-strategy.md
- ADR-004-provider-abstraction.md
- ADR-005-autonomous-actions.md
- ADR-006-ai-context-engine.md
- ADR-007-ai-security.md
- ADR-008-ai-docker-runtime.md

Antes de criar um ADR, verificar se já existe numeração/padrão equivalente.

> Nota de arquivamento (2026-10-07): este repo já está em ADR-035 — os
> ADRs de IA continuarão a partir de **ADR-036**, preservando o padrão
> `docs/adr/ADR-NNN-ARQUIVO.md` existente (os nomes acima são os
> temas/sugestões originais da blueprint).

## 60. Documentação obrigatória

Criar/atualizar em `/docs/ai/`:

- 00-vision.md
- 01-architecture.md
- 02-ai-gateway.md
- 03-provider-manager.md
- 04-mcp.md
- 05-tools.md
- 06-context-engine.md
- 07-rag.md
- 08-knowledge-engine.md
- 09-investigation-engine.md
- 10-ai-security.md
- 11-ai-observability.md
- 12-ai-evaluation.md
- 13-ai-prompts.md
- 14-ai-automation.md
- 15-docker-runtime.md

## 61. Roadmap final

Não criar mais fases.

Manter:

```text
FASE 0 — AI FOUNDATION
    ↓
FASE 1 — AI COPILOT
    ↓
FASE 2 — AI INVESTIGATION
    ↓
FASE 3 — AI KNOWLEDGE ENGINEER
    ↓
FASE 4 — AI REPORTING
    ↓
FASE 5 — AI AUTOMATION
```

Detalhamento:

```text
FASE 0
├── AI Gateway
├── Provider Abstraction
├── Provider & Model Manager
├── Ollama Docker
├── External Providers
├── Model Discovery
├── Security
├── Permissions
├── Audit
├── Observability
├── Runtime Health
├── MCP Foundation
├── AI Evaluation
└── AI Context Engine
    ├── RAG
    ├── Embeddings
    ├── Vector Store
    ├── Semantic Search
    ├── Keyword Search
    ├── Hybrid Search
    ├── Structured Search
    ├── Metadata Filtering
    ├── Reranking
    ├── Context Builder
    └── Evidence Model

FASE 1
├── Copilot
├── AI Chat
├── Context
└── Tools

FASE 2
├── Investigation Engine
├── Evidence
├── Correlation
├── Timeline
├── Hypotheses
├── Confidence
├── Incident Enrichment
└── Recommendations

FASE 3
├── Task → Knowledge
├── TESC → Knowledge
├── PDI → Knowledge
├── Incident → Knowledge
├── Duplicate Detection
├── Review
├── Approval
├── Embedding
└── RAG ingestion

FASE 4
├── Daily
├── Weekly
├── Monthly
├── Incident
└── Executive

FASE 5
├── Recommendation
├── Approval
├── Policy
├── n8n
├── Controlled Execution
└── Validation
```

## 62. Sprint 0 — ordem de implementação

Antes de Copilot, começar por:

1. INVESTIGAR REPOSITÓRIO
2. MAPEAR ARQUITETURA EXISTENTE
3. MAPEAR DOCKER
4. MAPEAR POSTGRES
5. MAPEAR REDIS
6. MAPEAR RABBITMQ
7. MAPEAR OBSERVABILIDADE
8. MAPEAR INTEGRAÇÕES
9. MAPEAR AUTENTICAÇÃO/PERMISSÕES
10. MAPEAR INCIDENT/ASSET/TASK/KNOWLEDGE

Depois:

11. ADR AI LAYER
12. AI Gateway
13. Provider abstraction
14. Provider & Model Manager
15. Ollama Docker
16. Runtime Health
17. AI Audit
18. AI Observability
19. AI Security
20. MCP foundation
21. Tool contracts
22. AI Context Engine
23. RAG foundation
24. Evaluation foundation

Somente após a fundação:

```text
FASE 1 — COPILOT
```

## 63. Definition of Done

Uma funcionalidade de IA só será considerada concluída quando possuir:

- [ ] Código
- [ ] Testes
- [ ] Autorização
- [ ] Logs
- [ ] Telemetria
- [ ] Tratamento de erros
- [ ] Documentação
- [ ] UI quando aplicável
- [ ] Auditoria
- [ ] Prompt documentado
- [ ] Tool documentada
- [ ] Evaluation test
- [ ] Docker/configuração validada quando houver dependência
- [ ] Health check
- [ ] Evidência de execução

## 64. Regra de Vibe Coding

Os agentes podem escrever muito código.

Mas o processo será:

```text
INVESTIGATE
     ↓
PLAN
     ↓
DOCKER / ENVIRONMENT
     ↓
VALIDATE
     ↓
IMPLEMENT
     ↓
TEST
     ↓
REVIEW
     ↓
EXECUTE
     ↓
OBSERVE
     ↓
DOCUMENT
```

Nunca:

```text
PROMPT
 ↓
1000 arquivos modificados
 ↓
"Done bro"
```

O agente deverá manter mudanças:

- pequenas;
- rastreáveis;
- reversíveis;
- testáveis.

## 65. Prompt operacional dos Vibe Coders

Todo agente que trabalhar no projeto deverá receber:

> Você está trabalhando no InfraControl.
>
> Antes de modificar código, investigue a arquitetura existente.
>
> Não crie componentes, serviços, entidades ou integrações duplicadas sem verificar se já existe implementação equivalente.
>
> Preserve Clean Architecture, DDD, CQRS, Vertical Slice e Modular Monolith.
>
> Preserve os limites dos módulos.
>
> Zabbix é fonte de monitoramento.
>
> UniFi é fonte de contexto de rede.
>
> n8n é ferramenta de workflow.
>
> InfraControl é o sistema central.
>
> IA é uma camada de interpretação, investigação, conhecimento, recomendação e automação controlada.
>
> MCP é mecanismo de exposição de ferramentas para a IA, não o domínio da aplicação.
>
> RAG fornece conhecimento semântico e histórico; não substitui fontes operacionais em tempo real.
>
> Nunca invente dados.
>
> Sempre diferencie fato, evidência, hipótese e recomendação.
>
> Toda ação potencialmente destrutiva exige política e autorização.
>
> A IA começa no LEVEL 0 — Read Only.
>
> Nunca permita LLM → Shell arbitrário.
>
> Nunca permita LLM → Banco diretamente.
>
> Use LLM → Tool → Policy → Permission → Approval → Execution quando houver ações.
>
> Antes de adicionar dependências de infraestrutura, investigue o Docker existente.
>
> Prepare o Docker/configuração antes de executar.
>
> Valide docker compose config, containers, networks, volumes, portas, health checks e conectividade antes de integrar a aplicação.
>
> Não execute comandos destrutivos sem autorização explícita.
>
> Toda alteração arquitetural relevante deve ser documentada como ADR.
>
> Antes de implementar mudanças complexas, produza um plano técnico baseado no código existente.
>
> Você possui autonomia para investigar documentação, dependências, APIs, código e alternativas técnicas.
>
> Porém, não altere os princípios arquiteturais do produto sem justificativa documentada.
>
> Ao finalizar uma implementação, informe:
>
> 1. O que foi alterado.
> 2. Onde foi alterado.
> 3. Por que foi alterado.
> 4. Quais arquivos foram criados/modificados.
> 5. Quais containers foram criados/modificados.
> 6. Quais testes foram executados.
> 7. Quais comandos foram executados.
> 8. Qual foi a evidência de sucesso.
> 9. Quais limitações existem.
> 10. Quais decisões futuras permanecem em aberto.

## 66. Prompt para iniciar o projeto

Use este prompt para o primeiro agente:

> Você está trabalhando no InfraControl.
>
> NÃO implemente nada ainda.
>
> Sua primeira tarefa é INVESTIGAR o projeto existente.
>
> Objetivo: preparar a implementação da AI Foundation do InfraControl.
>
> Execute somente investigação e planejamento nesta etapa.
>
> Analise:
>
> 1. Estrutura da solução
> 2. Projetos .NET
> 3. Modules
> 4. Clean Architecture
> 5. DDD
> 6. CQRS/MediatR
> 7. Vertical Slices
> 8. PostgreSQL
> 9. Redis
> 10. RabbitMQ/MassTransit
> 11. Identity
> 12. Roles/Permissions
> 13. React
> 14. API
> 15. Workers
> 16. Docker Compose
> 17. Networks
> 18. Volumes
> 19. Ports
> 20. Health checks
> 21. OpenTelemetry
> 22. Serilog/Seq
> 23. SignalR
> 24. Zabbix integration
> 25. Inventory
> 26. Incidents
> 27. Tasks
> 28. Knowledge
> 29. Existing AI/LLM code
> 30. Existing MCP code
> 31. Existing vector/RAG code
>
> NÃO crie novos componentes antes de verificar se já existe implementação equivalente.
>
> Depois produza:
>
> A. Arquitetura encontrada
> B. Componentes reutilizáveis
> C. Componentes inexistentes
> D. Integrações existentes
> E. Docker existente
> F. Dependências que precisam ser adicionadas
> G. Riscos
> H. ADRs necessários
> I. Plano da Fase 0
> J. Ordem exata de implementação
>
> REGRA DOCKER:
>
> Antes de qualquer execução de nova dependência:
>
> 1. Verificar Docker.
> 2. Verificar Docker Compose.
> 3. Verificar containers.
> 4. Verificar networks.
> 5. Verificar volumes.
> 6. Verificar portas.
> 7. Verificar recursos.
> 8. Preparar Compose.
> 9. Validar com docker compose config.
> 10. Subir somente o serviço necessário.
> 11. Validar health check.
> 12. Validar conectividade.
>
> NÃO instale Ollama, Vector Store, MCP Server ou qualquer nova infraestrutura antes dessa análise.
>
> Ao final, NÃO implemente.
>
> Entregue somente:
>
> - diagnóstico;
> - arquitetura atual;
> - gaps;
> - dependências;
> - plano técnico;
> - plano Docker;
> - ADRs necessários;
> - ordem de execução;
> - critérios de aceite.

## 67. Critério para avançar de fase

Não avançar apenas porque o código compilou.

### Fase 0 → Fase 1

Exigir:

- AI Gateway funcionando
- Provider Manager funcionando
- Ollama funcionando
- Modelo testado
- Runtime Health funcionando
- Audit funcionando
- Observabilidade funcionando
- Permissions funcionando
- MCP foundation funcionando
- Context Engine funcionando
- RAG foundation funcionando
- Tool contracts funcionando
- Evaluation foundation funcionando
- Docker validado
- Testes passando

### Fase 1 → Fase 2

Exigir:

- Copilot funcionando
- Tools funcionando
- Contexto real funcionando
- Grounding validado
- Authorization validada
- Evaluation passando

### Fase 2 → Fase 3

Exigir:

- Investigation funcionando
- Evidence funcionando
- Timeline funcionando
- Correlation funcionando
- Confidence funcionando
- Audit funcionando

E assim sucessivamente.

## 68. Princípio final

O InfraControl não deve se transformar em:

```text
Dashboard + ChatGPT
```

Deve evoluir para:

```text
                    INFRA CONTROL

        ITSM ─── INVENTORY ─── MONITORING
          │          │             │
          └──────────┼─────────────┘
                     │
                 KNOWLEDGE
                     │
                     ▼
               AI CONTEXT ENGINE
                     │
        ┌────────────┼────────────┐
        │            │            │
       RAG         TOOLS        DATA
        │            │            │
        └────────────┼────────────┘
                     │
              INVESTIGATION
                     │
        ┌────────────┼────────────┐
        │            │            │
     COPILOT     ANALYST      REPORTING
        │            │            │
        └────────────┼────────────┘
                     │
               RECOMMENDATION
                     │
              HUMAN APPROVAL
                     │
                 AUTOMATION
                     │
                    n8n
                     │
               INFRASTRUCTURE
                     │
                 VALIDATION
                     │
             NOVO CONHECIMENTO
```

A evolução desejada:

```text
Monitoramento
      ↓
Detecção
      ↓
Contextualização
      ↓
Investigação
      ↓
Correlação
      ↓
Conhecimento
      ↓
Recomendação
      ↓
Aprovação
      ↓
Automação
      ↓
Validação
      ↓
Novo conhecimento
```
