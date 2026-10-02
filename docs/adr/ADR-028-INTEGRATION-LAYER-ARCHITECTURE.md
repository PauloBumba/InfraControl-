# ADR-028 — Arquitetura da Camada de Integrações (Integration Layer)

- **Status**: aceito
- **Data**: 2026-10-01
- **Contexto**: F19-T000 (Fase 19 — Integrações Avançadas e Eventos Externos). Define a arquitetura unificada para os provedores externos **Zabbix** (Monitoramento/Triggers), **UniFi** (SDN/Infraestrutura) e **n8n** (Automação/Notificações Externa).

---

## Problema

O InfraControl está evoluindo para um **Operations Center**. Com isso, diferentes sistemas especializados precisam ser integrados:
1. **Zabbix:** NMS especialista em métricas, coleta SNMP, disponibilidade e triggers.
2. **UniFi Controller:** Fonte da verdade para redes SDN UniFi (Switches, APs, Gateways, SSIDs, portas PoE e clientes Wi-Fi).
3. **n8n:** Orquestrador de automações e envio de mensagens externas (WhatsApp, Telegram, E-mail).

Sem uma **Camada de Integração (Integration Layer)** bem definida, cada provedor seria implementado de forma ad-hoc (ex: `ZabbixService`, `UniFiService`, `N8nService` com padrões de autenticação, tratamento de erro, cache e logs divergentes), levando a:
- Duplicação de código e responsabilidades.
- Persistência indevida de telemetria de massa no PostgreSQL.
- Perda de visibilidade sobre o status de conectividade (Health) dos sistemas terceiros.
- Risco de quebra de idempotência ou concorrência na resolução de dispositivos.

---

## Decisão

### 1. Separação de Responsabilidades (Princípio de Registro Operacional)

- **InfraControl (Operations Center):** É o **System of Record** para `Device`, `Alert`, `Incident`, `Task`, `SLA/SLO`, auditoria e estado de atendimento.
- **Zabbix:** Mantém o histórico numérico pesado de métricas e executa a avaliação de triggers de infraestrutura.
- **UniFi:** Mantém o estado da topologia Wi-Fi/Switching. O InfraControl consome inventário/eventos operacionais e disponibiliza telemetria sob demanda via API REST.
- **n8n:** Executa fluxos de mensagens externas. **O n8n NÃO gerencia o estado dos incidentes**; o InfraControl é a única autoridade para alterar o status do `Incident` (`Open`, `Investigating`, `Identified`, `Monitoring`, `Resolved`).

---

### 2. Padrão de Autenticação por Provedor

| Provedor | Direção | Mecanismo de Autenticação | Configuração |
|:---|:---|:---|:---|
| **Zabbix** | Inbound (Webhook) | Header `X-Zabbix-Token` (comparação ordinal byte-a-byte, endpoint `[AllowAnonymous]` na allowlist de arquitetura). | `Integrations:Zabbix:Token` |
| **UniFi** | Outbound (REST) / Inbound (Webhook) | Sessão por Cookie/Token `unifises` via `POST /api/auth/login` (com re-autenticação automática no `IHttpClientFactory` em caso de 401). | `Integrations:Unifi:BaseUrl`, `Username`, `Password` |
| **n8n** | Outbound (Webhook) | HTTP POST assinado com Secret / Token de Webhook no header `X-InfraControl-Secret`. | `Notifications:Channels:Webhook:Url` |

---

### 3. Armazenamento Seguro de Segredos (Secrets)

- Segredos (`Tokens`, `Senhas`, `API Keys`) são lidos de variáveis de ambiente (`appsettings.json` em dev / envs de produção) e mantidos estritamente na camada de Infrastructure.
- DTOs de leitura da API (`IntegrationDto`) possuem **redaction total** de segredos (`[JsonIgnore]` ou valores omitidos).
- A auditoria do sistema (`AuditEntry`) aplica metadados de redaction para garantir que tokens e senhas jamais sejam persistidos em logs ou histórico.

---

### 4. Representação do Agregado `Integration`

O sistema criará o modelo unificado de acompanhamento de integrações no módulo `Domain`:

```csharp
public enum IntegrationProvider { Zabbix = 1, Unifi = 2, N8n = 3 }
public enum IntegrationStatus { Disconnected = 0, Connected = 1, Degraded = 2, Disabled = 3 }

public sealed class Integration : AggregateRoot
{
    public IntegrationProvider Provider { get; private set; }
    public IntegrationStatus Status { get; private set; }
    public DateTime? LastCheckedAt { get; private set; }
    public DateTime? LastSyncAt { get; private set; }
    public long? LatencyMs { get; private set; }
    public string? LastError { get; private set; }
    public string? ConfigurationJson { get; private set; }
}
```

#### Endpoints da API de Integrações (`api/v1/integrations`):
- `GET /api/v1/integrations` — Retorna o status consolidado do painel de integrações.
- `GET /api/v1/integrations/{provider}/health` — Executa a sondagem em tempo real e atualiza o estado de health.
- `POST /api/v1/integrations/unifi/sync` — Dispara a sincronização manual de inventário UniFi.

---

### 5. Health Checks e Diagnóstico de Erros

- Cada provedor possuirá um `IHealthCheck` dedicado registrado no ASP.NET Core Health Checks:
  - `ZabbixHealthCheck`: Valida a presença do token e a disponibilidade do endpoint inbound.
  - `UnifiHealthCheck`: Executa sondagem leve no endpoint de status da API UniFi (`GET /api/self`).
  - `N8nHealthCheck`: Testa a conectividade com o endpoint de webhook configurado.
- Exceções e erros HTTP são capturados pelo middleware/handler e salvos em `LastError` (truncado em 500 caracteres) com registro no Serilog contendo `IntegrationProvider` e `StatusCode`.

---

### 6. Resiliência e Retentativa (Polly)

- Requisições HTTP saintes (para UniFi Controller e n8n Webhook) utilizam políticas do **Polly** configuradas no `IHttpClientFactory`:
  - **Retry:** 3 tentativas com backoff exponencial (1s, 2s, 4s) com jitter.
  - **Circuit Breaker:** Se 5 falhas consecutivas ocorrerem num intervalo de 30 segundos, o circuito abre por 60 segundos, marcando o status da integração como `Degraded`.

---

### 7. Sincronização de Inventário (Sync)

- A sincronização de dispositivos (ex: UniFi Controller → InfraControl `Device`) é **idempotente**:
  1. Busca dispositivos no UniFi Controller via API REST.
  2. Mapeia pelo endereço `MAC` (normalizado sem separadores/caixa baixa).
  3. Atualiza ou cria o `Device` no inventário InfraControl.
  4. Garante o registro na tabela `external_device_references` (`Provider = Unifi`, `ExternalId = MAC`).

---

### 8. Idempotência e Prevenção de Duplicação

- **`MonitoringEvent`:** Garantida pelo índice único PostgreSQL em `(Source, ExternalEventId)`. Reenvios do mesmo evento externo são descartados no Inbox Store.
- **`Alert`:** Garantida pelo índice único PostgreSQL em `(Source, ExternalProblemId)`.
- **`Device`:** Resolução em cascata: `ExternalDeviceReference` → `IP` → `MAC` → `Hostname`.

---

### 9. Normalização de Eventos Externos (`MonitoringEvent`)

Todo evento recebido do Zabbix ou UniFi é primeiramente persistido como a entidade imutável `MonitoringEvent`:

```text
[Evento Bruto] ──► MonitoringEvent ──► AlertEvaluator ──► Alert ──► Incident
```

O `AlertEvaluator.EvaluateExternalAsync` processa o `MonitoringEvent`:
- **NÃO re-aplica streaks de Ping:** Confia na autoridade do Zabbix.
- **Aplica Supressão Topológica (BFS):** Se o switch pai tiver incidente aberto, o alerta externo nasce `Suppressed`.
- **Auto-Abre Incidente:** Se a severidade for Critical e não existir incidente não-resolvido para a mesma causa.

---

### 10. Padrão para Telemetria de Massa (Leveza do PostgreSQL)

- **Princípio de Não Duplicação:** O PostgreSQL armazena apenas cadastros (`Device`), identidades (`ExternalDeviceReference`), eventos operacionais (`MonitoringEvent`) e estados de atendimento (`Incident`/`Task`).
- **Telemetria sob Demanda (UniFi/Zabbix):** Métricas instantâneas (ex: tráfego em bps, potência PoE, lista de clientes Wi-Fi por rádio) são consultadas via API REST sob demanda e mantidas em **cache Redis** com TTL curto (30 a 60 segundos).
- **Atalho de Interface:** A UI do InfraControl exibirá um botão/link **`[Ver no UniFi]`** ou **`[Ver no Zabbix]`** direcionando o operador para o console de origem para análises profundas.

---

## Consequências

- **Arquitetura Padronizada:** Elimina a implementação ad-hoc de integrações.
- **PostgreSQL Leve:** Previne inchaço do banco de dados com telemetria temporária.
- **Monitoramento do Painel de Integrações:** Permite ao operador visualizar na UI a saúde de cada conexão (`Connected`, `Degraded`, `Disconnected`).
- **Compatibilidade:** Totalmente compatível com o [ADR-023](ADR-023-ZABBIX-INBOUND-WEBHOOK.md) existente.
