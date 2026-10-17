# 54 — Checklist de caça-bugs (front · back · arquitetura · integração · CQRS · cache · mensageria · CRUD)

> **Propósito**: roteiro de caça dirigida a bugs neste repositório. Cada item diz
> **o que atacar**, **onde olhar**, **assinatura típica** e **como provar**.
> Use junto do skill `bug-hunter` (scan adversarial) — este doc dá o mapa do terreno.
>
> **Regras da casa (AGENTS.md)**: scan não altera código; achar ≠ corrigir sem
> autorização; nunca esconder teste quebrado; decisão arquitetural exige ADR.

## Como usar

1. Escolha a(s) superfície(s) do alvo (seções abaixo).
2. Para cada item: leva o **par de arquivos** indicado, procura a assinatura, só então abre o diff/revisão.
3. Regrue o resultado em severidade: `Crítico` (dado/exploit) > `Alto` (quebra fluxo) > `Médio` (degrada) > `Baixo` (cosmético).
4. Achado sem PoC/repro não é achado — é palpite. Escreva o passo de repro.

**Superfície de ataque em ordem de ROI neste repo**: ① invalidação de cache
(contrato string-frágil) → ② transação × re-fetch AsNoTracking → ③ policies/RoleAccess
→ ④ outbox/mensageria → ⑤ credenciais em Values/logs → ⑥ guards de CRUD (404/409) →
⑦ race de UI (toggle/persistência) → ⑧ N+1 de resolução de nomes.

---

## 1. Pipeline CQRS (o coração — ataque primeiro)

Ordem real de behaviors
(`src/InfraControl.Infrastructure/Services/DependencyInjection.cs:299-310`):
`CorrelationId → Logging → Performance → Validation → Idempotency → Audit →
CacheInvalidation → Authorization → Caching → Observability → Transaction → RateLimiting`.

- [ ] **Command sem `BaseCommand`/`BaseQuery` cru** — todo slice novo usa `BaseCommand`/`BaseQuery` + `IAuthorizedRequest`
  (`BuildingBlocksStandardizationTests` lista os Admin_Slices; `IRequest`/`ICommand` cru passa reto por AuthorizationBehavior).
  *Assinatura*: endpoint acessível sem role / teste de arquitetura quebrando.
- [ ] **`IQuery` herdado por engano em mutation** — `TransactionBehavior` **não** chama `SaveChanges` para `IQuery`
  (`TransactionBehavior.cs:16-17`). Mutation marcada como query grava no contexto e nunca persiste (ou persiste na mão e quebra o UnitOfWork).
  *Prova*: handler muta entidade tracked e retorna 200; reload mostra estado antigo.
- [ ] **Duplo commit / SaveChanges no handler** — o `TransactionBehavior` já chama `unitOfWork.SaveChangesAsync` depois do handler.
  Handler que chama `SaveChangesAsync` próprio divide a transação (metade commita, metade não em erro posterior).
- [ ] **Re-fetch `AsNoTracking` pós-mutação** — precedente conhecido: `SyncSwitchPortsFromUnifiCommand` re-busca AsNoTracking e perde tracking
  (comentário documentado no código). Todo comando que "cria e lista" deve re-buscar **tracked** ou montar a resposta do próprio aggregate.
  *Assinatura*: resposta da API mostra o objeto **antes** da mutação.
- [ ] **Behavior de ordem** — ao adicionar behavior novo, relembre: registrado primeiro = externo. Invalidation roda **após** o commit do Transaction
  (Transaction é interno a CacheInvalidation) — janela pequena de cache velho entre commit e remoção de chave; anote se o cenário for financeiro/alerta.
- [ ] **`IdempotencyBehavior` não cobre o seu comando** — POSTs de criação com retry de rede podem duplicar se o request não passa pelo store de idempotência.

## 2. Comandos (Command)

- [ ] **Guard de sessão ausente** — handler que lê `ICurrentUserContext` deve falhar feio sem sessão
  (molde: `CreateTaskCommandHandler` → `UnauthorizedAccessException`; `CreateProjectCommandHandler`).
  *Assinatura*: `NullReferenceException`/GUID vazio virando `CreatedById` nulo silencioso.
- [ ] **`Guid.Empty` aceito em FK de negócio** — `TaskItem.Create` rejeita `Guid.Empty` em `CreatedById`; verifique novas FKs opcionais
  (null ok, empty não). *Assinatura*: linha no banco com `00000000-…` que nunca joina.
- [ ] **404 vs 400 vs 409 trocados** — alvo inexistente = 404 pt-BR; tipo errado do alvo = 400; conflito de estado (canal duplicado, device ocupado) = 409.
  Molde: `CameraNvrMappingCommands`, `VirtualMachineGuards` (F31-T002), `DeleteDevice` (Has* → 409 antes das interfaces).
- [ ] **Guard de tipo de device faltando** — checks/commands de device assumem tipo (`RunCheckCommands` Camera|Nvr; `DeviceProfileDefaults`).
  *Assinatura*: NVR roda check de câmera com semântica errada, ou 500 em null de enum.
- [ ] **`CacheKeysToInvalidate` incompleto** — comando que muda 2 entidades deve invalidar as chaves das 2 listas
  (ver §4). *Assinatura*: mutation OK mas listagem stale até TTL.
- [ ] **`RoleAccess` mais permissivo que o vizinho** — CRUD espelha policies do módulo irmão (Projetos ↔ PDI ADR-043);
  um `Operate` em rota que os pares exigem `Admin` é bug de autorização, não "diferença de design".

## 3. Queries e leitura

- [ ] **N+1 de resolução de nomes** — `TaskQueries` resolve nomes em **batch** (`GetUserNamesAsync`); Device/Virtualization resolvem por lote
  ou read-model em memória (`GetVirtualizationOverviewQuery`, molde F3-T006). Query nova que itera entidade e busca 1×1 por linha = N+1.
  *Prova*: log EF de N SELECTs numa listagem de 20 linhas.
- [ ] **Filtro por enum quebrado com string no banco** — enums viraram `HasConversion<string>` (§8); query que compara por **valor numérico**
  (`== 2`) ou ordena por enum persistido falha em produção mas passa em teste unitário com in-memory.
- [ ] **Paginação sem teto** — `page`/`pageSize` vindos do cliente sem clamp; `SearchAsync` com pageSize=100000.
- [ ] **`AsNoTracking` em handler de query que o comando seguinte espera mutar** — só perigoso se misturado no mesmo contexto/scoped (ver §1).
- [ ] **Resposta vazia ≠ erro** — `GetSwitchUnifiPortsQuery` devolve lista vazia (não 500) quando device sai do controller; replicar esse contrato nos novos
  "fonte externa pode sumir". 500 em fonte externa ausente = bug.

## 4. Cache e invalidação (ataque de alto ROI)

- [ ] **Chave de cache string-frágil** — `CachingBehavior` + `ICacheableQuery` usam chave montada à mão; `CacheKeysToInvalidate` tem que bater **byte a byte**
  (mesmo casing, mesmos parâmetros relevantes). Não há wildcard/prefix-wipe (regra AGENTS: não inventar).
  *Assinatura*: bug "some quando reinicio a API". *Prova*: mutar → listar sem esperar TTL → dado velho; log `CacheInvalidationBehavior` com a chave removida ≠ chave lida.
- [ ] **Parâmetro de filtro fora da chave** — query cacheável por `(term, page)` que ganhou `status` sem entrar na chave devolve resultado de outro filtro.
- [ ] **Cache cross-tenant/cross-user** — checar se query cacheável embute dado sensível à identidade (papéis, e-mail) e a chave não inclui userId/role.
- [ ] **Invalidação órfã** — `ICacheInvalidatingRequest` sem chave correspondente a nenhuma `ICacheableQuery` (remoção de chave que ninguém lê) é sinal de contrato quebrado.
- [ ] **Mutação sem interface de invalidação** — comando novo que altera entidade listada em página cacheada e não implementa `ICacheInvalidatingRequest`.

## 5. Mensageria, outbox e canais

- [ ] **Handler de evento que não passa pelo outbox** — persistência + publicação RabbitMQ precisa do padrão outbox
  (`IOutboxRepository`, `OutboxDispatchHandler`, tabela `outbox_messages`). Publicar direto no handler = perda de evento se o commit falhar.
- [ ] **Worker que engole exceção** — `Notification*Worker` (Email/SignalR/Slack/Telegram/WhatsApp/Webhook) herdam de `NotificationChannelWorkerBase`;
  catch genérico sem log/dead-letter esconde falha de canal. *Prova*: derrubar o canal e ver `attempt_count` subir sem alarme.
- [ ] **Retry sem teto/idempotência** — `notification_deliveries.max_retries`/`attempt_count`; webhook que reenvia o mesmo evento duplica efeito no destino.
- [ ] **Fila/outbox sem índice de polling** — `outbox_messages` com `Order`/status; polling full-scan trava com volume (ver retenção e cleanup).
- [ ] **SignalR × Redis backplane** — `AddStackExchangeRedis` + `SignalRRedisConnectionProvider` (`Program.cs:72-85`); user id provider errado entrega notificação
  para a sala errada (bug clássico: notificação "some" em multi-instância sem Redis ou com `IUserIdProvider` divergente).
- [ ] **Mensagem com credencial em payload/log** — senha ONVIF/ApiKey AI nunca em `Values`, mensagens ou logs (redação total — lição F30-T005a).

## 6. Integrações externas (UniFi · Zabbix · SNMP · ONVIF · IA)

- [ ] **Timeout x política** — `AiGatewayOptions.EffectiveTimeout + 15s` no HttpClient (AI-ENV-002): policy dispara antes, HttpClient = safety net.
  Novo cliente HTTP sem essa cascata = 504 do nginx ou `TaskCanceledException` virando 502.
- [ ] **"Não suportado" ≠ exceção** — ONVIF: Fault/resposta incompleta → **null** ("não suportado neste modelo"); só transporte/parse quebrado lança.
  Provider que trata null como erro degrada modelo barato à toa.
- [ ] **Parse de campo ausente vs falso** — `OnvifRecordingStatus` usa tri-state (`bool?`); campo ausente ≠ false. Espalhar `false` por default
  cria alerta falso de "gravação parada".
- [ ] **Normalização de entrada externa** — MAC sem separador, IP lowercase, `speed` 0 = down, `poe_power` string vs número (parsers UniFi já
  engoliaram lixo no passado). Entrada nova do mesmo formato = mesmo parser.
- [ ] **Credencial por device vs global** — `OnvifOptions` global não cobre device com senha própria (pendência conhecida F30-T005c);
  ao implementar, não vazar senha no `DeviceCredential` para DTO/resposta.
- [ ] **Degradação sem derrubar ingestão** — `fix(ai): ingesta degrada sem IVectorStore` (6d1c9f3): dependência opcional ausente derrubava 16 E2E.
  Testar caminho "componente não configurado".
- [ ] **IPv6/colchetes em Uri.Host** — achado real do ONVIF (fixture com prefixos SOAP; `Uri.Host` de IPv6 vem com colchetes).

## 7. CRUD, domínio e banco

- [ ] **FK sem política de exclusão explícita** — precedente: NVR `Cascade`, hypervisor/vCenter `Restrict` + guard de 409 **antes**
  (`HasCameraNvrMappingsAsync`, `HasLinkedVirtualMachinesAsync`). Entidade nova com FK "default" herda Cascade inesperado e apaga cascata.
- [ ] **Constraint unique ausente no nível certo** — `(NvrId, ChannelNumber)`, `(UserId, Role)` (DomainUserRoles), `(article_id, version)`.
  Unicidade só no handler = corrida duplica linha. *Prova*: dois POSTs paralelos.
- [ ] **Chave gerada no domínio marcada como `ValueGeneratedOnAdd`** — lição F18-T004 (`UserRoleAssignment.Id` + `ValueGeneratedNever`):
  DetectChanges anexava como Modified → UPDATE 0 linhas → 500. Toda chave "de loja" preenchida pelo domínio precisa `ValueGeneratedNever`.
- [ ] **Enum persistido como int** — regra da casa: **todo enum no banco é string** (`HasConversion<string>`; varredura 2026-10-17 eliminou os
  últimos 2: `invitations.status`, `invitation_roles.role`). Novo enum em config sem conversão = bug. Migração de conversão precisa de
  `USING CASE` no Postgres (scaffold padrão perde dados).
- [ ] **Coluna PascalCase × snake_case** — tabela `tasks` usa PascalCase (`CreatedById`, `DueDate`); demais tabelas snake_case.
  `HasColumnName` errado compila e explode só em runtime/migração.
- [ ] **Migração não idempotente × dev remendado** — dev já sofreu patch manual (AI-ENV-003): migração `AddColumn` sobre coluna remendada quebra.
  Em revisão de migração: "e se a coluna já existir?".
- [ ] **Soft delete / dado histórico** — retenção (`RetentionPolicy`, cleanup de eventos) e exclusão de device com evidence `SetNull`
  (`MonitoringEventConfiguration`): nova entidade precisa decidir SetNull vs Cascade vs Restrict e bater com a retenção.

## 8. Frontend (React + Vite)

- [ ] **Estado de toggle/persistência** — `localStorage["tasks:showCompleted"]` (molde F32-T002); parse defensivo de JSON corrompido
  (`JSON.parse` de `null`/lixo = crash na abertura da página).
- [ ] **Corrida de auto-seleção** — lição `ServersPage`/`PrintersPage`: efeito que seleciona o 1º item + refetch = estado selecionado
  sumindo (aula de corrida já documentada em teste). Efeito novo com `[data]` na deps precisa de guard.
- [ ] **`findBy*` × timeout de ambiente** — `setup.ts` sobe `asyncUtilTimeout: 5000` e `vitest.config.ts` `testTimeout: 15000` por contenção
  de máquina; teste que só passa isolado = suspeito de ordem/import compartilhado, não aumentar timeout de novo.
- [ ] **Flake conhecido** — `KnowledgePage.test.tsx` flaka no suite completo (15/15 isolado). Ao tocar Knowledge, rode isolado **e** completo antes de culpar o próprio diff.
- [ ] **Rota sem `ProtectedRoute` / nav órfã** — rota lazy nova registrada em `router/index.tsx` sem ProtectedRoute = página pública. `dashboardData.ts`
  com item de nav apontando rota inexistente = menu morto (verificar `access.ts` × roles).
- [ ] **Enum/label do front desalinhado do back** — `taskCatalogs.TERMINAL_TASK_STATES` × estados do workflow do back; novo estado back sem atualizar o catálogo
  = card "fantasma" no kanban (não cai em nenhuma coluna terminal nem ativa).
- [ ] **Número mágico de contrato** — front compara `type === 11` em vez de nome/constante; renumeração de enum (11→12 no merge de virtualização) quebra em silêncio.
- [ ] **Envelope `ApiResponse` ignorado** — erro 4xx tratado como exceção genérica mostra "algo deu errado" sem a mensagem pt-BR do back.

## 9. Segurança e autorização

- [ ] **Endpoint novo sem policy** — todo controller/route novo alinha policies ao módulo irmão; `[Authorize]` vazio herda default. Cruzar com `RoleAccess` do command/query.
- [ ] **IDOR por Guid** — query que aceita `deviceId` sem checar pertence ao escopo do usuário/papel (especie projetos/PDI/tarefas com membros).
- [ ] **Segredo em log/mensagem de erro** — `logger.LogError(exception)` com URL de provider contendo apiKey; exception message de SOAP com credencial.
- [ ] **Replay de convite/token** — `Invitation` expira por clock; `IsExpired` usa `utcNow` do caller (testar boundary, não confiar no relógio do servidor em teste).
- [ ] **Mass assignment** — command com propriedades a mais do que o endpoint deveria aceitar (ex.: `CreatedBy` vindo do cliente em payload de criação — tem que virar da sessão).

## 10. Observabilidade e operação

- [ ] **Health check mentiroso** — `AiRuntimeHealthService` degrada se router cair; novo check que só testa "processo vivo".
- [ ] **Correlação perdida** — `CorrelationIdBehavior` no pipeline; worker/outbox que loga sem o id perde o fio entre API → fila → worker.
- [ ] **Métrica de performance sem baseline** — `PerformanceBehavior` loga; alerta em p95 exige janela — não criar alerta de 1 ocorrência.
- [ ] **Retenção desligada** — eventos/sessões/outbox sem cleanup = disco. Checar se tabela nova tem política (F30 §F histórico/prevenção pendente).

## 11. Testes — como não se enganar

- [ ] **Teste verde no in-memory ≠ Postgres** — `EnsureCreatedAsync` cria schema do modelo **atual**; dev tem histórico real.
  Divergência de migração só aparece no banco real (AI-ENV-003 foi exatamente isso).
- [ ] **Fakes desatualizados em relação à seam** — estender interface (`IOnvifClient`, `IDeviceRepository`) sem atualizar os fakes quebra compilação
  do suíte inteiro; atualizar **todos** (`FakeDeviceRepository` ×2 sessões colidiram no merge).
- [ ] **Teste que só passa com a paralela no ar** — suíte dependente de container/Docker parado = falso negativo em CI. Marcar/saltar com motivo explícito.
- [ ] **Asserção de número de enum em teste de contrato** — teste que grava `DeviceType` e assere o **int** quebra com qualquer renumeração; assere o **nome**.

## 12. Comandos do caçador (atalhos)

```bash
# suíte de arquitetura (policies, slices, alinhamento)
dotnet test tests/InfraControl.ArchitectureTests

# unit completa (gate real)
dotnet test tests/InfraControl.UnitTests

# front
cd frontend/InfraControl.Web && ./node_modules/.bin/vitest run && ./node_modules/.bin/tsc -b

# enums ainda como integer no snapshot (tem que sair vazio de props de enum)
grep -n 'b.Property<int' src/InfraControl.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs

# comandos sem BaseCommand (suspeitos)
grep -rL "BaseCommand" src/InfraControl.Application --include="*Commands.cs" | grep -v Common

# keys de cache vs invalidações (comparar par a par)
grep -rn "CacheKeysToInvalidate" src/InfraControl.Application --include="*.cs"
```

## Pendências conhecidas registradas (não redescobrir)

| Item | Estado |
|---|---|
| `publicRoutes.test.tsx` 3 falhas (`/home`→`/`) | baseline antiga (bd9890a), conhecida |
| KnowledgePage flake no suite completo | 15/15 isolado; investigar ordem |
| Credencial ONVIF por device | pendência F30-T005c (DeviceCredential/F11) |
| Histórico/prevenção ONVIF (§F) | fora do lote |
| Integração vCenter (F31-T005) | aguarda ADR-058 aceita |
| `SnmpCameraProvider` | deliberadamente fora (SnmpInterfaces cobre) |
