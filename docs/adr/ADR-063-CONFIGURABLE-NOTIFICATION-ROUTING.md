# ADR-063 — Roteamento configurável de notificações Zabbix

- **Status**: **aceito pelo usuário em 2026-10-23; implementação em andamento**
- **Data**: 2026-10-23
- **Relacionado**: ADR-023, ADR-027, ADR-059, ADR-062; F39-T001; docs/62

## Contexto

O InfraControl já recebe eventos PROBLEM/OK do Zabbix, persiste a evidência,
gera Alert/Incident e distribui notificações por Outbox, RabbitMQ, consumer e
workers. O envio real ao WhatsApp passa pela Evolution API; Email e SignalR
também estão no fanout.

O roteamento permanece dividido entre configuração global,
`NotificationEventConsumer` e exceções específicas para eventos. O
Administrador não consegue ajustar pela plataforma os canais e os textos por
evento. O usuário quer gerenciar essas decisões sem alterações de código a
cada ajuste. A origem dos eventos e dos dados monitorados é o Zabbix, não o
texto montado pelo InfraControl.

## Proposta de decisão

Criar, depois da aprovação, uma política de notificação configurável pela
plataforma, com:

- defaults por instalação e substituição por tipo do
  `NotificationEventCatalog`;
- filtros por tipo de evento, severidade Zabbix 0–5, host, grupo, trigger e
  tags do evento;
- templates por canal com placeholders permitidos e contexto tipado;
- manutenção de destinatários, preferências, Outbox/Inbox, workers e retry
  atuais;
- versão da política registrada junto às novas deliveries, sem reprocessar
  pendências antigas;
- destino WhatsApp global da integração atual na primeira versão.

O contexto Zabbix deve ser consultado e persistido na ingestão (não no worker
de entrega), usando o ID do evento e a integração Zabbix administrada na
plataforma. O fluxo de ingestão continua aceitando PROBLEM/OK se o lookup
falhar; a falha é observável e as regras condicionais não casam sem os
metadados correspondentes.

Uma regra específica sem filtros aplica-se como regra do tipo; regras
condicionais do tipo que casarem combinam seus canais por união/deduplicação
e substituem o default global. Sem correspondência condicional, aplica-se a
regra sem filtros do tipo ou o default global. Regra sem canais desliga os
canais externos. Enquanto não houver política persistida, a compatibilidade
com o fanout atual é mantida.

## Evidências e restrições

- O contrato de entrada atual contém IDs do evento/recovery, host/IP, nome,
  severidade, status e horário; não contém trigger ID, grupo de host nem tags.
- O objeto Event e as APIs `event.get`, `problem.get`, `trigger.get` e
  `host.get` do Zabbix oficial expõem identificadores, estado, severidade,
  tags e relações de host/grupo úteis para filtros. Esses atributos não estão
  no webhook atual e devem ser consultados na ingestão/persistidos, não
  inferidos no consumer.
- O WhatsApp segue ADR-059/ADR-062: instância gerenciada e destino global. Um
  grupo diferente por regra muda uma decisão já aceita e precisa de aprovação
  e emenda.
- Tipos de evento novos continuam exigindo implementação e publicação no
  catálogo; a configuração dispensa código para alterar o roteamento de tipos
  existentes, não para inventar novos eventos.

## Consequências da decisão

- Menos alterações e deploys para mudar canais/textos de eventos suportados.
- Novo modelo persistido, serviços CQRS, endpoints Administer, tela
  administrativa, testes e migração inicial de compatibilidade.
- O fanout efetivo e templates passam a ser dados versionados e auditáveis.
- Filtros por host/grupo/tag foram aprovados para a primeira versão e dependem
  do contexto enriquecido pela API Zabbix na ingestão. O ambiente ativo é
  Zabbix 8.0.0; o smoke de compatibilidade ao vivo continua pendente antes do
  deploy.
- Testes e smoke cobrem Email, SignalR, WhatsApp e falhas; Evolution permanece
  atrás do dispatcher existente.

## Alternativas consideradas

1. **Continuar com config ambiental e exceções por evento**: rejeitada como
   solução final; mantém o requisito de deploy/código para mudanças.
2. **Regras livres com expressão/script**: rejeitada; complexidade, segurança
   e impossibilidade de garantir comportamento/observabilidade.
3. **Consultar Zabbix a cada notificação**: não recomendada para o MVP; acopla
   entrega à disponibilidade/latência da API Zabbix e exige credenciais em
   tempo de envio.
4. **Vários grupos WhatsApp por evento no MVP**: não recomendada sem decisão
   explícita; conflita com destino global aceito em ADR-059 e amplia riscos de
   envio para destino errado.

## Decisões confirmadas

O usuário aprovou: filtros de host/grupo/tag na primeira versão; grupo
WhatsApp global; precedência substitutiva, regras condicionais correspondentes
com canais unidos por deduplicação, regra vazia desliga canais externos e
política ainda não configurada mantém o legado. O contexto é buscado na
ingestão e persistido em `MonitoringEvent`; a API ativa é Zabbix 8.0.0.
Testes de contrato por fixture passaram; o smoke ao vivo ainda é necessário
antes do deploy.

## Fontes consultadas

- [Zabbix Event object](https://www.zabbix.com/documentation/current/en/manual/api/reference/event/object)
- [Zabbix event.get](https://www.zabbix.com/documentation/current/en/manual/api/reference/event/get)
- [Zabbix problem.get](https://www.zabbix.com/documentation/current/en/manual/api/reference/problem/get)
- [Zabbix trigger.get](https://www.zabbix.com/documentation/current/en/manual/api/reference/trigger/get)
- [Evolution API — Connect Instance, OpenAPI v2.3.7](https://docs.evolutionfoundation.com.br/evolution-api/connect-instance.md)
- [Repositório oficial Evolution API](https://github.com/EvolutionAPI/evolution-api)
