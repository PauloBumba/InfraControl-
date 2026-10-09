# 35. Auditoria UniFi — Fase 0 (descoberta técnica)

- **Task:** Fase 0 do `docs/34-PLANO-INTEGRACAO-UNIFI.md`
- **Data:** 2026-10-08
- **Status:** ✅ **concluída** — critério de saída cumprido (consultas reais à
  API, somente leitura, sem modificar a rede)
- **Método:** inspeção do ambiente Docker local + probes HTTP contra o
  controlador + documentação oficial da versão + leitura (não escrita) do
  MongoDB do controlador para caracterizar a instância

## 1. Versão e hospedagem

| Item | Evidência |
| --- | --- |
| **UniFi Network 10.4.57** | `GET https://localhost:8443/status` → `{"up":true,"server_version":"10.4.57","uuid":"7722c9e9-…"}` |
| Imagem/hospedagem | container Docker `lscr.io/linuxserver/unifi-network-application:latest` (rede `unifi_default`), criado em 03–04/08/2026 |
| Banco | container `unifi-mongodb` — MongoDB 7.0.39 com autenticação (`MONGO_INITDB_ROOT_*` só via env do container) |
| Portas publicadas | `8443/tcp` (UI/API HTTPS) · `8080/tcp` (inform de devices) · `8843/8880/tcp` (portais) · `3478/udp` (STUN) · `10001/udp` (discovery) |
| Instância (leitura do Mongo) | admin `paulo_bumba` · sites `default` + `super` · **1 device adotado** (AP `U7PG2`) · 13 clientes históricos |

Não é uma instalação de produção da empresa — instância pequena de teste já
existente no ambiente (regra "não criar nova instalação do UniFi" respeitada).

## 2. Acesso de rede, TLS e DNS

| Verificação | Resultado |
| --- | --- |
| Controlador alcançável a partir da rede do InfraControl | ✅ de container na mesma rede da API: `curl -sk https://172.21.0.1:8443/status` → **200** (gateway da rede `infracontrol-_infracontrol`; porta 8443 publicada em `0.0.0.0`) |
| TLS | certificado **self-signed** `CN=unifi` (emitido 04/08/2026, válido até 01/08/2036) — **não confiável pelo .NET por padrão** |
| DNS | sem nome dedicado — acesso por IP (`https://172.21.0.1:8443` de dentro da rede da API; `localhost:8443` do host). Nome `unifi` só resolve dentro de `unifi_default` |
| Credenciais | não existiam em `.env`/repositório; admin **redefinido** nesta auditoria (hash `x_shadow` SHA-512 crypt no Mongo) e gravado só em `.env` local (`UNIFI_USERNAME`/`UNIFI_PASSWORD` — arquivo **não versionado**) |

## 3. Tipo de API desta instalação (achado crítico)

Esta instalação é **standalone UniFi Network Application**, não um console
UniFi OS (UDM/Cloud Key). Probes sem autenticação:

| Caminho | Resultado | Significado |
| --- | --- | --- |
| `/api/...` | `401 api.err.LoginRequired` | ✅ API legada **na raiz** funciona |
| `/proxy/network/api/...` | `404` Tomcat | ❌ prefixo UniFi OS **não existe** |
| `/v1/info`, `/v1/sites` | `404` Tomcat | ❌ API oficial nova **não exposta** nesta versão/instalação |
| `GET /status` | `200` | versão/disponibilidade sem login |

Login real:

| Endpoint | Resultado |
| --- | --- |
| **`POST /api/login` (JSON)** | ✅ **200** `{"meta":{"rc":"ok"}}` + cookie `unifises` |
| `POST /api/auth/login` (UniFi OS) | ❌ `401 api.err.LoginRequired` nesta instalação |

Consequência: o `UnifiApiClient` atual (F20-T001) foi escrito para o padrão
UniFi OS e **não funciona nesta instalação** (ver §6).

## 4. Endpoints validados com consulta autenticada (somente leitura)

Todos `GET` com cookie de sessão — **zero ações de alteração**:

| Endpoint | HTTP | Resultado |
| --- | --- | --- |
| `GET /api/self/sites` | 200 | 1 site: `default` |
| `GET /api/s/default/stat/device` | 200 | 1 device (`U7PG2`, `adopted:true`, `state:1`) com **`port_table`** |
| `GET /api/s/default/stat/sta` | 200 | 3 clientes conectados (3 wireless, 0 wired) |
| `GET /api/s/default/rest/networkconf` | 200 | 3 redes (`corporate`, `vlan-only`) |
| `GET /api/s/default/stat/sysinfo` | 200 | ok |
| `GET /api/s/default/stat/health` | 200 | 5 categorias de saúde |

### Campos realmente disponíveis (para as fases seguintes)

- **Device** (`stat/device`): `mac`, `ip`, `name`, `model`, `type`,
  `state`, `adopted`, `version`/`displayable_version`, `uptime`,
  `last_seen`, `num_sta`, `uplink`, **`port_table`** (AP U7PG2 tem 2
  portas) com `port_idx`, `up`, `speed`, `full_duplex`, `autoneg`,
  `port_poe`/`poe_caps`, `enable`, `stp_state`, contadores
  `rx_*/tx_*` (bytes/packets/errors/drops), `is_uplink`, `mac_table_count`.
- **Cliente** (`stat/sta`): `mac`, `ip`, `hostname`, `essid`, `bssid`,
  `ap_mac`, `signal`, `noise`, `channel`, `is_wired`, `is_guest`,
  `first_seen`, `last_seen`, `oui`, `bytes-r`.
- **Rede** (`rest/networkconf`): `purpose`/`type` (corporate, vlan-only),
  nome, VLAN e demais campos de configuração.

### Documentação oficial consultada

- `developer.ui.com/network/v10.4.57` (existe para a versão exata; o site é
  JS — endpoints extraídos da página de exemplo + listagem equivalente
  v10.1.84 e validados por probe real §3/§4).
- A API **oficial nova** (`/v1/sites`, `/v1/sites/{id}/devices`, clients,
  networks, `X-API-Key`) é documentada para o caminho **connector/UniFi OS**;
  **não existe** nesta instalação standalone (404) — não usar como base.

## 5. O que será importado × somente consultado (decisão da Fase 0)

Mantida a **REGRA 8** (já decidida em F20-T001/ADR-028) — a auditoria
confirma que ela é suficiente:

| Dado | Tratamento |
| --- | --- |
| Device (MAC, modelo, IP, nome, estado, firmware, site) | **importado** — upsert por MAC em `Device` + `ExternalDeviceReference` (já existe) |
| Sites, clientes, redes, saúde, sysinfo | **somente consultados** (sob demanda + cache — F20-T003/REGRA 9) |
| Portas (`port_table`), estatísticas, topologia | **somente consultadas** — nunca no PostgreSQL |
| Histórico de observações (Fase 9) | eventos/registros de observação pontuais, sem telemetria de massa |

## 6. Gaps encontrados no código atual (entram na Fase 1)

1. **`LoginUri = /api/auth/login`** → `401` nesta instalação. Precisa
   fallback para `/api/login` (ou detecção do tipo de controlador).
2. **`DeviceStatsUri` prefixado com `/proxy/network`** → `404` nesta
   instalação. Precisa de base path configurável (raiz para standalone).
3. **Sem validação/bypass de certificado TLS** no named client `unifi` —
   cert self-signed `CN=unifi` derruba toda chamada com
   `HttpRequestException` (nenhuma opção de handler configurada em
   `DependencyInjection.cs`).
4. **Validação real pendente do F20-T001** — os 17 testes usam
   `HttpMessageHandler` fake; esta auditoria mostra que, contra o
   controlador real, sync/health **falhariam** pelos gaps 1–3.

## 7. Permissões necessárias e limitações conhecidas

- **Permissão:** admin local do controlador (login usuário/senha → cookie
  `unifises`; sem API key disponível nesta instalação). Escopo de leitura é
  suficiente para as Fases 1–11 (versão somente leitura).
- **Limitações:**
  - API oficial `/v1` e prefixo `/proxy/network` indisponíveis → usar
    somente `/api/...` raiz (validado §4).
  - Instância de teste com **1 AP** — sem switch real; os campos de porta
    existem (`port_table`) mas a validação rica de portas (Fase 5) ficará
    limitada até haver um switch adotado.
  - Site `super` existe no banco mas **não** é listado para o admin em
    `/api/self/sites` (listado apenas `default`).
  - Falha de consulta nunca é prova de device offline (regra do plano —
    os campos `state`/`last_seen` vêm do payload, não de inferência).
  - Credencial só em `.env` local (não versionado); nunca no repositório.

## 8. Critério de saída

- [x] Consulta real à API sem modificar a rede — 6 endpoints `GET`
      autenticados responderam `200` com dados reais (§4), nenhuma escrita.
- [x] Documento técnico entregue (este arquivo) com versão, endpoints
      validados, permissões e limitações.

## 9. Auditoria do projeto (F26 §1.1) — 2026-10-08

### 9.1 O que já existe

| Área | Achado |
| --- | --- |
| Solução | `InfraControl.slnx` (net10): Domain · Application · Api · Infrastructure · Workers + testes Unit/Integration/Architecture |
| Cliente UniFi | `Application/Integrations/Unifi/{Ports/IUnifiApiClient, UnifiDeviceDto, UnifiIntegrationOptions}` + `Infrastructure/Integrations/Unifi/UnifiApiClient` + `SyncUnifiInventoryCommand` + `UnifiIntegrationController` (`POST .../sync`, `GET .../health`) — 17 testes F20-T001; **F26 Fase 2 completou**: detecção de estilo de login (UniFi OS × standalone), prefixo de API por estilo, `UnifiHttpMessageHandler` (TLS opt-in), opção `AllowInvalidTlsCertificate` |
| Padrões HTTP | `IHttpClientFactory` named clients (`unifi`, n8n webhook) com Polly via `AddResilienceHandler` (retry 3× + circuit breaker), options via `Configure`/`Bind` de config, erros pelo `GlobalExceptionHandler` + envelope `ApiResponse` (pt-BR) |
| Persistência | EF Core + migrations (`AppDbContext`), padrão de configuração por entidade em `Infrastructure/Persistence/Configurations`, retenção no `CleanupService` (8 tipos, `CleanupWorker`) |
| Jobs/worker | `Workers` (OutboxDispatcher 5s, Cleanup, avaliação de monitoramento, discovery) — molde p/ sync automático da F26 Fase 10 |
| Autorização | ADR-030: policy HTTP + `IAuthorizedRequest` na mesma camada, guards `RoleAuthorizationStandardizationTests`/`BuildingBlocksStandardizationTests`; `Can`/`ROUTE_ACCESS` no front (só esconde) |
| Auditoria | `AuditBehavior` no pipeline (`AuditResource` por request) + `SecurityAudit` |
| Segredos | só via `.env` (git-ignored) → compose `${VAR:-}`; nunca no repositório (docs/29 §5, `UNIFI_*`, `ZABBIX_TOKEN`, Google ClientSecret) |
| Frontend reutilizável | `components/ui/` (`DataTablePanel`, `PageHeader`, `StatusChip`, `FilterBar`, `SearchInput`, `Pagination`, `ErrorBanner`, `LoadingBlock`, `SelectField`, `Dialog`/`ConfirmDialog`, `FormField`, `EntityView`, `ViewSwitcher`), `hooks/queries.ts` (React Query v5), `Can`, telas já existentes: **DevicesPage** (inventário), **ClientsPage**, **TopologyPage**, **DashboardPage**, **SwitchesPage** (portas cadastradas), **SettingsPage** |
| Inventário UniFi | `Device` (upsert por MAC) + `ExternalDeviceReference` (Source=UniFi, ExternalId=MAC lowercase — ADR-028 §7) + location "UniFi" auto-criada; **REGRA 8** (zero telemetria no Postgres) |
| Testes | suítes unit/arch/integration com fixtures reais (postgres via Docker); testes de integração F20 com `HttpMessageHandler` fake |
| Docs/ADRs | docs/29 (contrato), ADR-028 (camada de integração), ADR-030 (autorização), docs/34 (plano), docs/36 (checklist F26) |

### 9.2 Inventário de capacidades da API (resumo — detalhe §4)

Sites (`/api/self/sites`), devices (`stat/device` com `port_table`, `uplink`,
`lldp_table`, `radio_table`), clientes (`stat/sta`), redes
(`rest/networkconf`), saúde (`stat/health`), sysinfo (`stat/sysinfo`) — todos
`GET` validados contra o controlador 10.4.57. Paginação: não exposta nos
endpoints legados. API oficial `/v1` e prefixo `/proxy/network` indisponíveis
nesta instalação.

### 9.3 Componentes reutilizáveis (para as fases seguintes)

- **Backend:** `IUnifiApiClient` (estende por fase), `SyncUnifiInventoryCommand`
  (molde de upsert idempotente), options/named client/Polly, `ApiResponse` +
  `GlobalExceptionHandler`, `AuditBehavior` (`IAuditableRequest`), `Can`/policies
  ADR-030, `CleanupService` (retenção), Workers (jobs), `EntityView` +
  `hooks/queries.ts` + `FilterBar`/`DataTablePanel`/`StatusChip` (front).
- **Já prontos que as fases podem consumir:** DevicesPage (lista de devices
  já é a tela da Fase 3.3 — completar com site/estado/firmware/staleness),
  ClientsPage/TopologyPage/SwitchesPage (bases das Fases 4/5/6).

### 9.4 Funcionalidades ausentes / incompletas (fila da F26)

1. Tela **Administração → Integrações → UniFi** + configuração persistida +
   teste de conexão com versão/último erro/última sync (Fase 2.2).
2. Persistência de **sites** (`UniFiSite`) e sync de atributos de device
   (firmware/estado/site) além do upsert atual (Fase 3).
3. Endpoints/páginas de **clientes** e **redes** consumindo API interna
   (Fase 4) — hoje só existem como consulta direta auditada.
4. **Portas** reais do controlador (`port_table`) — SwitchesPage mostra portas
   cadastradas manualmente, não as do UniFi (Fase 5).
5. **Topologia** com origem/confiabilidade por evidência (Fase 6) —
   TopologyPage usa conexões do cadastro + discovery.
6. **Correlação** avançada com Assets: histórico de associação,
   duplicidades, associação manual (Fase 7) — base `ExternalDeviceReference`
   já existe.
7. **Dashboard** de infraestrutura UniFi com indicadores reais (Fase 8).
8. **Histórico** de sync/observações + retenção (Fase 9).
9. **Sync automático** por job, mutex, métricas de duração (Fase 10) — só
   manual (`POST /sync`) hoje; `F20-T002` (webhooks) e `F20-T003`
   (cache/consulta sob demanda) seguem abertas e encaixam nas Fases 4/10.
10. Validação real ponta-a-ponta (Fase 11) — auditada a API, falta o fluxo
    completo com containers configurados.

### 9.5 Critérios de aceite (recursos suportados pela API instalada)

| Recurso | Aceite |
| --- | --- |
| Conexão/config | login real 200 nos dois estilos; credencial ruim = 401 pt-BR; TLS self-signed só com flag explícita; segredos nunca em log/resp/UI |
| Sites | listagem igual à do controlador (1 `default`); `super` documentada como fora do escopo do admin |
| Devices | sync idempotente por MAC (2 syncs → mesmas linhas); AP U7PG2 aparece com modelo/IP/estado reais; falha de API ≠ offline |
| Clientes | 3 wireless reais exibidos com AP/sinal; ausência ≠ desconectado (staleness explícita) |
| Redes | 3 redes (`corporate`/`vlan-only`) com VLAN id quando exposta |
| Portas | `port_table` real do device (2 portas do U7PG2 — sem switch na base, limitação §7); campos não expostos = "desconhecido" |
| Topologia | só ligações com origem+confiabilidade; payload tem `uplink`/`lldp_table` — confirmar em Fase 6 antes de afirmar |
| Segurança | policies ADR-030 em todo endpoint novo; sync = `Operate`, config = `Administer`; somente `GET` no controlador |
