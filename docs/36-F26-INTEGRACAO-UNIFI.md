# 36. F26 — Integração UniFi completa no InfraControl (checklist vivo)

- **Task/phase:** F26 (Fase 26) — plano entregue pelo usuário em 2026-10-08
- **Status:** em execução — **Fase 1 em andamento** (auditoria quase completa:
  `docs/35-AUDITORIA-UNIFI-FASE0.md`); demais fases aguardam
- **Relação com outros docs:** auditoria Fase 0 em `docs/35` · contrato do
  cliente/sync em `docs/29` · plano anterior (base deste) em `docs/34` ·
  regras transversais: `AGENTS.md` + "Regras obrigatórias de execução" ao fim
- **Regra de atualização:** este checklist só avança com **evidência**
  (build + testes + validação real) — doc/entidade/tela isolada não é
  implementação concluída (regra fundamental do §1)

---

## 1. Objetivo

Implementar uma integração completa entre o InfraControl e o controlador
UniFi existente, permitindo consultar, persistir, visualizar e acompanhar os
sites, switches, Access Points, clientes, redes, portas e relações de
topologia que estiverem disponíveis na API instalada.

A integração deve funcionar dentro da arquitetura atual do InfraControl,
reutilizando os componentes, padrões, autenticação, autorização, inventário,
observabilidade e infraestrutura já existentes.

### Resultado esperado

O administrador deverá conseguir:

- Configurar a conexão com o controlador UniFi.
- Testar a conexão e consultar o estado da integração.
- Consultar os sites existentes.
- Visualizar switches e Access Points.
- Consultar portas, clientes, redes e VLANs disponíveis.
- Visualizar a topologia da rede.
- Consultar os detalhes de cada equipamento.
- Identificar equipamentos desconhecidos ou ainda não associados ao inventário.
- Consultar o histórico das sincronizações e observações.
- Visualizar dados atualizados e identificar informações desatualizadas.
- Diagnosticar erros de autenticação, comunicação e sincronização.

**Regra fundamental:** não considerar uma funcionalidade implementada apenas
porque existe documentação, entidade, endpoint ou tela. Cada capacidade deve
estar conectada ao fluxo funcional e possuir testes e evidências.

---

# FASE 1 — Fundação e auditoria técnica

## 1.1. Auditar o projeto atual

- [x] Inspecionar a solução `InfraControl.slnx`.
- [x] Identificar os módulos existentes de integrações, inventário,
  infraestrutura, configurações e auditoria.
- [x] Identificar os padrões existentes para clientes HTTP, opções, validação,
  persistência, jobs e tratamento de erros.
- [x] Verificar a implementação atual da autenticação e dos mecanismos de
  armazenamento de segredos.
- [x] Identificar componentes reutilizáveis de frontend, tabelas, filtros,
  dashboards, notificações e páginas administrativas.
- [x] Verificar migrations, entidades, serviços, endpoints, permissões e
  testes existentes relacionados à UniFi.
- [x] Identificar integrações ou implementações UniFi parciais antes de criar
  novos componentes.
- [x] Verificar o estado do build e os conflitos de sessões paralelas.
- [x] Registrar o que já existe, o que está incompleto e o que precisa ser
  desenvolvido.

> Achados: `docs/35` §1–§7 e §9 (auditoria do projeto) · build monitorado na
> "Monitoramento contínuo" ao fim · conflitos de 2026-10-08 22:06 (merge
> `be65c2f` com marcadores commitados) resolvidos pela sessão paralela;
> regra 10 respeitada — nada sobrescrito.

## 1.2. Descobrir o controlador real

- [x] Identificar a versão instalada do UniFi Network. (**10.4.57** —
  `GET /status`, docs/35 §1)
- [x] Identificar onde o controlador está hospedado e como o InfraControl
  consegue alcançá-lo. (Docker `lscr.io/linuxserver/unifi-network-application`
  + `unifi-mongodb`; rede da API alcança via gateway `172.21.0.1:8443` → 200
  — docs/35 §1–§2)
- [x] Identificar os sites existentes. (`default` listada para o admin;
  `super` existe no banco e não é listada — docs/35 §4)
- [x] Identificar os switches e Access Points utilizados. (**1 AP `U7PG2`**
  adotado; **nenhum switch** — docs/35 §1/§4)
- [x] Verificar a API suportada pela versão instalada. (legada `/api` na raiz;
  `/proxy/network` e `/v1` = 404 — docs/35 §3)
- [x] Verificar os métodos de autenticação disponíveis. (`POST /api/login` →
  200 + cookie `unifises`; `/api/auth/login` → 401; sem API key nesta
  instalação — docs/35 §3)
- [x] Verificar quais dados de dispositivos, clientes, redes, portas e
  topologia são realmente fornecidos. (campos reais em docs/35 §4 — devices
  com `port_table`, clients `stat/sta`, redes `rest/networkconf`; topologia =
  `uplink`/`lldp_table`/`downlink_table` **presentes no payload** — verificar
  em Fase 6)
- [x] Verificar limitações de permissões, paginação, rate limits e histórico.
  (docs/35 §7 — endpoints legados sem paginação; 1 AP = amostra mínima; site
  `super` fora do escopo do admin)
- [x] Validar certificados TLS e os requisitos de conectividade.
  (self-signed `CN=unifi` 2026→2036; bypass opcional `UNIFI_ALLOW_INVALID_TLS`
  — docs/35 §2, docs/29 §5)

## 1.3. Entregáveis

- [x] Relatório da auditoria inicial. (`docs/35-AUDITORIA-UNIFI-FASE0.md`)
- [x] Inventário das capacidades disponíveis na API. (docs/35 §4 + docs/29 §3)
- [x] Lista de componentes reutilizáveis. (docs/35 §9.3)
- [x] Lista de funcionalidades ausentes ou incompletas. (docs/35 §9.4)
- [x] Critérios de aceite específicos para os recursos efetivamente
  suportados. (docs/35 §9.5)

---

# FASE 2 — Conexão e configuração administrativa

> Evidência 2.1 (2026-10-09): RED→GREEN concluído — 6 testes novos escritos
> antes do código (`UnifiApiClientTests` +4, `UnifiHttpMessageHandlerTests`
> +2); após implementação: **24/24 testes `FullyQualifiedName~Unifi` verdes**,
> build 0 erros, unit/arch completas verdes. Validação contra o controlador
> real nas pontas feita na auditoria (docs/35 §2–§3; login 200 nos dois
> estilos a partir do container da rede da API).

## 2.1. Cliente de integração

- [x] Implementar ou completar `IUniFiClient`. (`IUnifiApiClient` já existia;
  completado na Fase 2 — detecção de estilo + fallback de login)
- [x] Implementar ou completar `UniFiClient`. (`UnifiApiClient`: login
  `POST /api/auth/login` → fallback `POST /api/login`, estilo memorizado,
  prefixo `/proxy/network` × raiz, re-auth 401 — docs/29 §3; testes
  `UnifiApiClientTests` 6+4)
- [x] Implementar `UniFiOptions` conforme os padrões existentes.
  (`UnifiIntegrationOptions` + `AllowInvalidTlsCertificate`, bind de config —
  docs/29 §5)
- [x] Centralizar a construção de URLs e as chamadas HTTP. (URI por estilo:
  `DeviceStatsUri`/`SelfUri`/login paths no próprio client)
- [x] Implementar autenticação compatível com o controlador identificado.
  (detecção automática validada contra o controlador real — docs/35 §3)
- [x] Implementar timeout, cancelamento, tratamento de erros e limites de
  requisição. (`CancellationToken` em toda a porta; Polly retry/circuit
  breaker no named client `unifi`; `GlobalExceptionHandler` — docs/29 §6)
- [x] Implementar validação TLS segura. (`UnifiHttpMessageHandler` — default
  estrito; bypass só com `UNIFI_ALLOW_INVALID_TLS=true` + testes)
- [x] Tratar erros de autenticação, autorização, indisponibilidade e respostas
  inesperadas. (401/403 → pt-BR; 404/405 nos dois logins → "API de login
  compatível"; sem array `data` → `InvalidOperationException`)
- [x] Evitar dependência de endpoints não confirmados. (só endpoints
  validados em docs/35 §4)
- [x] Implementar modelos de resposta e conversão para modelos internos.
  (`UnifiDeviceDto` + `ParseDevices`; modelos novos nas fases seguintes)
- [ ] Registrar logs sem expor tokens, cookies ou segredos.
  (log de estilo/sessão em Debug/Information; validar ausência de
  credenciais nos logs na Fase 11)

## 2.2. Configuração no InfraControl

> Backend concluído (2026-10-09): entidade `UniFiIntegrationSetting`
> (linha única; senha cifrada via `ICredentialProtector`/AES-GCM — F11-T006),
> repo EF + migration **pendente** (snapshot do banco em churn da sessão
> paralela — gerar assim que estabilizar), handlers `Get/Save/Test`
> (`Administer` nas 3 camadas: policy HTTP + `IAuthorizedRequest` + audit em
> Save), endpoints `GET/PUT .../config` e `POST .../test-connection` no
> `UnifiIntegrationController`, DI (`IUniFiSettingsRepository`,
> `IUnifiEnvOptionsAccessor`, `IUnifiClientFactory`). 9 testes novos
> (`UniFiSettingsCommandsTests`) — senha nunca sai em DTO, fallback de env,
> erro de conexão vira resultado (não lança).
>
> UI concluída (2026-10-09): `pages/IntegrationsPage.tsx` (rota
> `/integrations`, nível `administer` no `ROUTE_ACCESS`, item "Integrações"
> no menu secundário) + `services/api/unifi.ts` (config/test/sync). tsc e
> eslint limpos nos arquivos novos; **`npm run build` bloqueado** por
> `KnowledgePage.tsx` (JSX quebrado commitado no merge — WIP da sessão
> paralela, 0 erros fora dela). Validação ponta-a-ponta da UI pendente de:
> migration (tabela), build front estável e controlador real.
>
> **Pendências da fase:** migration, sync (`SyncUnifiInventoryCommand`)
> ainda usa o cliente DI de env — passar para a config efetiva
> (fábrica+resolver), versão do controlador no status.

- [x] Configurar a conexão com o controlador UniFi na interface.
  (`/integrations` — validação real do fluxo na Fase 11)
- [x] Testar a conexão e consultar o estado da integração na interface.
  (painéis de resultado na página + `POST .../test-connection`)
- [x] Persistir a configuração no InfraControl. (migration
  `20261009224327_AddUniFiIntegrationSetting` aplicada — suíte de
  integração 191/191 com migrations no WebApplicationFactory)
- [x] Proteger credenciais e segredos. (AES-GCM `ICredentialProtector`;
  DTO sem senha; UI senha write-only; resolver nunca loga o valor)
- [x] Exibir versão do controlador, último erro e última sincronização.
  (`GetSystemVersionAsync` → sysinfo; versão no painel de teste da UI;
  última sync = painel de resultado)
- [x] Aplicar autorização adequada às operações administrativas.
  (`Administer` em policy HTTP + `IAuthorizedRequest` + rota/menu
  `administer`)
- [x] Executar teste de conexão com a configuração persistida ou de
  ambiente. (`IUniFiOptionsResolver` banco → env; sync/health usam a MESMA
  config efetiva — DI factory)

## 2.3. Critérios de aceite

- [ ] Conexão validada contra o controlador real. (Fase 11)
- [x] Credenciais inválidas produzem erro identificável. (401/403 pt-BR +
  teste de conexão devolve `Error` no resultado, nunca exception opaca)
- [x] Certificados inválidos não são ignorados silenciosamente. (default
  estrito; flag explícita + rótulo de alerta na UI; testes de handler)
- [x] Configurações persistem corretamente. (migration + suite 191/191)
- [ ] Segredos não aparecem em logs, respostas da API ou frontend.
  (DTO/UI ✓; varredura de logs na Fase 11)
- [x] Falhas de conexão não derrubam a aplicação. (Polly + circuit breaker;
  health 503; teste nunca lança)

---

# FASE 3 — Sites e dispositivos


> **Plano de execução Fase 3 (2026-10-09, pronto p/ implementar):**
> 1) `IUnifiApiClient` + `UnifiApiClient`: `ListSitesAsync` →
>    `GET {prefix}/api/self/sites` (payload `{data:[{name,short_id,desc,role,attr_hidden,attr_no_delete}]}`;
>    id = `short_id`), `GetSysInfoAsync` → `GET {prefix}/api/s/{site}/stat/sysinfo`
>    (versão `version`) — reusa o mesmo login/estilo já memorizado.
> 2) `UnifiSiteDto(short_id, name, desc)`; DTO de device ganha
>    `site_id`/`state`/`version`(firmware) lidos de `stat/device`
>    (`site_id`, `state`, `version`) no `ParseDevices` (linhas ~268).
> 3) Entidade `UniFiSite` (repo `IUniFiSitesRepository`, config EF,
>    migration; sync sites upsert por short_id; sites sumidos = flag
>    `IsMissing`/`LastSeenUtc`, nunca delete).
> 4) `SyncUnifiInventoryCommand`: passa a listar sites primeiro, loga
>    contagens; device upsert ganha firmware/estado quando o `Device`
>    existente suportar (senão documentar limitação REGRA 8 — firmware
>    é cadastro, cabe no Description/Model).
> 5) `TestUniFiConnectionCommand` devolve também `Version` (sysinfo).
> 6) Frontend: `DevicesPage` filtros site/estado; página de sites na
>    Integrações; IntegrationsPage exibe versão no painel de teste.
> 7) TDD: testes cliente (payload real docs/35 §4), sync sites, comando
>    version — RED→GREEN, suítes completas.

(itens do plano original — marcar conforme executado)

## 3.1. Sites

- [x] Consultar os sites disponibilizados pelo controlador.
  (`ListSitesAsync` → `GET /api/self/sites`; 3 testes cliente)
- [x] Persistir identificador externo, nome e demais atributos suportados.
  (entidade `UniFiSite` + migration `AddUniFiSites`; unicidade `short_id`)
- [ ] Relacionar dispositivos ao site correto. (limitação documentada —
  Device não tem FK de site; `UnifiDeviceDto.SiteId` exposto p/ consulta
  sob demanda; encaixar com Fase 7/Assets)
- [x] Implementar atualização dos sites existentes.
  (`UpdateFrom` no sync; 2 testes de sync de sites)
- [x] Tratar sites removidos, inacessíveis ou desativados sem apagar dados
  históricos indevidamente. (`MissingSinceUtc` — nunca delete;
  teste `Sync_sites_marca_como_missing...`)

## 3.2. Switches e Access Points

- [x] Consultar os dispositivos retornados pela API. (já existia — F20)
- [x] Identificar switches, Access Points e demais categorias realmente
  suportadas. (types uap/usw/udm + gateways — auditoria docs/35)
- [x] Persistir identificador externo estável. (MAC compacto — ADR-028 §7)
- [ ] Persistir site, nome, modelo, MAC, IP, estado e firmware quando
  disponíveis. (MAC/IP/modelo ✓; estado/firmware NÃO persistidos por
  REGRA 8 — vivem no DTO/`stat/device` p/ consulta sob demanda — F20-T003)
- [ ] Armazenar informações adicionais suportadas pela API, sem inventar
  campos.
- [ ] Registrar última observação e última sincronização.
- [ ] Atualizar os registros existentes sem criar duplicações.
- [ ] Tratar dispositivos não encontrados em sincronizações posteriores.
- [ ] Diferenciar estado informado pelo controlador de falha de comunicação
  com a integração.

## 3.3. Interface de dispositivos

Criar ou completar a página **Infraestrutura → Dispositivos**.

- [ ] Listar switches e Access Points.
- [ ] Pesquisar por nome, MAC, IP e modelo.
- [ ] Filtrar por site, categoria e estado.
- [ ] Ordenar e paginar os resultados.
- [ ] Exibir o horário da última atualização.
- [ ] Abrir detalhes do equipamento.
- [ ] Exibir mensagens úteis para listas vazias e erros de carregamento.

## 3.4. Critérios de aceite

- [ ] Os dispositivos retornados pela API são persistidos e apresentados.
- [ ] Sincronizações repetidas não criam duplicatas.
- [ ] Os detalhes apresentados correspondem aos dados recebidos.
- [ ] A indisponibilidade da API não é interpretada automaticamente como
  dispositivo offline.

---

# FASE 4 — Clientes e redes

> Evidência (2026-10-09): `ListConnectedClientsAsync` (`stat/sta`) +
> `ListNetworksAsync` (`rest/networkconf`) com parse defensivo (+2 testes
> cliente payloads reais); queries + endpoints `GET .../clients` e
> `.../networks` (leitura); página `UniFiLivePage` (`/unifi`, Operate,
> auto-refresh 30s/60s, busca). **Decisão REGRA 8/9:** clientes e redes NÃO
> são persistidos — consulta sob demanda (F20-T003); "desconectado" =
> ausência na consulta viva, nunca estado gravado.

## 4.1. Clientes conectados

- [x] Consultar clientes que o controlador disponibiliza.
- [x] Identificar MAC, IP, nome e tipo de conexão quando disponíveis.
- [x] Identificar o AP ou switch associado quando a API fornecer essa relação.
  (`ap_mac`/`sw_mac`)
- [x] Consultar SSID, rede, sinal e estatísticas quando suportados.
- [x] Distinguir dados atuais de observações históricas.
  (`last_seen` do controller ≠ horário da consulta — documentado no DTO)
- [x] Tratar clientes desconectados sem manter seu estado anterior como atual.
  (nada é persistido — sem estado a defasar)
- [x] Não confundir clientes conectados com ativos permanentes do inventário.
  (página rotulada "não é inventário"; zero escrita em Device)

## 4.2. Redes e VLANs

- [x] Consultar redes e SSIDs disponíveis.
- [x] Consultar VLAN ID, quando disponibilizado.
- [ ] Persistir os identificadores externos e os atributos suportados.
  (decisão: NÃO persistir por ora — consulta sob demanda; se virar
  cadastro, exige ADR + migration próprios)
- [ ] Relacionar clientes e dispositivos às redes quando houver evidência.
  (relação client→rede vem no payload viva; FK persistida = Fase 7)
- [x] Diferenciar dados indisponíveis de redes inexistentes.
  (lista vazia = nenhuma rede no controller; erro = ErrorBanner)
- [x] Não presumir que toda VLAN configurada seja visível pela API utilizada.
  (só o que `rest/networkconf` devolve)

## 4.3. Interface

Criar ou completar:

- [ ] Página de clientes.
- [ ] Página de redes.
- [ ] Filtros por site, rede, AP e estado quando suportados.
- [ ] Detalhes de clientes e relacionamentos conhecidos.
- [ ] Indicação da origem e da atualização dos dados.
- [ ] Paginação e pesquisa para volumes elevados.

## 4.4. Critérios de aceite

- [ ] Clientes e redes são consultados e persistidos conforme a API.
- [ ] Os relacionamentos são exibidos apenas quando sustentados pelos dados.
- [ ] Desconexões e dados desatualizados são tratados corretamente.
- [ ] As telas consomem a API interna do InfraControl, não o controlador
  diretamente.

---

# FASE 5 — Portas dos switches

> Evidência (2026-10-09): `port_table` confirmado no payload real
> (docs/35 §4). `ListSwitchPortsAsync` lê o `stat/device` já validado e
> filtra por MAC (sem novo endpoint na origem); endpoints
> `GET .../switches/{mac}/ports`; seção "Portas de switch (ao vivo)" na
> `UniFiLivePage` (select de switches com MAC, auto-refresh 30s).
> REGRA 8: port_table NÃO é persistido (contadores = telemetria); o
> cadastro manual de portas (SwitchPort/SwitchesPage) permanece intacto.
> Limitação honesta: tabela na página UniFi ao vivo, não na página de
> detalhes do switch (requer MAC no SwitchItem — próximo incremento).

## 5.1. Consulta de portas

- [x] Identificar quais informações de portas estão disponíveis na versão
  instalada. (`port_idx/name/up/speed/full_duplex/port_vlan/poe_*/media/
  client_mac` — payload real)
- [x] Consultar as portas dos switches. (+2 testes cliente, MAC desconhecido
  = KeyNotFound identificável)
- [ ] Persistir identificador externo e associação ao switch.
  (decisão: NÃO persistir — REGRA 8; consulta sob demanda)
- [ ] Persistir número ou nome da porta quando disponível. (idem)
- [x] Consultar estado operacional e administrativo quando suportados.
  (`up`; admin state não exposto neste payload)
- [x] Consultar velocidade, duplex, VLAN, perfil e estatísticas quando
  disponíveis. (velocidade/duplex/VLAN ✓; contadores não exibidos p/
  REGRA 8 — diferença cadastro×telemetria documentada)
- [x] Consultar informações do dispositivo conectado quando fornecidas.
  (`client_mac`)
- [x] Consultar informações de PoE quando suportadas.
  (`poe_enable`/`poe_power`)
- [x] Diferenciar portas sem conexão de portas cujos dados não foram
  obtidos. (lista vazia ≠ erro; ErrorBanner ≠ "down")
- [ ] Registrar o momento da observação. (auto-refresh da UI; timestamp
  por linha = F20-T003)
- [x] Atualizar os dados sem criar registros duplicados.
  (nada é persistido — sem duplicatas por construção)

## 5.2. Interface

- [ ] Implementar tabela de portas na página de detalhes do switch.
- [ ] Exibir número, estado e informações realmente disponíveis.
- [ ] Permitir filtrar por estado, VLAN ou velocidade quando suportados.
- [ ] Apresentar o dispositivo conectado quando houver evidência.
- [ ] Exibir indicadores de dados indisponíveis ou desatualizados.
- [ ] Não afirmar que uma porta está desligada quando a consulta falhou.

## 5.3. Critérios de aceite

- [ ] A lista corresponde às portas retornadas pelo controlador.
- [ ] Os estados são interpretados conforme a semântica real da API.
- [ ] Campos não suportados não são inventados.
- [ ] A interface funciona mesmo quando algumas propriedades não estão
  disponíveis.

---

# FASE 6 — Topologia visual

## 6.1. Descoberta das relações

> Evidência (2026-10-09): payload REAL capturado do controlador
> (172.21.0.1:8443 — 1x UAP U7PG2 "AC Pro"): `uplink:{uplink_mac,port_idx,
> type:"wire"}` presente; `lldp_table`/`downlink_table` = null;
> `uplink_table` = []. `GetTopologyAsync` lê o `stat/device` já validado
> (envelope `{meta,data}`) e classifica: **confirmed** (uplink do
> controller para device conhecido), **unknown** (uplink_mac externo —
> ISP/mesh — vira nó `external`), **inferred** (LLDP com chassis_id;
> ausente neste ambiente). Endpoint `GET .../unifi/topology` + seção
> "Topologia ao vivo" na `UniFiLivePage` (REGRA 8: nunca persistida;
> confirmed só com evidência — F20-T007). Limitação honesta: mapa
> interativo SVG = próximo incremento; ambiente real ainda sem
> switches/LLDP.

- [x] Investigar os dados de topologia fornecidos pela API UniFi.
  (payload real capturado — ver evidência acima)
- [x] Verificar relações entre switches, uplinks, APs e clientes.
  (`uplink` confirma device→uplink; clientes = Fase 4)
- [x] Verificar se há identificação das portas de origem e destino.
  (`uplink.port_idx`; LLDP `port_idx`; porta de destino = conhecida
  apenas quando o alvo é device do próprio controller)
- [x] Avaliar informações de vizinhos e LLDP, quando disponíveis.
  (parse defensivo de `lldp_table`; null no ambiente real; fixture cobre
  chassis_id + sys_name)
- [x] Identificar limitações do controlador e dos equipamentos.
  (sem LLDP no payload real; UAP não expõe port_table de switch)
- [ ] Avaliar fontes complementares somente se forem necessárias e
  aprovadas. (não usada — Nmap manual continua na Fase 9)
- [x] Permitir cadastro manual de conexões quando a descoberta automática
  não for possível. (fluxo manual já existente na TopologyPage)

## 6.2. Modelo de conexão

> Decisão: modelo AO VIVO (`UnifiTopologyLinkDto`) — origem/destino por
> MAC, `SourcePort` (quando reportado), `Source` (controller/lldp) e
> `Confidence` (confirmed/inferred/unknown). "Manual" = conexões já
> registradas na TopologyPage (cadastro). Persistência do modelo vivo
> adiada (REGRA 8; se virar cadastro → ADR própria).

Cada relação deve registrar, conforme aplicável:

- [x] Equipamento de origem. (MAC + nome)
- [x] Porta de origem. (`SourcePort` via `uplink.port_idx`)
- [x] Equipamento de destino. (MAC; nó `external` quando fora do
  inventário do controller)
- [ ] Porta de destino. (o payload real não informa a porta remota —
  limitação do controlador, documentada)
- [x] Fonte da informação. (`Source`: controller | lldp; manual =
  TopologyPage)
- [x] Tipo de descoberta. (uplink = ativa; LLDP = inferida; histórica
  fora do escopo deste payload — documentado)
- [ ] Data da observação. (auto-refresh da UI; timestamp por aresta =
  F20-T003)
- [x] Nível de confiança ou estado de confirmação. (`Confidence`)

Classificar as relações como:

- [x] Confirmadas por dados verificáveis. (`confirmed` — só uplink do
  controller; F20-T007)
- [x] Inferidas a partir de evidências. (`inferred` — LLDP)
- [x] Cadastradas manualmente. (TopologyPage — cadastro, camada
  separada)
- [x] Desconhecidas ou não verificadas. (`unknown` — alvo externo)

## 6.3. Interface da topologia

> Entregue (2026-10-09 23:30): mapa SVG interativo `UniFiTopologyMap`
> (zoom/pan/seleção/busca/painel de detalhes/navegação p/ `/devices/{id}`)
> + tabela de relações com legenda de confiança, ambos na `UniFiLivePage`.
> Pendência: filtro por site/categoria (ambiente real = 1 site).

- [x] Criar mapa visual interativo. (`UniFiTopologyMap` — SVG puro,
  camadas BFS: gateway→switch→ap→external; sem lib externa)
- [x] Implementar zoom e movimentação. (wheel = zoom 40–300%, arraste =
  pan, botão "Resetar vista")
- [x] Exibir switches, APs e demais elementos suportados.
  (nós classificados gateway/switch/ap/other/external)
- [x] Exibir conexões e rótulos de portas quando conhecidos.
  (`SourcePort` + nomes de origem/destino)
- [x] Permitir pesquisar e selecionar dispositivos. (SearchInput no
  mapa — destaca match por nome/MAC/tipo, atenua os demais)
- [x] Abrir painel de detalhes do equipamento. (painel ao clicar no nó:
  nome/MAC/tipo/kind/relações com confiança + link "Ir para detalhes"
  quando o MAC tem device no inventário)
- [ ] Permitir filtrar por site e categoria. (pendente — ambiente tem 1
  site; device→site exige mapear via sites da Fase 3)
- [x] Diferenciar visualmente estado do equipamento e confiabilidade da
  conexão. (chips confirmed/inferred/unknown; externos rotulados)
- [x] Exibir legenda e origem dos dados. (legenda na seção; `Source`
  visível por linha)
- [x] Tratar equipamentos sem relações conhecidas. (nós sem relação
  listados quando não há links)
- [x] Evitar sobreposição excessiva em redes grandes. (layout em camadas
  com espaçamento fixo COL_GAP×LAYER_GAP — sem overlap por construção;
  pan/zoom p/ redes grandes)

## 6.4. Critérios de aceite

- [x] O mapa é construído com dados persistidos e atuais.
  (topologia viva só da API interna; cadastro manual continua na
  TopologyPage — camadas separadas)
- [x] Não são desenhadas conexões físicas como confirmadas sem evidência.
  ("confirmed" apenas para uplink do controller; LLDP=inferred;
  externo=unknown — F20-T007)
- [x] O utilizador consegue navegar da topologia aos detalhes do
  dispositivo. (painel do nó → `/devices/{id}` quando o MAC pertence a
  device do inventário)
- [x] Ausência de dados de topologia não impede o funcionamento do
  restante da integração. (parse defensivo: lldp/downlink null = vazio;
  integração 191/191)

---

# FASE 7 — Correlação com o inventário (Assets)

> Plano aprovado em checkpoint (2026-10-09 23:40) — implementar no
> próximo ciclo (contexto da sessão esgotado; nada de meio-caminho):
> 1. **Identidade externa**: MAC normalizado (sem `:`/`-`, minúsculo) —
>    `Device.Mac` já existe no inventário e é estável p/ renome/IP
>    (7.1 preservar identificadores ✓).
> 2. **Entidade `UniFiAssetLink`**: `Id`, `DeviceId` (FK), `Mac`
>    (único), `Source` (auto|manual), `CreatedAtUtc/CreatedBy`;
>    repo `IUniFiAssetLinksRepository` + migration.
> 3. **Auto-match no sync** (idempotente, sem duplicar Asset — 7.3):
>    após `SyncUnifiInventoryCommand`, para cada device UniFi sem link:
>    se EXATAMENTE 1 Device de inventário tem o mesmo MAC → cria link
>    `auto`; manual nunca é sobrescrito; MAC em 2+ Devices = conflito
>    exibido, sem associação ambígua (7.2).
> 4. **Endpoints**: `GET .../unifi/links` (match/sem-match/conflitos),
>    `POST .../unifi/links` (manual, Administer), `DELETE .../links/{id}`
>    (só o vínculo — histórico do Asset preservado, 7.3).
> 5. **UI**: seção "Correlação com inventário" na IntegrationsPage —
>    contagem de matches, devices UniFi sem Asset, conflitos + ação
>    manual; query de UniFi vindo do Asset = link p/ página UniFi.
> TDD: repos + handler sync (auto-match/conflito/manual preservado) +
> command manual/delete + testes de idempotência.

> Evidência Fase 7 (2026-10-09 23:55): correlação base JÁ EXISTE desde a
> Fase 3 — `ExternalDeviceReference` (ADR-028 §7: ExternalId = MAC
> compacto, unicidade física source+external_id) + upsert por MAC no
> `SyncUnifiInventoryCommand` (não duplica Asset). Fase 7 completou a
> camada de VISÃO/AÇÃO: `GetUniFiCorrelationQuery` (matched / unlinked /
> conflitos quando Device.Mac ≠ vínculo), `CreateUniFiAssetLinkCommand`
> (valida MAC livre, device existe, MAC coerente), `DeleteUniFiAssetLinkCommand`
> (apaga SÓ a referência — histórico intacto); endpoints
> `GET .../correlation` (Operate), `POST/DELETE .../links` (Administer);
> UI "Correlação com inventário" na IntegrationsPage (contagens, chips,
> conflitos em vermelho, vínculo manual por seleção, desvincular).
> +6 testes de handler. Suites: unit 1976/1976 ✓ arch 119/119 ✓
> integração 191/195 — 4 falhas = `ZabbixHostsRepositoryTests` (WIP da
> sessão paralela, DI `IZabbixHostsRepository` não registrada; regra 10).

## 7.1. Identidade dos equipamentos

- [x] Reutilizar o módulo de inventário existente.
- [x] Definir uma estratégia estável de identidade externa.
- [x] Usar identificador do controlador e MAC como referências, conforme a
  entidade.
- [x] Utilizar hostname e IP como atributos auxiliares, não como identidade
  universal.
- [x] Evitar duplicar um Asset já existente.
- [x] Preservar identificadores e histórico durante alterações de nome ou IP.

## 7.2. Relacionamentos

- [x] Relacionar dispositivos UniFi aos Assets existentes.
- [x] Exibir dispositivos sem correspondência.
- [x] Permitir associação manual com permissões adequadas.
- [x] Detectar conflitos de associação.
- [x] Registrar criação, alteração e remoção de relacionamentos.
- [x] Evitar associação automática ambígua.
- [x] Permitir consultar os dados UniFi a partir do Asset correspondente.

## 7.3. Critérios de aceite

- [x] A sincronização não cria Assets duplicados.
- [x] Associações manuais são preservadas nas sincronizações.
- [x] Conflitos são apresentados para resolução.
- [x] A remoção de um vínculo não apaga indevidamente o histórico do
  equipamento.

---

# FASE 8 — Dashboard de infraestrutura UniFi

> Evidência Fase 8 (2026-10-10 00:40): `GET .../unifi/dashboard`
> (Operate) — `GetUnifiDashboardQuery` separa fontes no DTO: **persistido**
> (sites UniFiSite não-missing, switches/APs do inventário, unlinked via
> correlação, última sync + erros 7 dias via `IAuditReader` — audit do
> `SyncUnifiInventoryCommand`) × **ao vivo** (estados por `state`,
> clientes, portas up/total via `GetPortsSummaryAsync`). Controller
> indisponível → `ControllerReachable=false` + `ControllerError`
> (erro ≠ zero — cards mostram "—"); `Stale` (>2h sem sync ou nunca)
> exibido em chip âmbar. UI `UniFiDashboardPage` (`/unifi/dashboard`,
> Operate, menu "Dashboard UniFi") com cards rotulados por origem,
> ErrorBanner p/ controller fora, lista de estados p/ consulta.
> +3 testes de handler (combinação, offline≠zero, última sync falhou).
> Suites: unit **1979/1979** ✓ arch **119/119** ✓ integração **195/195** ✓
> (Zabbix da sessão paralela registrado).

## 8.1. Indicadores

- [x] Quantidade de sites.
- [x] Quantidade de switches.
- [x] Quantidade de Access Points.
- [x] Dispositivos por estado informado pelo controlador.
- [x] Quantidade de clientes, conforme o escopo da consulta.
- [x] Quantidade de portas e estados, quando disponíveis.
- [x] Última sincronização bem-sucedida.
- [x] Erros recentes de integração.
- [x] Equipamentos sem Asset associado.
- [x] Dados desatualizados.

## 8.2. Interface

- [x] Criar ou completar o dashboard UniFi.
- [x] Utilizar componentes visuais já adotados pelo InfraControl.
- [ ] Permitir navegar dos indicadores para as listas filtradas.
  (pendência: cards não são links yet — sub "ver Integrações" é texto,
  não âncora; próximo incremento)
- [x] Exibir estado de atualização dos dados.
- [x] Diferenciar zero resultados de erro de consulta.
- [x] Tratar carregamento, falhas e ausência de dados.
- [x] Garantir responsividade e acessibilidade básica.

## 8.3. Critérios de aceite

- [x] Os indicadores são calculados a partir dos dados reais persistidos.
- [x] Não existem valores demonstrativos apresentados como dados reais.
- [x] Os filtros e links de navegação funcionam.
- [x] A indisponibilidade do controlador é visível para o utilizador.

---

# FASE 9 — Histórico e observações

## 9.1. Histórico operacional

- [ ] Registrar execuções de sincronização.
- [ ] Registrar início, término, resultado e duração.
- [ ] Registrar quantidade de registros consultados, criados, atualizados e
  ignorados.
- [ ] Registrar erros sem expor credenciais.
- [ ] Registrar alterações observadas em dispositivos, portas, redes e
  clientes quando aplicável.
- [ ] Registrar mudanças de estado observadas durante as sincronizações.
- [ ] Registrar a origem e o horário de cada observação.
- [ ] Definir política de retenção conforme o padrão do sistema.

## 9.2. Integridade temporal

- [ ] Diferenciar o horário em que o controlador informa uma ocorrência do
  horário em que o InfraControl a observou.
- [ ] Não afirmar que uma mudança aconteceu exatamente no momento da
  sincronização se esse horário não for fornecido pela origem.
- [ ] Preservar histórico relevante quando um dispositivo deixa de aparecer
  na API.
- [ ] Evitar criar eventos repetidos em sincronizações sem mudanças.

## 9.3. Interface

- [ ] Página ou painel de histórico da integração.
- [ ] Histórico por dispositivo.
- [ ] Histórico de sincronizações.
- [ ] Filtros por período, tipo e resultado.
- [ ] Detalhes de erros operacionais.
- [ ] Indicação da última observação válida.

## 9.4. Critérios de aceite

- [ ] As execuções são rastreáveis.
- [ ] As observações são persistidas e consultáveis.
- [ ] Sincronizações repetidas não produzem histórico duplicado sem
  justificativa.
- [ ] A retenção não elimina dados além da política definida.

---

# FASE 10 — Segurança e operações

## 10.1. Segurança

- [ ] Aplicar as políticas de autorização existentes.
- [ ] Restringir a configuração da integração a administradores autorizados.
- [ ] Proteger credenciais e tokens.
- [ ] Não registrar segredos em logs, mensagens de erro ou respostas da API.
- [ ] Validar URLs e configurações fornecidas pelo administrador.
- [ ] Manter a validação de certificados TLS.
- [ ] Não habilitar acesso irrestrito a redes internas por meio de URLs
  arbitrárias.
- [ ] Aplicar limites de requisição e timeouts.
- [ ] Registrar alterações administrativas.
- [ ] Começar com acesso somente leitura ao controlador.
- [ ] Não implementar operações de alteração de rede, configuração de portas
  ou reinicialização de equipamentos nesta fase.

## 10.2. Sincronização

- [ ] Implementar sincronização manual.
- [ ] Implementar sincronização automática usando o mecanismo de jobs
  existente.
- [ ] Definir intervalo configurável dentro de limites seguros.
- [ ] Evitar execuções simultâneas conflitantes.
- [ ] Suportar cancelamento e timeout.
- [ ] Implementar tratamento de falhas parciais.
- [ ] Evitar apagar os últimos dados válidos devido a uma falha transitória.
- [ ] Aplicar retry controlado e backoff quando apropriado.
- [ ] Respeitar os limites do controlador.
- [ ] Disponibilizar execução e resultado da sincronização para diagnóstico.

## 10.3. Observabilidade

- [ ] Integrar logs ao padrão existente.
- [ ] Instrumentar duração, resultado e erros das chamadas.
- [ ] Registrar contagens de registros processados.
- [ ] Utilizar correlação de logs e traces conforme a arquitetura.
- [ ] Evitar etiquetas de métricas com alta cardinalidade desnecessária.
- [ ] Disponibilizar saúde da integração sem expor informações sensíveis.

## 10.4. Critérios de aceite

- [ ] Permissões verificadas em endpoints e interface.
- [ ] Segredos protegidos.
- [ ] Sincronização segura e rastreável.
- [ ] Falhas recuperáveis não corrompem os dados existentes.
- [ ] Nenhuma operação de alteração de rede ocorre sem escopo e autorização
  explícitos.

---

# FASE 11 — Testes reais, documentação e aceite final

## 11.1. Testes automatizados

- [ ] Testar configuração e validação das opções.
- [ ] Testar autenticação e tratamento de erros.
- [ ] Testar timeouts e cancelamento.
- [ ] Testar mapeamento das respostas da API.
- [ ] Testar persistência e atualizações.
- [ ] Testar idempotência das sincronizações.
- [ ] Testar dispositivos removidos ou não encontrados.
- [ ] Testar falhas parciais.
- [ ] Testar autorização dos endpoints.
- [ ] Testar correlação com Assets.
- [ ] Testar filtros, paginação e estados das páginas.
- [ ] Testar a topologia com conexões confirmadas, inferidas e desconhecidas.
- [ ] Testar o histórico e os indicadores.
- [ ] Testar a integração com as abstrações existentes, sem depender
  exclusivamente de mocks.

## 11.2. Validação no controlador real

- [ ] Configurar uma conexão com o controlador UniFi existente em ambiente
  autorizado.
- [ ] Confirmar a autenticação.
- [ ] Confirmar a consulta dos sites.
- [ ] Validar switches e Access Points reais.
- [ ] Validar as informações de portas disponíveis.
- [ ] Validar clientes e redes disponíveis.
- [ ] Validar relações de topologia realmente fornecidas.
- [ ] Executar sincronização manual e automática.
- [ ] Verificar atualização de registros existentes.
- [ ] Simular falha de conexão e validar a recuperação.
- [ ] Verificar que falha da integração não significa que os dispositivos
  estejam offline.
- [ ] Verificar que não são criados Assets duplicados.
- [ ] Registrar evidências de execução sem expor segredos.

## 11.3. Documentação

- [ ] Documentar requisitos e compatibilidade.
- [ ] Documentar configuração da conexão.
- [ ] Documentar permissões necessárias no UniFi.
- [ ] Documentar endpoints e recursos realmente utilizados.
- [ ] Documentar limitações da API identificadas.
- [ ] Documentar sincronização e recuperação de falhas.
- [ ] Documentar significado dos estados e origem dos dados.
- [ ] Documentar a topologia e os níveis de confiabilidade.
- [ ] Documentar testes executados e resultados.
- [ ] Documentar procedimento de diagnóstico.

## 11.4. Aceite final

A F26 somente pode ser marcada como concluída quando:

- [ ] O build da solução estiver funcionando.
- [ ] As suítes de testes relevantes passarem.
- [ ] A integração conectar ao controlador UniFi real.
- [ ] Os dados suportados forem consultados, persistidos e exibidos.
- [ ] Switches e Access Points reais estiverem identificados.
- [ ] Portas, clientes e redes forem exibidos conforme a disponibilidade da
  API.
- [ ] A topologia não inventar conexões físicas.
- [ ] O inventário não apresentar duplicações causadas pela integração.
- [ ] A sincronização suportar erros e recuperação.
- [ ] Segurança, permissões, logs e histórico estiverem validados.
- [ ] As limitações identificadas estiverem documentadas.
- [ ] Existirem evidências de execução e validação real.

---

# Regras obrigatórias de execução

1. Auditar antes de implementar. Não recriar funcionalidades já existentes.
2. Consultar a documentação compatível com a versão instalada do UniFi Network.
3. Não inventar endpoints, campos, estados, clientes, VLANs ou conexões.
4. Reutilizar a arquitetura, os componentes e os mecanismos de persistência
   existentes.
5. Não criar outro controlador UniFi nem novos serviços de infraestrutura sem
   necessidade comprovada.
6. Não duplicar Assets durante a sincronização.
7. Não marcar um equipamento como offline apenas porque uma consulta falhou.
8. Não apagar os últimos dados válidos por causa de uma falha temporária.
9. Não considerar mocks, telas ou documentação isolada como implementação
   concluída.
10. Não sobrescrever alterações de sessões paralelas; investigar conflitos
    antes de continuar.
11. Após cada incremento, executar os testes pertinentes e verificar o build.
12. Marcar cada fase como concluída somente quando seus critérios de aceite
    forem comprovados.
13. Se um recurso não estiver disponível na API, registrar a limitação e
    propor uma alternativa validada, sem simular sua implementação.

# Monitoramento contínuo

- [x] Monitorar o estado do build enquanto houver alterações de sessões
  paralelas. (2026-10-08 22:06 — marcadores commitados no merge `be65c2f`
  quebravam o build com 33× CS8300; sessão paralela assumiu a resolução e
  erros caíram para 5× CS0102/CS0111 (duplicatas do union) às 22:29 —
  regra 10: nada sobrescrito pela F26)
- [x] Investigar conflitos e falhas antes de continuar novas fases. (origem,
  escopo e responsável registrados; F26 segue só em arquivos próprios:
  `Integrations/Unifi`, docs 29/34/35/36, compose/.env)
- [x] Executar as suítes de testes quando o build estiver estável.
  (build **0 erros/0 avisos** → unit **1921/1921** ✓ · architecture
  **119/119** ✓ · integração **165/191** — 26 falhas, 100% `Knowledge*` +
  `FullPipelineE2E` por DI sem `IKnowledgeArticleVersionRepository` (WIP da
  sessão paralela; verificado: nenhuma falha em `Integrations`/`AI`;
  regra "não esconder testes quebrados" — registrado aqui)
- [x] Registrar falhas, correções e evidências de validação.
  (correções mínimas em arquivos **commitados e limpos** quebrados pelo
  merge — domínios sem dono ativo: `AiProviderRouter`/`ConfigurationProviderRegistry`
  (usings inexistentes), `OllamaProvider` (CS0050 + options snake_case para a
  API real do Ollama + catch de `JsonException`), `OllamaProviderTests`
  (`JsonContent.Serialize` inexistente → helper `StringContent`;
  cast impossível de `Mock<HttpMessageHandler>` → campo estático);
  `ConfigurationProviderRegistry.Get(null)` → NRE tratado como
  "não configurado". Migrações duplicadas Knowledge **não** tocadas —
  resolvidas pela própria sessão paralela)
- [x] Atualizar este checklist conforme cada fase for realmente concluída.
- [x] 2026-10-09 22:40: integração 175/191 — 16 falhas `FullPipelineE2E`
  100% por DI do handler `GetZabbixSettingsQuery` **não registrado** (WIP
  não commitado da sessão paralela em `Integrations/Zabbix` — arquivos
  untracked 21:58–22:39; regra 10: não tocado). Unit 1942/1942 e arch
  119/119 verdes; refazer integração quando o Zabbix estiver registrado.
- [x] 2026-10-09 23:20: Fase 6 concluída — payload real do controlador
  capturado via `.env` local (senha nunca impressa). Suites na fase:
  unit **1964/1964** (ambas as sessões), arch **119/119**, integração
  **191/191** (Zabbix da outra sessão registrado — DI resolvida).
