# F39 — Plano de roteamento configurável de notificações do Zabbix

- **Estado**: escopo aprovado pelo usuário em 2026-10-23; implementação em fatias
- **Data**: 2026-10-23
- **Relacionados**: ADR-023, ADR-027, ADR-059, ADR-062; docs/07, docs/27 e docs/61

## Objetivo

Permitir que um Administrador ajuste pela plataforma quais canais recebem
cada tipo de notificação e o texto enviado, sem editar código ou reiniciar
containers para mudanças de operação. O Zabbix continua sendo a fonte dos
eventos e valores monitorados; o InfraControl decide como distribuir as
notificações depois da ingestão.

Não se pretende transformar a plataforma num construtor de código ou regras
arbitrárias. A configuração deve usar tipos de evento publicados pelo
InfraControl, filtros e campos explicitamente suportados e templates com
variáveis permitidas.

## Diagnóstico do fluxo atual

```text
Zabbix PROBLEM/OK
  → POST /api/v1/integrations/zabbix/problems
  → MonitoringEvent → Alert → Incident
  → Domain Event → Outbox → RabbitMQ
  → NotificationEventConsumer
  → Notification + destinatários + NotificationDelivery por canal
  → Workers de Email / SignalR / WhatsApp
  → Evolution API (WhatsApp)
```

- O endpoint de entrada é nosso contrato; o Zabbix monta o JSON a partir de
  macros da ação. O payload atual guarda `eventId`, `recoveryEventId`,
  `host`, `ip`, `name`, `severity`, `status` e `occurredAt`.
- `MonitoringEvent.RawPayload` é reconstruído desse contrato. O webhook atual
  não envia `triggerId`, grupos de host ou tags do evento.
- Os tipos de evento publicados estão em `NotificationEventCatalog`; o
  `NotificationEventConsumer` possui handlers para eles e define títulos,
  mensagens e destinatários. Alterar roteamento hoje depende de
  `Notifications:Channels:Default`, exceções por evento e da ativação
  persistida do WhatsApp.
- Email e SignalR são canais do fanout padrão; eventos de PDI já passam uma
  lista explícita de canais. WhatsApp usa uma instância e um destino global
  selecionado na tela de integração, com preferências de usuário preservadas
  e broadcast no grupo conforme as regras atuais.
- A configuração atual não oferece editor geral de regras nem de templates.
  Um evento novo precisa ser implementado e publicado no catálogo; uma
  configuração não cria novos tipos de evento por si só.

## Pesquisa em fontes oficiais

Consulta realizada em 2026-10-23:

1. A referência oficial do [objeto Event do Zabbix](https://www.zabbix.com/documentation/current/en/manual/api/reference/event/object)
   descreve identificadores `eventid` e `objectid`, origem/tipo do evento,
   estado, horário, severidade Zabbix 0–5 e `r_eventid` para o evento de
   recuperação. Portanto, severity/estado e identidade devem vir do evento,
   sem inferência a partir do texto.
2. A [API `event.get`](https://www.zabbix.com/documentation/current/en/manual/api/reference/event/get)
   admite seleção por IDs de evento, hosts, grupos e objetos; a
   [API `problem.get`](https://www.zabbix.com/documentation/current/en/manual/api/reference/problem/get)
   recupera problemas ativos e, quando solicitado, problemas resolvidos
   recentes. A [API `trigger.get`](https://www.zabbix.com/documentation/current/en/manual/api/reference/trigger/get)
   permite consultar triggers por identificadores e relações com hosts/grupos.
   Isso confirma que filtros de host/grupo são possíveis, mas não significa
   que estejam disponíveis no webhook ou no modelo persistido atual.
3. O guia oficial atual da Evolution API documenta a conexão por instância e
   autenticação por header `apikey` em
   [Connect Instance](https://docs.evolutionfoundation.com.br/evolution-api/connect-instance.md);
   o OpenAPI publicado nessa página informa versão **2.3.7**. O
   [repositório oficial](https://github.com/EvolutionAPI/evolution-api)
   descreve a Evolution como uma API REST multi-provedor.
4. O contrato de envio, grupos e mídia que o InfraControl usa está registrado
   no [ADR-059](adr/ADR-059-WHATSAPP-EVOLUTION-API.md) e no
   [estudo do fluxo](61-ESTUDO-WHATSAPP-FLOW.md), incluindo validação da tag
   `v2.3.7` no ambiente do projeto. Alterações de rota/payload da Evolution
   continuam exigindo validação contra a versão pinada; a documentação pública
   não substitui o smoke test dessa instalação.

### Como obter o contexto de roteamento

O webhook atual não carrega IDs de host/trigger, grupos nem tags. A proposta
aprovada é enriquecer a evidência **na ingestão**, consultando a API Zabbix
configurada na plataforma pelo ID do evento e persistindo o contexto junto do
`MonitoringEvent`. Isso evita uma chamada ao Zabbix por mensagem no worker e
não depende de macros não comprovadas no action atual.

- A consulta deve usar as relações do próprio evento: ID/trigger, hosts,
  grupos e tags de evento.
- O fluxo PROBLEM/OK deve continuar idempotente mesmo se o lookup de contexto
  falhar; a falha fica explícita em log/estado e não deve descartar o evento
  recebido.
- Se houver política condicionada a metadados e o contexto não estiver
  disponível, essa condição não casa. A decisão de fallback usa apenas a
  política global explicitamente configurada; o sistema não inventa
  correspondências por título/IP.
- Para oferecer seletores dinâmicos de host/grupo na plataforma, reutilizar a
  integração Zabbix autenticada já administrada em Administração → Integrações.
  A tela deve indicar claramente quando essa conexão não está configurada ou
  falha.
- O ambiente ativo usa Zabbix 8.0.0; a tentativa de migração para 7.4.15 foi
  revertida em 2026-10-19 (ver docs/27). Manter testes de contrato por fixture
  para o 8.0 e validar consultas ao vivo antes do deploy; a documentação
  `/current` pode descrever versões diferentes.

## Escopo aprovado — versão inicial

### 1. Configuração administrada na plataforma

Adicionar uma área de **Notificações → Regras de envio**, acessível somente
por Administrador, que permita:

- ver o catálogo suportado, a descrição, origem e os canais atuais por tipo;
- definir os canais padrão da instalação;
- definir uma substituição por tipo de evento, com habilitar/desabilitar;
- definir severidade mínima/máxima quando o evento realmente tiver
  severidade;
- editar templates por canal com placeholders de uma lista documentada;
- visualizar prévia usando uma notificação real selecionada ou amostra
  explicitamente marcada como fictícia;
- salvar, testar e consultar quem alterou a configuração e quando.

O editor deve comunicar a diferença entre **canal indisponível** (por
exemplo, WhatsApp sem conexão/destino) e **regra desativada**. Um canal sem
destino não deve ser tratado como envio bem-sucedido.

### 2. Regras e precedência

Recomendação inicial:

1. Uma regra global define os canais padrão da instalação.
2. Uma regra específica de `EventType` substitui os canais padrão para aquele
   tipo; não faz uma união implícita.
3. Filtros de severidade Zabbix usam exclusivamente o nível original 0–5
   persistido em `MonitoringEvent`; não reconstruí-lo a partir de
   `AlertSeverity` ou `IncidentSeverity` depois do mapeamento.
4. A preferência pessoal por canal e a resolução de destinatários existentes
   continuam sendo aplicadas; o novo roteador decide o fanout, não substitui
   RBAC nem destinatários.
5. Regra explícita sem canais significa “não enviar por canal externo” e não
   deve provocar fallback silencioso para Email. A compatibilidade e o fallback
   atual são preservados para instalações ainda sem política persistida; essa
   migração deve ser explícita e testada.
6. A decisão efetiva da política deve ser registrada na delivery/outbox no
   momento do enfileiramento. Alterar uma regra não reescreve mensagens já
   pendentes.

### 3. Escopo de filtros da primeira versão aprovada

- **Incluído**: tipo de evento, severidade Zabbix 0–5 (preservada da
  `MonitoringEvent`), host ID, host group ID, trigger ID e tags (chave/valor)
  obtidos do evento Zabbix.
- O match usa **AND entre dimensões preenchidas** e **OR dentro de uma lista**
  de hosts ou grupos; todos os pares de tag informados na regra devem existir
  no evento. Comparações de IDs são ordinais; chaves/valores de tags usam
  comparação ordinal sem diferenciar maiúsculas/minúsculas.
- Podem existir várias regras condicionais por tipo. Canais de todas as regras
  condicionais que casarem são unidos e deduplicados; se uma ou mais regras
  desse tipo casarem, esse conjunto substitui o default global. Sem regra
  condicional correspondente, aplica-se a regra sem filtros do tipo (se
  existir) ou, na ausência dela, o default global.
- Regras de host/grupo usam IDs estáveis do Zabbix; a UI mostra nomes
  sincronizados para identificação. Tags são dados do evento, não inventário
  preenchido manualmente.
- **Não incluído**: expressões livres, scripts, código, consultas SQL ou
  templates que executem lógica arbitrária.

### 4. Destino WhatsApp — decisão aprovada

Para evitar alterar a decisão existente do ADR-059, a primeira versão mantém
uma única instância e um grupo global já selecionado na integração WhatsApp.
A nova regra escolhe **se** a mensagem vai ao WhatsApp; não escolhe grupo
diferente por evento. Decisão confirmada pelo usuário em 2026-10-23.

### 5. Templates e dados

- Templates não chamam Zabbix nem Evolution e não aceitam execução de
  expressões. O renderizador só substitui placeholders tipados e escapados.
- Primeira versão só expõe campos presentes de forma confiável no contexto de
  notificação existente; a lista final será fechada após mapear cada
  `DomainEvent` para o contexto de notificação correspondente.
- Para eventos de monitoramento, ampliar o contexto a partir dos dados
  persistidos de `MonitoringEvent`/`Alert`/`Incident`, identificando no
  contrato a origem de cada campo. Host/IP desconhecidos permanecem ausentes,
  não são adivinhados.
- O cartão PNG existente continua sendo gerado pelo pipeline atual e só pode
  exibir valores associados de forma inequívoca a dados Zabbix. Template de
  texto e cartão não devem divergir sobre estado, horário ou métrica.

### 6. Segurança, confiabilidade e operação

- CRUD e teste protegidos por `Administer`; auditoria sem segredo, conteúdo
  sensível ou credenciais da Evolution.
- Validar tamanho de template, placeholders permitidos e canal antes de
  persistir. Remover caracteres de controle e respeitar limites do canal.
- Prévia não envia. Envio de teste é uma ação separada, confirmada e usa o
  destino configurado exibido ao Administrador.
- Reutilizar Outbox, Inbox, entregas, retry e DLQ existentes. A regra não
  deve enviar diretamente à Evolution nem contornar workers.
- Erro de renderização/template deve ser observável e não virar uma entrega
  com texto vazio ou uma resposta de sucesso.
- Exibir configuração inválida, último erro, estado do canal e resultado do
  teste sem revelar API key.

## Plano de execução aprovado

1. **Contrato e domínio**: fechar escopo, precedência, campos do contexto e
   limites dos filtros; emendar ADR-059/ADR-062 se o destino ou o fanout
   mudar.
2. **Persistência e serviço de política**: entidade/configuração versionada,
   repositório, resolução determinística e testes para defaults, substituição,
   severidade, canal inativo e compatibilidade.
3. **Integração no fanout**: substituir as exceções codificadas do consumer
   pela política persistida sem interromper Inbox/Outbox, preferências,
   deduplicação, broadcast de grupo e retry.
4. **Templates e contexto**: construir um contexto tipado a partir dos dados
   reais do evento, renderização validada e testes de escaping/fallback.
5. **UI e auditoria**: tela de regras, catálogo, formulário, prévia, teste e
   histórico de alterações.
6. **Migração e ativação**: carregar uma política inicial equivalente ao
   comportamento atual e comparar o fanout antigo/novo em ambiente de teste
   antes de habilitar a nova configuração.
7. **Documentação e smoke**: atualizar docs/07, docs/27, runbook WhatsApp e
   ADRs; testar Email, SignalR, WhatsApp no grupo de teste, indisponibilidade,
   retry/DLQ e recuperação Zabbix PROBLEM→OK.

## Critérios de aceite propostos

- O Administrador altera canais e templates pela plataforma sem editar código
  ou reiniciar API/Workers.
- Todos os tipos publicados no catálogo apresentam regra efetiva e precedência
  compreensível.
- Email, SignalR e WhatsApp continuam no pipeline existente; preferências,
  destinatários, deduplicação, retry e DLQ permanecem cobertos por testes.
- A política inicial reproduz o comportamento atual, salvo decisões de
  produto aprovadas e documentadas.
- Campos Zabbix exibidos e filtros usados podem ser rastreados até payload ou
  dado persistido; nenhum valor/severidade/tag é inferido do texto.
- Template inválido não é salvo nem gera mensagem vazia; configuração inválida
  do canal é claramente mostrada.
- Smoke em ambiente de teste comprova PROBLEM e OK com conteúdo e canais
  configurados, sem envio acidental para grupo de produção.

## Decisões aprovadas pelo usuário

- Incluir host/grupo/tag do Zabbix já na primeira versão, além do tipo de
  evento e severidade.
- Manter um único grupo WhatsApp global, selecionado na integração.
- Regra específica substitui o default; regra vazia desliga canais externos;
  ausência de política persistida preserva o comportamento legado.

O contrato da API Zabbix e a disponibilidade dos campos no ambiente ativo
8.0.0 devem ser validados antes do deploy. O lookup e a persistência do contexto
foram implementados em F39-T002; testes automatizados usam fixtures JSON-RPC.
O smoke da API ativa ainda faz parte de F39-T006. Não alterar/reconstruir
containers antes da suíte direcionada, build e smoke planejado.
