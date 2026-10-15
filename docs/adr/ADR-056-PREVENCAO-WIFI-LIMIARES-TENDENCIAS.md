# ADR-056: Prevenção WiFi — limiares, tendências e histórico via Zabbix

- **Status**: **aceito** (aprovado pelo decisor humano em 2026-10-15 —
  docs/51 §S6 e docs/52 §S4 exigiam ADR aprovado antes de qualquer código)
- **Data**: 2026-10-15
- **Contexto**: F30-T004 S4 (ficha 45 §G — "Histórico e Prevenção").

## Contexto

As 8 fichas da F30 declaram o mesmo gap: **prevenção não existe**. A ficha
45 §G lista o que se quer visualizar (tendência de utilização, degradação
de sinal, aumento de clientes, interferência crescente) e propõe um modelo
de manutenção preventiva com limiares — mas nenhuma ficha implementa.

Hoje:

- O `AlertThresholds` (`AlertEvaluator.cs`) é **reativo e hardcoded** —
  avalia CADA poll isolado (Ping streak, tóners…), sem noção de tendência;
- O histórico numérico vive no **Zabbix** (template por host, retenção
  30/90 dias @ 5 min — ficha §"Métricas que Precisam de Histórico"), e a
  leitura já está pronta desde a F29:
  `IZabbixClient.GetHistoryAsync` + `GetZabbixHistoryQuery` (autorizado) +
  mapeamento device↔host via `IZabbixHostsRepository`;
- Insumos ao vivo também existem: telemetria UniFi (`stat/device` —
  utilização de rádio por AP, já exibida na WiFiPage) e os agregados do S1
  (RSSI médio, clientes por SSID);
- O pipeline **evento → alerta → incidente** existe (ADR-027), assim como
  a regra de não persistir telemetria bruta (REGRA 8, ADR-028 §10).

## Decisão

### 1. Prevenção = análise de TENDÊNCIA sobre histórico — não um check a mais

A prevenção avalia **janelas de tempo** (a ficha: "utilização > 70% e
tendência ascendente", "RSSI médio caindo"), o que é impossível no molde
poll → resultado. Nasce um avaliador dedicado:

- contrato `EvaluateWifiPreventionCommand(DeviceId)` (CQRS, ADR-006) que
  abre/fecha alertas pelo pipeline existente;
- execução agendada (ciclo de polling da plataforma) **e** gatilho manual
  — o desenho exato do driver é decisão de fatia, não deste ADR.

### 2. Fonte de histórico: Zabbix. InfraControl guarda só o agregado

- O avaliador consulta `GetHistoryAsync` para a janela (60 min, resolução
  5 min, **N ≥ 12 pontos** exigidos);
- Métrica ausente no Zabbix (host sem item mapeado) ⇒ **aquela regra não
  roda** para o device — ausência honesta, nunca `0` (REGRA da casa);
- Nenhuma tabela nova de telemetria: persiste apenas o **Alert/Evento**
  gerado (ADR-027) — REGRA 8 / ADR-028 §10.

### 3. Limiares iniciais (defaults em código; ativação opt-in por device)

| Regra | Sinal | Severidade | Confirmação |
|---|---|---|---|
| Saturação de rádio | utilização > 80% | Warning | sustentada ≥ 15 min (3 amostras) |
| Saturação crítica | utilização > 90% | Critical | sustentada ≥ 10 min |
| Rota para saturação | utilização > 70% com inclinação ascendente | Warning | janela 60 min |
| Sinal degradando | RSSI médio < −70 dBm **ou** inclinação descendente sustentada | Warning | janela 60 min |
| Densidade de clientes | clientes por AP > 50 | Warning | sustentado ≥ 15 min |

- Defaults em código, no espírito do `AlertThresholds`/ADR-014 — mudar
  default exige release;
- **Opt-in por device**: nenhum device gera alerta preventivo sem
  habilitação explícita (o mecanismo exato — flag × profile — é decisão
  de fatia; `DeviceProfileDefaults` permanece intocado até lá).

### 4. Tendência: método determinístico, sem ML

- inclinação por mínimos quadrados sobre a janela de 60 min;
- regra só dispara com **2 avaliações consecutivas** sinalizando (anti-
  flapping);
- recuperação fecha o alerta pelo mesmo ciclo de vida do ADR-027;
- camada de IA (ADR-052…055) fica fora — determinismo primeiro.

### 5. Fora do primeiro release (pendências explícitas)

- **Interferência/noise floor**: o controller UniFi expõe, mas o DTO
  atual não lê (`radio.noisefloor`/`interference`) — candidato a parser
  (mesma família da pendência S1b de banda por cliente);
- **PoE falhando** (ficha alerta Critical): vendor-specific, sem
  OID/endpoint mapeado;
- **Tela de tendência** (gráficos de histórico): fatia própria, após o
  avaliador existir.

## Consequências

- **Positivo**: primeira capacidade preventiva do produto, usando
  histórico que o Zabbix **já coleta** (custo de coleta zero) e o
  pipeline de alertas que já existe;
- **Negativo**: depende do template Zabbix ter os itens (utilização,
  RSSI, clientes) mapeados por host — sem item, sem prevenção daquela
  regra (honesto, mas exige setup externo);
- **Risco aceito**: limiares genéricos podem gerar falso positivo em
  ambientes densos — mitigado por opt-in + dupla confirmação; ajuste
  fino vem com uso.

## Alternativas consideradas

1. **Persistir telemetria própria no PostgreSQL** → viola REGRA 8 /
   ADR-028 §10 e duplica o histórico do Zabbix;
2. **Só UniFi ao vivo (sem Zabbix)** → sem histórico não existe tendência;
   permanece reativo (o que já existe);
3. **Predictive com ML** → camada ADR-052…055, complexidade desproporcional
   para os primeiros limiares;
4. **Não fazer** → os gaps das 8 fichas permanecem e prevenção continua
   sendo promessa de documento.

## Referências

- Ficha 45 §G (modelo preventivo) + §"Métricas que Precisam de Histórico"
  (retenção 30/90 dias @ 5 min)
- docs/51 §S6 e docs/52 §S4 — ambos exigem este ADR antes do código
- ADR-028 §10 (REGRA 8 — histórico detalhado é do Zabbix; o InfraControl
  guarda agregados/eventos)
- ADR-027 (MonitoringEvent → Alert → Incident)
- ADR-006 (CQRS), ADR-014 (defaults em código)
- `IZabbixClient.GetHistoryAsync` (F29) · agregados S1 (WiFiPage)
