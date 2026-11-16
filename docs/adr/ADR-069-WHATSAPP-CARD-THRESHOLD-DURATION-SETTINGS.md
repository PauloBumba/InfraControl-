# ADR-069 — Cartão visual WhatsApp: threshold, duração do ciclo e settings admin

- **Status:** Aceito
- **Data:** 2026-11-13
- **Contexto:** F33-T005/ADR-059 entregou o cartão PNG (SkiaSharp) com badge
  INCIDENTE/AVISO/RESOLVIDO, device, stats ATUAL/MÍNIMO/MÁXIMO e linha
  histórica do Zabbix. O usuário pediu (spec "Notificações inteligentes")
  o padrão de dashboard de observabilidade: linha de limite real, duração
  do incidente, e configuração administrativa do modelo. ADR-068 já garante
  que o cartão só nasce em transição de estado — este ADR estende a
  apresentação, sem criar pipeline paralelo.

## Decisão

1. **Threshold real vem do Zabbix, nunca inventado.** Novo método
   `IZabbixClient.GetTriggerAsync(triggerId)` (`trigger.get` com
   `expression`/`recovery_expression`). O `triggerId` é recuperado do
   `MonitoringProblemState.CorrelationKey` (`Zabbix:trigger:{id}` — ADR-068)
   do device mais recente. A expressão é parseada por heurística
   (`{item.last()} > N`) e vira `IncidentChartData.LimitValue`; sem
   trigger/expressão/parse → cartão sai sem linha de limite (degrada,
   nunca falha).

2. **Duração do ciclo vem do `MonitoringProblemState`** (OpenedAt →
   ResolvedAt no RESOLVIDO; OpenedAt → agora nos demais). Lookup por
   `DeviceId` mais recente — aproximação correta no fluxo normal porque a
   entrega é quase em tempo real. `IncidentChartData.CycleDuration`
   (TimeSpan?) alimenta um quarto stat no cartão.

3. **Settings do cartão na linha singleton `whatsapp_integration_settings`**
   (mesmo padrão ADR-062 — sem tabela nova): `card_incident_enabled`,
   `card_warning_enabled`, `card_resolution_enabled` (default true =
   comportamento atual), `card_window_hours` (default 2, 1–24) e três
   cores hex opcionais por tipo (null = paleta semântica atual). Tipo
   desligado → `Chart = null` → envio segue em texto (mesmo fallback já
   existente). Expostos em `WhatsAppSettingsDto`/`SaveWhatsAppSettingsRequest`
   e na tela Administração → WhatsApp, com endpoint `POST
   /integrations/whatsapp/test-card` que renderiza um cartão de exemplo e
   envia ao destino configurado.

4. **Escopo deliberado:** "selecionar métricas" e "personalizar título/
   identificação visual" ficam de fora nesta fase — a resolução de item já
   é automática (match por palavras + fallback de disponibilidade) e os
   badges semânticos são o padrão aprovado. Cores configuráveis cobrem a
   personalização visual pedida.

## Consequências

- `IZabbixClient` ganha `GetTriggerAsync`; `IMonitoringProblemStateRepository`
  ganha `GetLatestByDeviceAsync`; `IncidentChartData` ganha `CycleDuration`
  e `AccentColor`; renderer ganha stat de duração e override de cor.
- Uma query extra por delivery WhatsApp (problem state) + uma chamada
  `trigger.get` por cartão — custo irrelevante na volumetria atual.
- Sem triggerId (fallback `name:`) ou trigger sem expressão → sem limite;
  sem problem state → sem duração. Ambos degradam silenciosamente.
