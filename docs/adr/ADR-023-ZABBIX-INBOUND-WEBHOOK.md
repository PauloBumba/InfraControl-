# ADR-023 — Webhook de entrada do Zabbix

- **Status**: aceito
- **Data**: 2026-09-30
- **Contexto**: F16-T001 (decisões do usuário 2026-09-30: direção "Zabbix →
  InfraControl", incidente como destino, sem device conhecido cria sem device,
  recuperação OK fecha automático)

## Problema

O Zabbix já monitora a infraestrutura e dispara `PROBLEM`/`OK`, mas os eventos
ficam presos nele: no InfraControl eles não viram incidente, nem passam pelo
ciclo F6/F7 (incidente → tarefa → conhecimento) nem pelas notificações. O caso
de uso do usuário é simples de enunciar: **o Zabbix avisa, o InfraControl
orquestra.**

Três empecilhos reais no sistema atual:

1. **Nenhum endpoint é anônimo.** Toda a superfície exige sessão
   (`[Authorize]`, ADR-016); o Zabbix chama webhook sem cookie nem token de
   usuário. A regra de arquitetura `Public_Endpoints_Should_Be_The_Explicit_
   Allowlist` já prevê isso: endpoint público é `[AllowAnonymous]` explícito
   **e** listado numa allowlist verificada por teste.
2. **O `Alert` não serve para evento externo.** Ele exige `DeviceId` + `CheckKind`
   (nasce do avaliador de checks F6) — um problem do Zabbix não tem "check"
   nosso e pode vir de host fora do inventário.
3. **Incidentes não notificam.** `NotificationEventConsumer` (único consumer)
   trata apenas eventos de identidade; nem incidentes manuais (F6) disparam
   fanout hoje. Notificação de incidente é feature nova — **F16-T002**.

## Decisão

### 1. Endpoint dedicado com token no header

`POST api/v1/integrations/zabbix/problems` — primeiro endpoint público da API.

- `[AllowAnonymous]` explícito no action **+ entrada nova na allowlist** de
  `Public_Endpoints_Should_Be_The_Explicit_Allowlist` (a regra de arquitetura
  que lista os 9 endpoints públicos de autenticação/convite).
- **Auth por token fixo**: header `X-Zabbix-Token` comparado, byte a byte,
  com `Integrations:Zabbix:Token` (`appsettings.json` da Api; binding em
  `Program.cs`).
  - Token **vazio** (default) → **404** com envelope `ApiResponse.Fail`:
    integração desligada, a rota se comporta como inexistente.
  - Token **ausente ou divergente** → **401** com envelope `ApiResponse.Fail`
    ("Token do Zabbix inválido.").
- Comparação ordinal (não case-insensitive): token é segredo, não nome.
- Superfície mínima: um método, um corpo.

### 2. Contrato de payload

O *action* do Zabbix monta o JSON (exemplo de runbook em `docs/27`):

```json
{
  "eventId": "{EVENT.ID}",
  "host": "{HOST.NAME}",
  "ip": "{HOST.IP}",
  "name": "{TRIGGER.NAME}",
  "severity": 4,
  "status": "PROBLEM"
}
```

- `name` obrigatório; `severity` obrigatória inteira 0..5; `status` ∈
  `PROBLEM`|`OK` (case-insensitive). `eventId`, `host`, `ip` opcionais.
- Validação no **handler** (nascida em handler, pt-BR, vira 400 pelo
  `GlobalExceptionHandler`), precedência `name` → `severity` → `status`.
- JSON malformado (ex.: `severity` como string) cai no binding do MVC 400 —
  mesmo comportamento dos demais controllers.

### 3. Mapeamento para Incident (decisões do usuário)

**PROBLEM** → `Incident.Create(name, severidade, deviceId?, descrição)`:

- Device por **IP** (`GetByIpAsync`) com **fallback por hostname**
  (`GetByNameAsync`); sem match → `DeviceId` **nulo** e host/IP/eventId
  guardados na descrição (o incidente nasce sem device — decisão do usuário).
- **Dedup anti-storm**: já existe incidente não-`Resolved` com mesmo **título +
  device** (null incluso) → responde idempotente `duplicate` sem criar outro.
  Mesmo título em devices diferentes não colide (device faz parte da chave).
  É a mesma filosofia do F6 (1 incidente Open por device): heurística, não
  integridade referencial — aceito. Dois hosts desconhecidos sem device com
  o mesmo problema dividem a chave (título + null) — conhecido e aceito.
- Retorno: `{ incidentId, outcome }` com `outcome ∈ created|duplicate`.

**OK** → resolve o incidente aberto correspondente (mesma chave título+device):
`ChangeStatus(Resolved)` → `resolved`; nenhum correspondente → `none`.

**Severidade Zabbix → `IncidentSeverity`** (6 níveis → 4 da UI):

| Zabbix | IncidentSeverity |
|--------|------------------|
| 0 Not classified | Low |
| 1 Information | Low |
| 2 Warning | Medium |
| 3 Average | High |
| 4 High | Critical |
| 5 Disaster | Critical |

### 4. Comando fora do pipeline de roles

`IngestZabbixProblemCommand : BaseCommand<ZabbixProblemResultDto>` **sem**
`IAuthorizedRequest`: o guard é o token no controller. `AuthorizationBehavior`
só exige autenticação/role para `IAuthorizedRequest`, então o comando passa
sem usuário — a allowlist `Admin_Slices_Should_Declare_Required_Roles_Via_
IAuthorizedRequest` não muda (ela é explícita por slice).

### 5. Notificação fica na F16-T002

O webhook deixa o incidente visível no painel, mas **não notifica** (hoje nada
de incidente notifica). Tarefa separada — F16-T002: `IncidentCreatedDomainEvent`
+ outbox + `NotificationEventConsumer`, destinatários por role **Operator e
Admin** (decisão do usuário 2026-09-30), cobrindo incidentes do webhook e os
manuais/automáticos do F6.

## Consequências

- **Sem migration nova**: o webhook só cria/resolve linhas de `incidents`.
- Token em configuração; em produção valor forte (vazio = desligado). Header
  no corpo de rede claro (HTTPS obrigatório em produção — nginx ADR-022).
- O contrato é nosso (o Zabbix não manda JSON pronto): mudar o payload exige
  mudar o action no Zabbix junto — `docs/27` documenta os dois lados.
- Dedup por título+device é heurística; colisão possível só entre hosts sem
  device e mesmo nome de problema (aceito, F6 já pensa assim).
- `RateLimit` padrão (120/min) vale para a rota — suficiente para o volume
  de problems de um Zabbix normal.

## Alternativas descartadas

- **Criar `Alert`**: exigiria `DeviceId` obrigatório (perderia hosts fora do
  inventário) e um `CheckKind` novo sem significado de check real.
- **Consumir a API JSON-RPC do Zabbix** (direção B): payload e credenciais bem
  maiores; candidata futura se quisermos inventário/acks de lá — fora do
  escopo desta decisão.
- **Canal Webhook de saída (F9-T008)**: só levaria notificação adiante; não
  cria incidente nem entra no ciclo F6/F7 — não atende "Zabbix avisa,
  InfraControl resolve".
- **MFA/mTLS/IP allowlist**: sobreengenharia para o primeiro endpoint; token
  estático cobre o caso (Zabbix em rede interna + HTTPS).
