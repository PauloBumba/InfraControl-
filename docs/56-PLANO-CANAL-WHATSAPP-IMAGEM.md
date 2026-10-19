# Plano de implementação — F33-T005: mensagem com gráfico (texto + imagem no WhatsApp)

> **Status**: plano de apoio à task **F33-T005** da Fase 33 do TASKS.md.
> **Fonte da verdade das decisões**: `docs/adr/ADR-059-WHATSAPP-EVOLUTION-API.md`
> (aceita 2026-10-18 — em caso de divergência, a ADR prevalece).
> **Origem do pedido**: plano completo colado pelo usuário (2026-10-18):
> Zabbix → plataforma → SkiaSharp → Evolution API → grupo de WhatsApp,
> sem IA, sem headless, sem serviço pago de imagem.

## 1. O que já existe (não duplicar)

| Peça do pedido | Estado | Onde |
| --- | --- | --- |
| Webhook Zabbix, dedupe, recuperação | **Pronto** | ADR-023, ADR-027, docs/27 |
| Incidente/Alerta + Outbox + RabbitMQ + inbox idempotente | **Pronto** | docs/06, docs/07 |
| Fanout → delivery WhatsApp → worker com retry 5× + DLQ | **Pronto** | F9-T007/F9-T009, docs/07 |
| Infra Evolution (3 containers, profile `evolution`, spike validado) | **Pronto** | ADR-059 §2, F33-T001, compose |
| Config do canal (GroupId/BaseUrl/ApiKey/InstanceId) | **Planejado** | ADR-059 §3, F33-T002 |
| Dispatcher real `sendText` (Polly retry+CB, condicional) | **Planejado** | ADR-059 §4, F33-T002 |
| Destino grupo + dedup por notificação | **Planejado** | ADR-059 §5, F33-T003 |
| Evento AlertCreated (Critical) no grupo | **Planejado** | ADR-059 decisão 2/3, F33-T004 |
| **PNG do gráfico + sendMedia + fallback** | **Esta task (F33-T005)** | este documento |
| Runbook + smoke ponta-a-ponta | **Planejado** | F33-T006 |

Contrato do `sendMedia` já parcialmente sondado (F33-T001): rota viva na
v2.3.7 (`POST /message/sendMedia/{instance}`, probe `{}` → 400 "requires
number/mediatype"). Corpo exato (mediatype/mimetype/media base64/caption/
fileName, limite ~5MB, destino `@g.us`) = **spike obrigatório** (checklist
na ADR-059 §6/T005).

## 2. Decisões que regem esta task (herdadas da ADR-059)

- **Canal**: o envio sai **exclusivamente** pelo pipeline existente
  (Outbox → RabbitMQ → NotificationEventConsumer → NotificationWhatsAppWorker
  → dispatcher). Proibido atalho que chame a Evolution fora do fluxo.
- **Formato**: caption espelhando o card da plataforma (status/data+hora,
  dispositivo, título, métricas — referência `image.png` do desktop) +
  PNG do gráfico anexado via `sendMedia`.
- **Fallback obrigatório**: geração do PNG falhou ou sem histórico → envio
  automático degrada para `sendText` — mensagem nunca fica por enviar por
  causa do gráfico.
- **Mídia restrita**: só esta imagem de evento (proibido fotos/vídeos/PDFs
  arbitrários — ADR-059 §1).
- **Sem dependência alienígena**: lib .NET headless de renderização
  (escolha técnica abaixo: SkiaSharp), espírito ADR-048 §4.
- **Destino**: grupo único (`WHATSAPP_GROUP_ID`) — ver §7 pendência aberta.

## 3. Fonte da série histórica (spike obrigatório)

O payload do webhook do Zabbix (docs/27) **não traz** o histórico da
métrica. Opções a validar no spike, nesta ordem de preferência:

1. **API history do Zabbix** (preferida): já existe o cliente
   `IZabbixClient.GetHistoryAsync(itemId, from, to, limit)` (F27) —
   necessita resolver trigger→item. Persistir `triggerId`/`itemId` no
   evento quando disponíveis no payload; lookup por API quando não.
2. **Métricas do device no banco** (se o histórico relevante já for
   persistido pela plataforma — cobertura parcial, validar).
3. **Sem fonte**: mensagem segue com caption completa e **sem gráfico**
   (regra 6 do pedido original — identificar "dados indisponíveis").

Janela do gráfico: configurável (default ex. últimas 2h), alinhada ao
horário do evento; linha tracejada do limite do trigger quando conhecido.

## 4. Renderização (SkiaSharp)

- Pacote `SkiaSharp` + `SkiaSharp.NativeAssets.Linux` no projeto de
  implementação (nativo é obrigatório em Linux/Docker — sem ele o
  `SKBitmap` derruba em runtime).
- Porta em Application (`IIncidentImageRenderer`: entrada `IncidentImageData`,
  saída `byte[]`/`Stream` PNG); implementação em Infrastructure — troca de
  lib não toca Application (padrão do repo).
- Cartões: **incidente** (vermelho), **aviso** (laranja), **resolução**
  (verde); fundo escuro, identidade uniforme; host, problema, severidade,
  valor atual, mínimo/máximo, linha do limite, série com escala/tempo,
  data/hora do evento; PNG em memória.
- Sem dados suficientes → cartão sem gráfico com aviso de dados
  indisponíveis.
- Testes unit: magic bytes PNG, dimensões, os 3 kinds, série vazia, tema
  estável (TimeProvider fixo) — precedente dos renderers QuestPDF/ClosedXML
  do PDI (Fase 8).

## 5. Dispatcher (adição à F33-T002)

- `EvolutionApiWhatsAppNotificationDispatcher` ganha caminho de mídia:
  `POST /message/sendMedia/{InstanceId}` com header `apikey`
  (`mediatype=image`, `mimetype=image/png`, `media=<base64>`, `caption`,
  `fileName` — contrato exato no spike).
- Geração/render + leitura da série acontecem **antes** do POST (fora do
  retry HTTP): falha de geração → `sendText` direto; falha de rede → Polly
  como texto (retry 3× 1s/2s/4s jitter + CB 5/30s→60s; 4xx sem retry).
- Idempotência: mesma `NotificationDelivery` nunca reenvia após `Sent`
  (barreira do worker — at-least-once do Outbox não vira duplicata no
  grupo).

## 6. Testes e validação

- Unit: renderer (cartões/séries/fallback) + dispatcher com mídia
  (stub HTTP — precedente `HttpWebhookNotificationDispatcherTests` e o
  smoke F-T011c com HttpListener).
- Worker: delivery de incidente → tenta mídia → fallback texto.
- Integração/smoke (F33-T006): trigger real no Zabbix → pipeline completo →
  2 mensagens no grupo (Alerta Critical + Incidente aberto) **com gráfico**;
  OK → resolução verde; evidências de log + print.

## 7. Pendência aberta (decisão do usuário)

Minha proposta inicial (docs/56 v1) sugeria **vários destinos**
(lista phone/group configurável). A ADR-059 **aceita** define destino
**único** (grupo; Phone individual só como fallback retrocompatível) e
lista "destino por usuário" como proibido. **Enquanto não houver emenda
formal à ADR-059, este plano segue o destino único.** Se quiser vários
destinos (vários grupos), é emenda na ADR-059 §3/§5 + ajuste no fanout.

## 8. Checklist da task

- [ ] Spike: contrato `sendMedia` (corpo, limite, `@g.us`) na v2.3.7
- [ ] Spike: fonte da série (history Zabbix × banco × sem fonte)
- [ ] Porta + `SkiaIncidentImageRenderer` + pacotes nativos
- [ ] Cartões incidente/aviso/resolução com testes unit
- [ ] Dispatcher: sendMedia + fallback sendText + testes
- [ ] Worker: mídia para deliveries de incidente/resolução
- [ ] docs/07: formato da mensagem (caption + gráfico)
- [ ] Smoke ponta-a-ponta (F33-T006) com evidências

## 9. Referências

- `docs/adr/ADR-059-WHATSAPP-EVOLUTION-API.md` (decisões 1–7, §1–§6)
- `TASKS.md` Fase 33 (Canal WhatsApp via Evolution) — F33-T000..T006
- `docs/07-NOTIFICACOES.md` (canal WhatsApp, fanout, formatos)
- `docs/06-MESSAGING-OUTBOX-RABBITMQ.md`, `docs/27-INTEGRACAO-ZABBIX.md`
- ADR-023 / ADR-027 (ingest), ADR-012 (notificações)
