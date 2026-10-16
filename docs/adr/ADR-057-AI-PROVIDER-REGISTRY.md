# ADR-057: Providers de IA cadastrados na plataforma (descentralização por empresa/demo)

- **Status**: aceito (2026-10-15 — D1/D2/D3 fechados nas alternativas
  recomendadas; usuário direcionou continuidade. Reversível por nova ADR.)
- **Data**: 2026-10-15
- **Tasks**: `FAI-T011` (TASKS.md); depende de `AI-ENV-003` (baseline do
  schema do banco dev — sem `__EFMigrationsHistory` o ambiente novo
  defasa de novo)

## Contexto

Hoje o Provider & Model Manager é **v1 por configuração** (ADR-037 §2):
`Ai:Ollama` / `Ai:OpenAiCompatible` / `Ai:Gemini` no `appsettings` +
envs do compose (`Ai__*` em `docker-compose.yml`), com `Ai:Routing`
escolhendo provider/modelo por capacidade (§15) e a registry de
configuration resolvendo por `Name`. O ADR-037 §11–§12 **já previa** a
evolução "banco + UI Admin".

Pedido do usuário (2026-10-15): provider de IA passa a ser **cadastro
dentro da plataforma**, para dar espaço quando surgirem **novas demos
ou novas empresas** — descentralizar o cadastro, sem redeploy por
provider novo. **Ollama permanece o único provider default** (padrão
local, LB ADR-041); OpenAI-compatible, Gemini e futuros entram via
cadastro.

O smoke test ao vivo (2026-10-15, TASKS.md §"Smoke test ao vivo da IA")
validou as 7 superfícies com o runtime atual — esta ADR não muda
contratos de chat/investigação/etc., só **de onde vem a resolução de
provider**.

## Decisão (proposta)

1. **Cadastro persistido vira fonte da resolução de provider.**
   Tabela(s) novas (migração EF obrigatória) no lugar do registry de
   configuration. A config `Ai__*` continua existindo como **fallback
   de compatibilidade** (estado "não configurado" §44 preservado:
   sem cadastro e sem config → degradado, nunca resposta inventada).
2. **Ollama é o único provider default embarcado.** Seed idempotente
   cria o cadastro `ollama` apontando para o LB do compose
   (`OLLAMA_BASE_URL`, ADR-041). Um deploy novo já nasce com IA
   funcional sem cadastro manual; remover/renomear o seed não é
   suporte (escape hatch = config).
3. **Modelos e routing por capacidade também no cadastro.** Cada
   provider cadastrado declara seus modelos; o assignment
   `capacidade → provider + DefaultModel + Fallback` espelha o
   `AiRoutingModelOptions` atual (§15/§54) em tabela própria,
   auditável e editável pela UI (recomendado; alternativa JSON no
   cadastro rejeitada por dificultar validação/consulta).
4. **Provider concreto = implementação em código, cadastro = endereço
   e credencial.** `OllamaProvider` / `OpenAiCompatibleProvider` /
   `GeminiProvider` continuam sendo os únicos adaptadores (contrato
   `IAiProvider`, ADR-050 §1); o cadastro só parametriza
   `Name/BaseUrl/ApiKey/Models/Ativo`. Novo provedor exige código +
   cadastro (o cadastro sozinho não instancia SDK).
5. **API Key: protegida em repouso, mascarada em toda superfície.**
   Chave nunca em log/audit (`F0-T014` já proíbe payload),
   nunca em resposta de leitura (mostrar só sufixo), nunca em
   exception. Mecanismo: **D1 fechada** (ICredentialProtector/AES-GCM —
   F11-T006, o mesmo do RemoteAccess/UniFi em código).
6. **Superfície Admin.** CRUD `Administer` (policies existentes) +
   "testar conexão" (§13) e "listar modelos" (§14) reaproveitando o
   contrato `IAiProvider` já implementado — sem novo contrato de
   teste. Health `ai-runtime` (§44) passa a ler do cadastro.
7. **Escopo do cadastro.** **D2 fechada**: global na v1 (o produto não
   tem modelo de tenant hoje); evolução por empresa/demo = nova ADR
   quando o domínio tiver empresa.

## Decisões fechadas (2026-10-15)

| # | Questão | Decisão |
|---|---------|---------|
| D1 | **Mecanismo da ApiKey em repouso** | **(A) `ICredentialProtector`** (AES-256-GCM, F11-T006) — o "mesmo padrão do RemoteAccess" em código é esta porta, não `IDataProtectionProvider`; chave de `RemoteAccess:CredentialEncryption:Key`/`CREDENTIAL_ENCRYPTION_KEY`, já usada pelo UniFi/Zabbix. Descoberto na implementação do FAI-T011a; vault externo = evolução futura (novo ADR) |
| D2 | **Escopo por empresa/demo** | **(A) cadastro global na v1**, com nota de evolução — o produto não tem modelo de tenant hoje; inventar um aqui seria arquitetura sem requisito. Quando existir empresa/tenant no domínio, nova ADR estende o escopo |
| D3 | **Fallback para config** | **(A) config `Ai__*` lida só quando o cadastro não tem o provider** — deploy legado não quebra; sem cadastro e sem config → estado "não configurado" §44 |

## Consequências

- Migração EF nova — **bloqueio prático**: `AI-ENV-003` (ambiente dev
  sem histórico de migrações) precisa de baseline antes, senão o schema
  novo repete o débito do Context Engine (colunas/tags que faltavam).
- Workers herdam a mesma resolução (mesma DI/`Ai:*`).
- ADR-037 §2/§11–§12 fica concretizado; ADR-050 (multi-provider)
  permanece válido por cima (cadastro parametriza, não substitui,
  os providers).
- UI nova de Admin (página "Providers de IA") — escopo de task
  separado (FAI-T011 deve ser quebrada em verticais, padrão F30).
- Risco controlado de regressão: os7 endpoints validados no smoke
  mudam apenas a origem da resolução — suíte `InfraControl.UnitTests.AI`
  + smoke ao vivo são o gate.

## Referências

- ADR-037 (Provider & Model Manager v1 — §2, §11–§12, §15, §54)
- ADR-050 (multi-provider: OpenAI-compatible/Gemini)
- ADR-041 (runtime Ollama no compose, LB 3 réplicas)
- ADR-040/§41–§42 (auditoria AI.*, conteúdo recuperado = dado)
- TASKS.md: FAI-T011, AI-ENV-001..003 (smoke test 2026-10-15)
