# ADR-062: Canal WhatsApp gerenciado pela plataforma — config persistida em banco + conexão (QR), grupos e teste na UI

- **Status**: **aceito (desenho) — usuário direcionou "ajusta e começa a executar" em 2026-10-19; libera fatia 1 (backend)**
- **Data**: 2026-10-19
- **Relacionado**: ADR-059 (transporte via Evolution API, texto+gráfico), ADR-023/ADR-027 (eventos Zabbix), precedente de settings persistidas: `UniFiIntegrationSetting` (F26, `docs/36`) e `VCenterIntegrationSetting` (ADR-058 §3 — "espelho byte a byte do UniFi").

## Contexto

O canal WhatsApp já funciona de ponta a ponta (F33-T002..T005, ADR-059):
dispatcher real (sendText + sendMedia com gráfico Skia), worker com destino
grupo, consumer com dedup de broadcast e `Notification.DeviceId` para a série
do Zabbix. **Mas toda a configuração vive em `.env`/appsettings** (env), e a
conexão do WhatsApp (instância + QR) é feita **fora da plataforma**, no
manager UI da Evolution API.

O usuário quer que **tudo passe a viver dentro da plataforma**:

> "o grupo definido após criação fica no banco; tem que ter um processo
> gerenciando e comunicando com a Evolution API" — e a conexão (QR) também
> dentro da UI, sem depender do manager externo.

### Estado atual (o que existe hoje)

**Config**: `WhatsAppChannelOptions` (`Notifications:Channels:WhatsApp`: Phone,
GroupId, BaseUrl, ApiKey, InstanceId) é lida de **env** via
`IOptions<NotificationChannelsOptions>` em **4 pontos**:

| Ponto | Arquivo | O que lê |
|---|---|---|
| Worker | `src/InfraControl.Api/Workers/NotificationWhatsAppWorker.cs:30` | `GroupId ?? Phone` (destino) |
| Dispatcher | `src/InfraControl.Infrastructure/Services/EvolutionApiWhatsAppNotificationDispatcher.cs:73` | `BaseUrl`, `ApiKey`, `InstanceId`, `GroupId` |
| Factory DI | `src/InfraControl.Infrastructure/Services/DependencyInjection.cs:247` | `BaseUrl` (escolhe Logging vs Evolution) |
| Consumer | `src/InfraControl.Workers/Consumers/Notifications/NotificationEventConsumer.cs:391,449` | `IsSendable` (GroupId ou Phone) + `groupBroadcast` (dedup) |

**Conexão/QR**: hoje é manual, fora da plataforma:

```text
usuário abre manager UI da Evolution (http://localhost:18081/manager)
  → informa apikey
  → cria/conecta instância "Admin"
  → QR exibido no manager → escaneia no celular
  → connectionState vira "open"
```

A plataforma **não tem** nenhuma tela de WhatsApp, nenhum client de gestão
(só o dispatcher de envio), nenhum endpoint de settings.

### Endpoints da Evolution API já validados (spike F33-T001/T005)

| Endpoint | Método | Uso | Status spike |
|---|---|---|---|
| `/message/sendText/{instance}` | POST | envio texto (`{number,text}` + header `apikey`) | ✅ 201 |
| `/message/sendMedia/{instance}` | POST | envio mídia (`{number,mediatype,mimetype,media,caption,fileName}`) | ✅ 201 |
| `/group/fetchAllGroups/{instance}?getParticipants=false` | GET | lista grupos (`id`=`@g.us`, `subject`, `size`, `owner`, `desc`, `restrict`, `announce`) | ✅ 117 grupos |
| `/instance/connectionState/{instance}` | GET | estado (`open`/`connecting`/`close`) | ✅ poll ok |
| `/instance/connect/{instance}` | GET | gera QR (devolve `base64` = data-URI PNG) + pairing code | ✅ decodificado |
| `/instance/create` | POST | cria instância (`{instanceName,integration:"WHATSAPP-BAILEYS",qrcode:true}`) | ✅ (rota sem path param) |
| `/instance/logout/{instance}` | DELETE | desconecta (workaround #1408 p/ regenerar QR) | ✅ |

> Nota: a rota de criação real é `POST /instance/create` (body), **não**
> `/instance/create/{instance}` (404). Workaround #1408: `logout` → `connect`
> para regenerar o QR quando a instância fica `close`/`connecting`.

## Decisão

Criar **gestão completa do canal WhatsApp dentro da plataforma**, com config
persistida em banco (padrão singleton `*IntegrationSetting`, precedente UniFi/
Zabbix/VCenter) e uma tela de Integração → WhatsApp que conversa com a
Evolution API. A fonte de verdade do destino (GroupId) passa a ser o **banco**,
com **fallback para env** enquanto não houver linha (transição sem quebrar).

### Novas peças — visão geral

```text
┌───────────────────────────── FRONTEND (React) ─────────────────────────────┐
│  WhatsAppSettingsPage  /integrations/whatsapp                              │
│  ├─ Conexão: status (poll), botão "Gerar QR" (exibe PNG), "Desconectar"    │
│  ├─ Config: BaseUrl / ApiKey(write-only) / InstanceId / Phone              │
│  ├─ Grupos: botão "Carregar grupos" → lista (subject + @g.us + size)       │
│  │          → selecionar grupo → salva GroupId no banco                    │
│  └─ Teste: botão "Enviar teste" no grupo escolhido (201/erro na tela)      │
└─────────────────────────────────────┬───────────────────────────────────────┘
                                      │ /api/v1/integrations/whatsapp (Administer)
┌─────────────────────────────────────▼───────────────────────────────────────┐
│  WhatsAppIntegrationController                                              │
│   GET  config            → GetWhatsAppSettingsQuery                         │
│   PUT  config            → SaveWhatsAppSettingsCommand                      │
│   GET  connection-state  → GetWhatsAppConnectionStateQuery                  │
│   POST connect (QR)      → GenerateWhatsAppQrCommand                        │
│   POST disconnect        → DisconnectWhatsAppInstanceCommand                │
│   GET  groups            → ListWhatsAppGroupsQuery                          │
│   POST test              → SendWhatsAppTestCommand                          │
└─────────────────────────────────────┬───────────────────────────────────────┘
                                      │ CQRS (BaseQuery/BaseCommand + IAuthorizedRequest)
┌─────────────────────────────────────▼───────────────────────────────────────┐
│  Application/Integrations/WhatsApp/                                         │
│   WhatsAppSettingsCommands.cs (Get/Save/Disconnect/Connect)                 │
│   WhatsAppGroupsQueries.cs (ListGroups/Test/ConnectionState)                │
│   Ports/: IWhatsAppSettingsRepository, IWhatsAppOptionsResolver,            │
│           IEvolutionInstanceClient                                          │
└─────────────────────────────────────┬───────────────────────────────────────┘
                                      │
┌─────────────────────────────────────▼───────────────────────────────────────┐
│  Infrastructure                                                             │
│   Integrations/WhatsApp/WhatsAppOptionsResolver.cs  (banco → fallback env)  │
│   Integrations/WhatsApp/EvolutionInstanceClient.cs  (HttpClient "evolution- │
│     api": create/connect/logout/connectionState/fetchAllGroups/sendText)    │
│   Repositories/WhatsAppSettingsRepository.cs        (auto-registro F0-T024) │
│   Domain/Entities/WhatsAppIntegrationSetting.cs     (singleton 1 linha)     │
│   Persistence/Configurations/WhatsAppIntegrationSettingConfiguration.cs      │
│   Persistence/Migrations/AddWhatsAppIntegrationSetting (+ DbSet)            │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Entidade `WhatsAppIntegrationSetting` (singleton — 1 linha)

| Coluna (snake_case) | Tipo | Obs |
|---|---|---|
| `id` | uuid | PK |
| `base_url` | varchar(512) | URL da Evolution (ex.: `http://evolution-api:8080`) |
| `api_key_encrypted` | varchar(2048) | **cifrada** AES-GCM (`ICredentialProtector`), write-only |
| `instance_id` | varchar(128) | nome da instância (ex.: `Admin`) |
| `group_id` | varchar(128) null | `@g.us` escolhido na UI — **destino** |
| `phone` | varchar(64) null | fallback individual (retrocompatível) |
| `connection_state` | varchar(32) null | último estado visto (`open`/`connecting`/`close`) |
| `last_tested_at_utc` | timestamptz null | |
| `last_test_ok` | bool | |
| `last_test_error` | varchar(1000) null | |
| `create_date_time` / `update_date_time` | timestamptz | da base `Entity` |

Validações de domínio: `BaseUrl` http/https absoluta; `ApiKeyEncrypted` nulo =
mantém a gravada (roteamento opcional, igual Zabbix); `GroupId` se preenchido
termina em `@g.us`.

### Accessor/Resolver (padrão `ZabbixOptionsResolver`)

`IWhatsAppOptionsResolver.ResolveAsync(ct)`:
- há linha no banco → devolve options materializadas (`BaseUrl`, `ApiKey`
  decifrada via `UnprotectQuietly`, `InstanceId`, `GroupId`, `Phone`)
- senão → `IOptions<NotificationChannelsOptions>.Value.WhatsApp` (env)

Registrado **Scoped**; o `EvolutionInstanceClient` e o dispatcher passam a
obter as options por esse resolver (por request), permitindo **trocar o grupo
sem restart**.

### Rewire dos 4 pontos de leitura (banco → env)

Hoje os 4 pontos leem `IOptions` estático. Passarão a ler via resolver
(Scoped), mantendo o fallback env:

1. **Worker** (`NotificationWhatsAppWorker`) — destino `GroupId ?? Phone` do resolver
2. **Dispatcher** (`EvolutionApiWhatsAppNotificationDispatcher`) — `BaseUrl/ApiKey/InstanceId/GroupId` do resolver (elegível a `IOptions` no ctor → resolver)
3. **Factory DI** (`DependencyInjection.cs:247`) — escolha Logging vs Evolution via resolver
4. **Consumer** (`NotificationEventConsumer`) — `IsSendable` + `groupBroadcast` via resolver

> Como o resolver é Scoped e o consumer/worker rodam por mensagem, a resolução
> é por delivery — troca de grupo no banco reflete na próxima mensagem.

### Fluxo de conexão (QR) dentro da plataforma

```text
UI "Gerar QR"
  → POST /api/v1/integrations/whatsapp/connect        [Administer]
  → GenerateWhatsAppQrCommand
  → EvolutionInstanceClient: logout (se close) → GET /instance/connect/{instance}
  → devolve base64 (data-URI PNG) + pairingCode
  → UI decodifica e exibe a imagem do QR
usuário escaneia no celular (WhatsApp → aparelhos conectados)
UI faz poll: GET /connection-state
  → GET /instance/connectionState/{instance} → "open"
  → grava connection_state = "open" no banco (e mostra "Conectado")
```

### Fluxo de seleção de grupo

```text
UI "Carregar grupos"
  → GET /api/v1/integrations/whatsapp/groups           [Administer]
  → ListWhatsAppGroupsQuery
  → EvolutionInstanceClient: GET /group/fetchAllGroups/{instance}?getParticipants=false
  → lista de DTOs { id(@g.us), subject, size, owner, desc, restrict, announce }
UI mostra a lista; usuário clica num grupo
  → PUT /config { ..., groupId: "1203...@g.us" }       [Administer]
  → SaveWhatsAppSettingsCommand → banco (upsert singleton)
```

### Fluxo de teste

```text
UI "Enviar teste"
  → POST /api/v1/integrations/whatsapp/test            [Administer]
  → SendWhatsAppTestCommand
  → resolver lê settings (banco/env) → alvo = GroupId ?? Phone
  → EvolutionInstanceClient: POST /message/sendText/{instance} {number, text}
  → 201 → grava last_test_ok=true; erro → last_test_error (pt-BR, sem ApiKey)
  → devolve resultado na UI
```

### Fluxo de envio de eventos (INALTERADO — só muda a fonte do destino)

O pipeline de notificação **não muda** (decisão 5 do ADR-059: proibido atalho).
Muda apenas que `GroupId`/`Phone` agora vêm do resolver (banco → env):

```text
Zabbix PROBLEM → POST /api/v1/integrations/zabbix/problems
  → IncidentCreatedDomainEvent / AlertCreatedDomainEvent
  → Outbox → RabbitMQ → NotificationEventConsumer (fanout/dedup groupBroadcast)
  → NotificationDelivery (WhatsApp) → NotificationWhatsAppWorker
  → IWhatsAppNotificationDispatcher (EvolutionApi)
      destino = resolver.GroupId ?? resolver.Phone     ← AGORA do banco
      gráfico (Chart) → sendMedia, senão sendText
```

## Decisões do usuário (2026-10-19)

1. **Grupo persiste no banco** após criação/seleção — confirmado.
2. **Conexão (QR) dentro da plataforma** — confirmado ("tudo dentro da
   plataforma; como vai ser o QR?").
3. **Fatias**: **fatia 1 = backend completo** (persistência + client + CQRS +
   controller + **conexão/QR** + rewire); **fatia 2 = frontend** (tela).
   QR/conexão entra já na fatia 1 (backend expõe os endpoints; a UI os usa
   na fatia 2).

## Alternativas consideradas

1. **Manter só `.env`** — rejeitado: exigiria reiniciar a API pra trocar grupo;
   não atende "gerenciar pela plataforma".
2. **Bot/webhook separado pra capturar group_id** — rejeitado pelo usuário:
   "não seria via webhook; a ideia é a plataforma conversar com a Evolution".
3. **Config por arquivo/editável sem banco** — rejeitado: sem auditoria, sem
   UI nativa, frágil em container.

## Riscos e mitigações

| Risco | Mitigação |
|---|---|
| Baileys instável (disconnect aleatório) | UI de status + botão reconectar/QR; runbook F33-T006 |
| ApiKey vazando em log/erro | write-only no DTO; `UnprotectQuietly`; nunca logar header `apikey` |
| Troca de grupo em produção afeta envio | resolver por request; fallback env se linha apagada |
| `fetchAllGroups` lento com muitos grupos | `getParticipants=false`; paginação futura se preciso |

## Itens a validar antes do código (spike desta ADR)

- [x] `POST /instance/create` (body) e `GET /instance/connect` retornam QR
      utilizável **a partir da plataforma** (já validado no spike manual).
- [ ] Resolver banco→env não quebra o envio existente (regressão F33-T002..T005).
- [x] `fetchAllGroups` com `getParticipants=false` responde em tempo aceitável.
- [x] Decisão: QR/conexão na **fatia 1 (backend)**.

## Referências

- ADR-059 — transporte Evolution API, texto + gráfico, decisões 1–7
- `docs/36-F26-INTEGRACAO-UNIFI.md` §2.2 — precedente `*IntegrationSetting`
- ADR-058 §3 — VCenter espelha UniFi (padrão formalizado)
- `src/InfraControl.Infrastructure/Integrations/Zabbix/ZabbixOptionsResolver.cs` — fallback banco→env
- `src/InfraControl.Api/Controllers/Integrations/ZabbixIntegrationController.cs` — controller settings (policy Administer)

## 10. F38-T005 — fechar o fluxo sem valores de negócio pré-cadastrados (2026-10-23)

### Diagnóstico do fluxo existente

O fluxo já permite salvar a configuração, criar uma instância, gerar QR,
consultar estado, carregar os grupos reais da Evolution, selecionar um grupo e
enviar um teste. A inspeção ponta a ponta encontrou lacunas que impedem
considerá-lo gerenciável e confiável:

1. A criação trata HTTP 400 como sucesso e retorna apenas o estado; a UI
   apresenta a mesma confirmação genérica sem identificar a instância nem
   distinguir criação de instância já existente.
2. A UI pode pedir criação/QR antes de salvar os valores que essas operações
   resolvem no banco/ambiente, causando erro ou usando uma configuração
   diferente da que está no formulário.
3. O exemplo de ambiente define `WHATSAPP_INSTANCE=Admin`, preenchendo um nome
   de negócio no lugar de o administrador escolher a instância.
4. A tela deixa a seleção de grupo em estado local até o administrador salvar
   a configuração; o fluxo precisa deixar explícito quando o destino ainda
   não está persistido.

### Plano de ajuste e critérios de aceite

1. **Configurar sem dados de negócio embutidos**: exigir URL, chave e nome
   escolhidos pelo administrador; remover o nome `Admin` do `.env.example`.
   O `.env` legado continua aceito como fallback para não interromper o envio
   atual durante a transição. A chave permanece write-only e cifrada.
2. **Salvar antes de operar**: operações que dependem da configuração devem
   usar exatamente a configuração persistida. A UI deve salvar alterações
   válidas antes de criar/conectar/carregar grupos, ou impedir essas ações com
   orientação clara enquanto houver mudanças não salvas.
3. **Criar e identificar**: a resposta deve identificar o nome da instância e
   se foi criada ou já existia. HTTP 400 e demais falhas não esperadas devem
   aparecer como erro, nunca como confirmação de sucesso. Conflito conhecido
   pode ser tratado como "já existe", seguido de leitura do estado real.
4. **Conectar e selecionar dinamicamente**: exibir o QR e estado da instância;
   buscar grupos exclusivamente com `fetchAllGroups` da instância conectada,
   sem catálogo local/pré-cadastrado. A seleção deve indicar destino salvo ou
   alteração pendente e persistir `GroupId` no banco ao salvar.
5. **Validar o resultado**: o teste manual e as notificações permanecem no
   destino resolvido pela configuração salva, com falhas visíveis e sem expor
   ApiKey. O envio de eventos permanece exclusivamente em
   Outbox → RabbitMQ → consumer → worker → dispatcher, conforme ADR-059.
6. **Ativar o canal pela plataforma**: incluir a ativação de notificações
   WhatsApp na configuração persistida, para não exigir alterar
   `Notifications:Channels:Default`/`.env` depois de configurado o destino.
   O campo no banco será nullable para instalações existentes: `null` mantém
   o comportamento legado do ambiente; um valor escolhido na UI passa a ser
   a fonte de verdade. Instalações novas mantêm o canal desativado até o
   administrador ativá-lo.

### Escopo e limites

- Esta entrega mantém a configuração singleton existente: um nome de
  instância/número ativo por instalação do InfraControl. O nome não é fixo e
  o administrador escolhe qual instância configurar.
- Não inclui cadastro/seleção simultânea de múltiplas instâncias, nem
  instalação, atualização ou controle de ciclo de vida dos containers Docker
  Evolution pela UI. A infraestrutura continua implantada pelo profile
  `evolution` do Compose; a plataforma gerencia a instância e o canal via API.
- Não remove o fallback de variáveis de ambiente nesta correção; retirar o
  fallback exige migração operacional separada para instalações que já o usam.
- A ativação persistida substitui a configuração de fanout somente para
  WhatsApp quando há uma escolha explícita na UI; os demais canais mantêm
  `Notifications:Channels:Default`.

### Sequência de uso esperada

```text
Admin abre Administração → WhatsApp
  → informa URL, ApiKey e nome escolhido → salva (config persistida)
  → cria instância → UI confirma nome e resultado real (criada/existente)
  → gera QR → escaneia → status conectado
  → carrega grupos da Evolution → escolhe um grupo real
  → salva a configuração → UI confirma o destino persistido
  → envia teste → resultado explícito
  → eventos continuam pelo pipeline Outbox/RabbitMQ até o grupo salvo
```

**Tarefa rastreável**: `F38-T005` em `TASKS.md`. Implementar e testar os
critérios acima, atualizar este fluxo com o comportamento efetivamente
validado e registrar o resultado em `PROJECT_STATE.md`.

## 11. Ajuste — substituição explícita do número ativo (2026-10-23)

### Objetivo

Deixar inequívoco na UI como trocar o número atualmente configurado por outro,
sem sugerir suporte a vários números simultâneos e sem reutilizar
acidentalmente o destino anterior.

### Plano e critérios

1. Detectar alteração do nome da instância em relação à configuração persistida
   e pedir confirmação antes de gravar a substituição.
2. Explicar na confirmação que a plataforma passará a operar a nova instância,
   que não apagará a instância antiga da Evolution e que o número novo precisa
   ser pareado novamente por QR.
3. Ao editar o nome da instância, limpar grupo e telefone locais, descartar a
   lista de grupos/QR/resultados antigos e desligar notificações automáticas.
   A substituição só deve ser salva com destino vazio e fanout desativado;
   ativação e seleção do novo destino são etapas explícitas posteriores.
4. Depois da confirmação: salvar → criar ou identificar a instância → gerar QR
   → parear → carregar grupos do número conectado → selecionar grupo → salvar
   destino → testar envio; ativação de notificações automáticas é opcional e
   requer escolha explícita depois da troca.
5. Cobrir com testes a confirmação/cancelamento e garantir que o payload da
   substituição não contém o grupo, telefone ou ativação do destino anterior.

### Implementação

- A tela detecta a troca da instância persistida e abre confirmação antes de
  salvar. Cancelar mantém o formulário editado sem chamar a API.
- Enquanto o nome novo não estiver salvo, as operações de conexão e a ativação
  automática ficam bloqueadas; grupo e telefone anteriores são limpos da
  edição.
- Confirmar salva a nova configuração com `groupId = null`, `phone = null` e
  `notificationsEnabled = false`. O restante do fluxo de conectar, selecionar
  grupo e testar continua sendo o caminho já existente.
- Testes focados cobrem cancelar e confirmar sem carregar destino/automação
  anteriores para a nova instância.

### Limites

O modelo permanece singleton. Não remover/desconectar automaticamente a
instância anterior na Evolution, pois ela é estado externo e a substituição da
configuração da plataforma não deve apagar nem afetar uma sessão não
confirmada pelo operador. Não incluir gerenciamento de múltiplos números.
