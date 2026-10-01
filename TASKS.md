# TASKS

## Como usar

Cada Task deve ser pequena, verificável e ter dependências explícitas.

### Estados

- BACKLOG
- READY
- PLANNING
- IN_PROGRESS
- BLOCKED
- TESTING
- REVIEW
- VALIDATION
- DONE
- CANCELLED

## Definition of Done

```text
[ ] Objetivo atendido
[ ] Escopo respeitado
[ ] Dependências resolvidas
[ ] Testes necessários criados
[ ] Testes passando
[ ] Build passando
[ ] Segurança verificada
[ ] Observabilidade verificada
[ ] Documentação atualizada
[ ] Review concluído
[ ] Evidências registradas
[ ] PROJECT_STATE atualizado
[ ] Commit realizado
```

## Ordem alinhada com o problema real (2026-09-23)

Problema do blueprint: gestão heterogênea on-premises com visibilidade centralizada.
Caso de uso do usuário: ver cada porta/switch/impressora/AP; toner e cilindro da impressora; AP on/off + internet; ping tipo Zabbix com alerta; acesso remoto a switch (SSH), servidor (SSH) e computador (RDP).

Ordem de execução recomendada (uma Task IN_PROGRESS por vez):

1. `F2-T003` Device (tipos: Switch, Printer, AccessPoint, Camera, Server, Workstation, …)
2. `F2-T008` DeviceProfile — perfil de checks/thresholds padrão por DeviceType (ADR antes)
3. `F5-T003` Ping — up/down de todos (estilo Zabbix)
4. `F5-T004` SNMP Printer — toner, drum/cilindro (Printer MIB) e alertas de nível
5. `F5-T007` Worker de polling contínuo
6. `F6-T001`/`F6-T002` Alert + regras (down, toner/drum baixo, recovery)
7. `F3` Switch/Port/VLAN (liga “o que está em cada porta”)
8. `F11` Remote Access — SSH (switch/servidor), RDP (computador), sem expor credenciais
9. `F4` Discovery (LLDP/ARP/MAC) e `F9` Notifications só depois do loop acima verde

## Fase 0 — Fundação

- [x] F0-T001 Criar solução .NET
- [x] F0-T002 Criar estrutura de projetos
- [x] F0-T003 Criar solução de testes
- [x] F0-T004 Configurar Docker Compose
- [x] F0-T005 Configurar PostgreSQL
- [x] F0-T006 Configurar Redis
- [x] F0-T007 Configurar RabbitMQ
- [x] F0-T008 Configurar migrations
- [x] F0-T009 Configurar observabilidade base
- [ ] F0-T010 Configurar CI (BLOCKED: conta atingiu 2.000/2.000 minutos do GitHub Actions; reset em 01/10/2026)
- [x] F0-T011 Criar estrutura Frontend e dashboard inicial
- [x] F0-T012 Documentar capacidades extraídas do Sentinel e critérios de adoção
- [x] F0-T013 Implementar behaviors base, autorização, cache, transação, rate limiting, Outbox Dispatcher e health endpoints
- [x] F0-T014 Auditoria persistida pós-handler/commit, sem payloads sensíveis
- [x] F0-T015 Inbox e deduplicação de consumidores
- [x] F0-T016 Resiliência por provider (retry, timeout e circuit breaker)
- [x] F0-T017 Retenção e cleanup operacional
- [x] F0-T018 Cache com invalidação opt-in no pipeline
- [x] F0-T019 Feature flags com configuração e override temporário
- [x] F0-T020 Registry de providers extensíveis para Discovery e Monitoring
- [x] F0-T021 Criar BuildingBlocks Shared Kernel alinhado ao Sentinel
- [x] F0-T022 Migrar slices CQRS para ICommand/IQuery/IAuthorizedRequest quando cada módulo for implementado
- [x] F0-T023 Tratamento global de exceções → envelope ApiResponse com mensagens em pt-BR
- [x] F0-T024 Registro automático de repositórios por varredura de assembly <!-- estilo MediatR (RegisterServicesFromAssembly): RepositoryRegistrar varre Application p/ interfaces I*Repository e Infrastructure p/ classes *Repository, registra Scoped com TryAdd (registro manual prevalece como escape hatch) e fail-fast no startup se par ausente/ambíguo; substitui as 11 linhas AddScoped manuais do DependencyInjection; docs/24 -->

## Fase 1 — Identity

- [x] F1-T001 ASP.NET Core Identity (DDD, agregados, eventos, roles, sessões e migration)
- [x] F1-T002 Bootstrap Admin (opt-in, idempotente, transacional e com segredo externo)
- [x] F1-T003 Roles (Vertical Slice/CQRS, policies, sincronização Identity e proteção do último Admin)
- [x] F1-T003A Alinhar Infrastructure ao Sentinel (Auth, Caching, Persistence, Repositories e Services)
- [x] F1-T004 Permissions (catálogo no domínio, matriz por role, claims e policies granulares)
- [x] F1-T008 Password Change (endpoint autenticado, revogação de sessões e auditoria)
- [x] F1-T009 Password Reset (token single-use Redis, expiração e resposta anti-enumeração)
- [x] F1-T011 Auth API e dashboard de usuários (registro, login, refresh, logout, `me`, status e roles)
- [x] F1-T012 Estrutura MVC modular da API alinhada ao Dashboard Sentinel
- [x] F1-T005 Invitation
- [x] F1-T006 Accept Invitation
- [x] F1-T007 Reject Invitation
- [x] F1-T010 Audit
- [x] F1-T013 Guard de ativação do usuário (registro inativo, aceite provisiona conta inativa, `set-password` ativa, login/reset exigem ativo)
- [x] F1-T014 Login social Google (Google Identity Services; `POST /api/v1/auth/google` valida o `id_token` no servidor via `GoogleJsonWebSignature` — assinatura/JWKS, `aud`, `iss`, `exp` e `email_verified`; **sem** Client Secret em lugar nenhum (fluxo GSI: handler `AddGoogle` e pacote `Microsoft.AspNetCore.Authentication.Google` removidos, ficou só `Google.Apis.Auth` 1.72.0) e **sem** auto-provisionamento: só conta já cadastrada e ativa, com resposta anti-enumeração (`401` credencial inválida, `403` e-mail desconhecido ou inativo, `503` provider não configurado); rate limit `Login`; `GoogleAuthOptions` só com `ClientId`/`AllowedDomains` (allow-list de domínio opcional, entradas vazias ignoradas — decisão do usuário 2026-09-28: deixar vazia, sem restrição); claim `displayName` no `GET /me`; 26 testes em `GoogleExternalLoginTests`)
- [x] F1-T015 Antiforgery (CSRF) <!-- ADR-017: double-submit do ASP.NET Core. `AntiforgeryCookieSetup` é a fonte única da configuração (mesma instância usada pelo Program.cs e pelos testes): cookie `XSRF-TOKEN` legível pelo JS (HttpOnly=false é o ponto), Secure+SameSite=Strict, header `X-XSRF-TOKEN`. A política é um middleware global (POST/PUT/PATCH/DELETE autenticados), não atributo de controller — endpoint novo nasce protegido por padrão; anônimo (login/registro/convite/reset/`/auth/google`) não exige token porque não há cookie para explorar. Falha = `AntiforgeryValidationException` → 400 no envelope, mensagem genérica (dizer "faltou" vs "invalidou" seria oráculo). Emissão em `GET /auth/me` (o boot do dashboard já chama), sem endpoint novo. Front: `client.ts` lê o cookie e manda o header. Testes: `AntiforgeryCookieMiddlewareTests` 17 (sem token nega, token válido passa, token de outro usuário nega, cookie-sozinho/header-sozinho negam, GET/anônimo passam, round trip de 2 requests como o navegador) + `AntiforgeryRegistrationTests` 7 na arquitetura (lê Program.cs e exige registro/ordem após UseAuthentication, e os mesmos literais no client.ts) + 2 no GlobalExceptionHandler + 1 em /auth/me. Pendência declarada no ADR: sem WebApplicationFactory no repo, não há prova e2e pelo pipeline HTTP completo -->
- [x] F1-T016(b) Teto de sessão + verificação server-side <!-- ADR-018 (implementado 2026-09-29): claim sid no cookie, AbsoluteExpiresAt 12h, ActiveSessionMiddleware 401+cookie apagado, cache 30s; escopo original (b) + parte de "sessão expirada derruba o cookie" do critério de aceite cobertos -->
- [x] F1-T016a MFA TOTP <!-- ADR-019 (concluído 2026-09-29): Otp.NET RFC 6238 (6 dígitos/30s, tolerância ±1 passo via Mfa:ToleranceSteps), secret AES-GCM via ICredentialProtector (chave F11-T006), tabela user_mfa (1:1, enabled=false até confirm, recovery_codes JSON hash SHA-256 uso único), comandos Setup/Confirm/Disable/Status auto-serviço (ICurrentUserContext), LoginAsync retorna LoginFailure (MFA on + sem código → "Código MFA obrigatório." sem sessão; inválido → "Código MFA inválido."; recovery code válida marca usedAt), 10 recovery codes XXXX-XXXX devolvidos UMA vez no confirm; migração AddUserMfa (arrasta também o drift AbsoluteExpiresAt da ADR-018, que nunca tinha migrado); testes UserMfaTests(8)+MfaEnrollmentTests(10)+MfaLoginTests(6) → unit 1165 + arch 82 verdes; integração bloqueada nesta máquina (sem Docker) -->
- [x] F1-T016b Sessões <!-- ADR-020 (concluído 2026-09-29): ISessionService + SessionsController (GET lista com isCurrent, DELETE {sessionId}, DELETE others; inexistente/alheia = MESMA "Sessão não encontrada."), revogação = banco + check Revoked no cache distribuído (ActiveSessionMiddleware trocou IMemoryCache por ICacheService — reversão documentada da ADR-018; janela imediata entre instâncias; logout e troca/reset de senha também invalidam), SessionLifetimeOptions movida para Application (IdentityAuthenticationService lê as options — fim das constantes 30d/12h, drift da ADR-018), RoleAbsoluteLifetimes (cap por role só encurta o teto, aplicado no login, validado na startup), LastSeenAt com touch 5min no ActiveSessionReader; testes SessionManagementTests(8) + ActiveSessionReaderTests(7) + ActiveSessionMiddlewareTests (ICacheService) → unit 1181 + arch 84 verdes; integração bloqueada nesta máquina (sem Docker) -->
- [x] F1-T016c Auditoria <!-- MFA ativado/desativado, sessão revogada, login com segundo fator no audit existente -->
- [x] F1-T016d UI <!-- concluído 2026-09-29: LoginPage em 2 passos — o 401 "Código MFA obrigatório." (contrato mensagem→UI, ADR-019; constantes MFA_REQUIRED_MESSAGE/isMfaRequired em services/api/auth.ts) troca para campo autoComplete=one-time-code aceitando TOTP ou recovery code NO MESMO campo, senha mantida só no estado local para o retry e limpa no sucesso/falha do passo1, Voltar + Google/esqueceu escondidos no passo2, código vazio reaproveita a mensagem do backend; login e google agora mandam anonymousExpected (a recusa não dispara o interceptor de signedOut). SessionsPage nova em /sessions (nav "Sessões"): lista real de GET /auth/sessions (badge "Este dispositivo" em isCurrent, criada, última atividade, expira + teto absoluto ADR-018), revogar por linha com confirm, "Sair de todos os outros" desabilitado sem outros, revogar a atual = cookie morto no backend + signedOut→/login na hora; 1ª página com API real (demais seguem mock F14); DELETE com antiforgery do client (ADR-017). Validação: tsc -b + vite build verdes (achado: node_modules sem redux-persist e @react-oauth/google — declarados no package.json, build já falhava antes; npm install restaurou, lockfile intocado); backend intocado -->
- [x] F1-T017 Envio de convite por e-mail <!-- decisão do usuário 2026-09-29: caminho DIRETO espelho do forgot-password (não o Outbox→Notification do docs/05 — diagrama atualizado): IIdentityEmailSender.SendInvitationAsync(email, acceptUrl) no CreateInvitationCommandHandler após o save; adaptador LoggingIdentityEmailSender (provider real = pendência separada, exige SMTP/API + credenciais); CreateInvitationCommand + BaseUrl? opcional (5º parâmetro) e InvitationsController deriva Request.Scheme://Request.Host (igual forgot-password, sem mexer no front); link {origin}/api/v1/auth/invitations/{id}/accept?token= com token URL-safe base64url; falha de envio com TransactionBehavior desfaz a linha (admin recria sem 409 pendente); token continua na resposta (copy-link); testes: 2 novos em InvitationTests (RecordingEmailSender) RED(CS1739)→GREEN + 6 fakes existentes ganharam o método + harnesses InvitationTests/InvitationActivationTests registraram a porta; pendências: provider real de e-mail, página de aceite no frontend -->
<!-- decisão do usuário 2026-09-28: (a..e) acima; dividida em 4 sub-tasks em 2026-09-29, (b) concluída via ADR-018 -->

## Fase 2 — Inventário

- [x] F2-T001 Location
- [x] F2-T002 Rack
- [x] F2-T003 Device  <!-- tipos Server/Workstation/Switch/Router/Firewall/AccessPoint/Printer/Camera/Phone/UPS/Other; campos ip/mac/serial/model/vendor/vlanId/locationId/rackId; nome único global case-insensitive; DeleteRack com devices → 409; FK Restrict location/rack; migração AddDevices + índice ux_devices_name; seed fictício docs/database/seed-fictitious-devices.sql (RFC 5737, sem dados reais); policies InventoryRead/InventoryManage; BaseCommand/IQuery/IAuthorizedRequest -->
- [x] F2-T004 NetworkInterface
- [x] F2-T005 MAC  <!-- entidade MacAddress ancorada em NetworkInterface (decisão do usuário); MAC único GLOBAL case-insensitive (pré-check 409 + índice LOWER(mac)); normalização aa:bb:cc:dd:ee:ff aceitando :, -, . ou 12 hex; DeleteNetworkInterface com MACs → 409 (fecha cadeia Device→Interface→MAC); migração AddMacAddresses; ValueObject órfão Domain/ValueObjects/MacAddress.cs permanece sem consumidores (fora de escopo) -->
- [x] F2-T006 IP  <!-- entidade IpAddress ancorada em NetworkInterface (docs/03); IP único GLOBAL (usuário) com normalização canônica v4/v6 (pré-check 409 + índice UNIQUE LINQ na migração AddIpAddresses); DeleteNetworkInterface com IPs → 409 fecha dependência F2-T004; busca por interfaceId/deviceId(subquery)/termo; IpAddressesController em api/v1/inventory/ip-addresses; ValueObject órfão Domain/ValueObjects/IpAddress.cs permanece sem consumidores -->
- [x] F2-T007 Busca global  <!-- decisão do usuário: 6 fontes (Location/Rack/Device/NetworkInterface/MacAddress/IpAddress), só backend; GET api/v1/inventory/search?term&limit com InventoryRead; GlobalSearchQuery (IQuery+IAuthorizedRequest) fan-out sobre os SearchAsync existentes — sem SQL/migração; termo vazio → 400 "O termo de busca é obrigatório."; limit vale por fonte (clamp 1..50) e TotalCount reflete o total real; ordem fixa location→rack→device→interface→mac→ip; Subtitle = IP do device; barra de busca no frontend vira task F14 futura -->
- [x] F2-T008 DeviceProfile  <!-- ADR-014 Accepted; DeviceProfileDefaults por tipo em código (Printer→Ping+SnmpToner+SnmpDrum, AP→Ping+HttpWan, Switch/Server/Router/Firewall/UPS→Ping+SnmpSystem, demais→Ping); materializado no Device.Create com ProfileIsCustom=false; ApplyCustomProfile/ResetProfileToDefault (vazio/duplicado → 409/400 pt-BR); Update preserva override na troca de tipo; UpdateDeviceProfileCommand (checks XOR useDefault); PUT /devices/{id}/profile; colunas profile_is_custom + monitoring_checks (CSV) + migração AddDeviceProfile; EF ValueConverter List<CheckKind>→string; tests DeviceProfileTests + DeviceProfileCommandTests; 6 slices na regra de arquitetura -->

## Fase 3 — Network

- [x] F3-T001 Switch  <!-- extensão de Device tipo Switch; REUSO do fluxo F2-T003 sem entidade nova (docs/03: Switch(Device) 1:N SwitchPort); SearchSwitchesQuery delega ao SearchDevicesQueryHandler com type=Switch fixo (termo/local/rack passam direto); GetSwitchQuery guarda DeviceType.Switch → 404 "Switch '{id}' não encontrado."; DTO DeviceDto e mapper ToDto reaproveitados; GET api/v1/network/switches + /{id} (InventoryRead); namespace Network/Switches aberto para F3-T002 ports; sem migração -->
- [x] F3-T002 SwitchPort  <!-- entidade SwitchPort (DeviceId FK p/ Device, Name único por switch case-insensitive, Description, LinkUp bool? null=ainda não observado p/ F4); slice Application.Network.SwitchPorts (Commands/Queries/Ports/DTO) com guard REUSADO do F3-T001 via GetSwitchQueryHandler (404 "Switch '{id}' não encontrado."); 409 criação "Já existe uma porta '{nome}' neste switch." + DeleteDevice 409 "Não é possível excluir o device: existem portas vinculadas." (portas ANTES das interfaces, HasPortsAsync); rotas GET/POST api/v1/network/switches/{switchId}/ports + GET/PUT/DELETE api/v1/network/switch-ports/{id}; migração 20260925161824_AddSwitchPorts com índice bruto ux_switch_ports_device_name (device_id, LOWER(name)); 26 testes novos (8 domínio + 17 comandos + 1 delete) → 339 unit + 8 arch -->
- [x] F3-T003 VLAN  <!-- entidade Vlan (Name ≤120 único global CI, VlanNumber 1..4094 único global, Description ≤500); slice Network.Vlans (Create/Update/Delete/Search/Get); 409 "Já existe uma VLAN com o número '{n}'." e "com o nome '{nome}'."; ordenação da search por VlanNumber (ordem 802.1Q); rotas api/v1/network/vlans; migração 20260925175849_AddVlans + índice bruto ux_vlans_name; IVlanRepository registrado AUTOMATICAMENTE pelo F0-T024 (zero linhas de DI); N:N SwitchPortVlan = F3-T004; devices.vlan_id (Guid?) segue sem FK (fora de escopo) -->
- [x] F3-T004 SwitchPortVlan  <!-- junção PURA decide o usuário (sem flag tagged/untagged nesta fase — modelar acesso/trunk fica p/ F4/F11); entidade SwitchPortVlan (SwitchPortId+VlanId, par único); slice Network.SwitchPortVlans: Assign (guards reusados F3-T002/F3-T003 → 404; 409 "A porta já está vinculada a esta VLAN.") e Unassign (404 do vínculo); queries VLANs da porta (ordem 802.1Q, reusa VlanDto) e portas da VLAN (ordem nome, reusa SwitchPortDto) com novos GetByIdsAsync em IVlanRepository/ISwitchPortRepository; DeleteSwitchPort/DeleteVlan ganharam checagem → 409 "existem VLANs/portas vinculadas."; rotas GET/POST switch-ports/{id}/vlans + DELETE .../vlans/{vlanId} + GET vlans/{id}/ports; migração 20260925193218_AddSwitchPortVlans (2 FKs Restrict + índice único do par, 100% LINQ); IVlanRepository auto-registrado pelo F0-T024 -->
- [x] F3-T005 Connection (CRUD)  <!-- decisões: par DIRIGIDO (a,b)≠(b,a), ÚNICA restrição self-loop → 400, mesmo device permitido; entidade Connection (Source/Target FK Restrict, índice único do par) + slice Network.Connections (Create 201 InventoryManage com guards reusados F2-T004 → 404 pt-BR e duplicado → 409 "Já existe uma conexão entre essas interfaces."; Delete 404 "Conexão '{id}' não encontrada."; Search PagedResult com join das pontas, filtro DeviceId em qualquer lado + Term nos nomes das interfaces, ordena origem/destino; Get 404); DeleteNetworkInterface ganhou checagem 409 "existem conexões vinculadas"; rota api/v1/network/connections (GET/POST/GET {id}/DELETE {id}); migração AddConnections; docs/03 seção Connection; 19 unit (5 domínio + 14 command) -->
- [x] F3-T005b Connection (ingestão LLDP/CDP)  <!-- decisões: walk ifName local (IF-MIB 1.3.6.1.2.1.31.1.1.1.1, IfNameLookup compartilhado tolerante a falha) → atributo local_if_name nos providers LLDP/CDP (IfNameTableOid público) + gatilho AUTOMÁTICO no fim do StartDiscoveryCommand (candidatos = achados Neighbor Succeeded com DeviceId via F4-T010, mesmo SaveChanges do run, erro de ingestão não derruba o run); NeighborConnectionImporter: local = (device do run, local_if_name) + vizinho = (DeviceId do achado, port_desc??port_id??device_port) → NetworkInterface por NOME → Connection dirigida; self-loop/ponta ausente/dedupe par → pula, erro por candidato → Warning+continua; DI INeighborConnectionImporter (produção + BaseIntegrationTest); docs/09 seção Ingestão de Connections + docs/03 atualizado; 8 unit novos (2 LLDP + 2 CDP + 4 StartDiscovery) -->
- [x] F3-T006 Topology  <!-- decisões do usuário: SÓ BACKEND (consumo no frontend é task F13/F14), nodes = INTERFACES (incluindo as isoladas — nenhum device some do mapa), edges = Connections do F3-T005, status = "up"/"down"/null do ÚLTIMO Ping do histórico por device (herdado pelo node), sem DiscoverTopology (coberto por F4 discovery + F3-T005b); slice Network.Topology (GetTopologyQuery InventoryRead → TopologyDto{Nodes,Edges}; TopologyNodeDto{Id,DeviceId,Name,DeviceName,DeviceType,Ip,Status}, TopologyEdgeDto{Id,SourceInterfaceId,TargetInterfaceId}); ITopologyRepository.GetGraphAsync (interface⋈device ordenado por device/nome + Connections + GroupBy DeviceId do último MonitoringResult Kind=Ping via CreateDateTime, EF Core traduz GroupBy→First); rota GET /api/v1/network/topology (TopologyController); 6 unit novos (grafo com conexão, node isolado, status up/down/null via seed SQL, vazio, ordenação, permissions); frontend /topology continua com dados fake (task separada) -->

## Fase 4 — Discovery

- [x] F4-T001 DiscoveryRun — entidade + enum (Running/Completed/Failed), migração AddDiscoveryRuns (FK Cascade device), API somente-leitura GET runs (filtro device) + GET runs/{id} (policy MonitoringRead); fluxo: gatilho manual por device, POST vem com providers T003/T004; 13 testes
- [x] F4-T002 DiscoveryResult  <!-- modelo aprovado pelo usuário: Kind + Summary + JSON; escopo leitura apenas. Entidade + enum DiscoveryResultKind (Neighbor|ArpEntry|MacTableEntry|DhcpLease|Host), tabela discovery_results (FK run cascade, device opcional SetNull p/ T010, values_json no padrão MonitoringResult), migração AddDiscoveryResults, repositório/paginação CreatedAt DESC, queries Search/Get (MonitoringRead; run desconhecido → 404 pt-BR), endpoints GET runs/{id}/results + results/{id}, registro dos dois repositórios no BaseIntegrationTest, docs/09 seção do modelo; 12 unit + 3 integration -->
- [x] F4-T003 ICMP Provider  <!-- escopo aprovado: POST incluído + ping falho = achado negativo. IcmpDiscoveryProvider (Name ICMP, seam IIcmpClient, Attributes reachable/roundtrip_ms, Summary pt-BR opcional no DiscoveryProviderResult), StartDiscoveryCommand (MonitoringManage, AuditResource DiscoveryRun) valida device→provider(400 pt-BR via catch do registry)→IP, cria run, probe, grava DiscoveryResult(Host, DeviceId=device), Complete, 1 SaveChanges; exceção do probe → run.Fail(truncado 500) e DTO Failed sem exceção; POST api/v1/discovery/devices/{id}/runs (Admin,User) + StartDiscoveryViewModel; DI IcmpDiscoveryProvider (produção+testes); BUG corrigido: controllers de discovery usavam Policy="MonitoringRead" inexistente (fora do ADR-008) → Roles Admin,User,Viewer; docs/09 seção do provider; 17 unit + 3 integration -->
- [x] F4-T004 SNMP Provider  <!-- escopo aprovado: só system group MIB-II (sem ifTable). SnmpDiscoveryProvider: walk 1.3.6.1.2.1.1 via ISnmpClient (seam do monitoring, sem rede), atributos sys_descr/sys_up_time/sys_contact/sys_name/sys_location + reachable, Summary "SNMP: <sys_name> — <sys_descr>" truncado em 500 (constante DiscoveryResult); sem resposta/ exceção → achado negativo pt-BR (mesma decisão do ICMP, run Completed pelo comando); StartDiscoveryCommand resolve "Snmp" pelo registry sem mudança (kind Host cobre ICMP+SNMP; T005+ estende o mapeamento); DI SnmpDiscoveryProvider (produção) + FakeSnmpClient/SnmpDiscoveryProvider no BaseIntegrationTest; docs/09 seção do provider; 12 unit + 1 integration -->
- [x] F4-T005 LLDP  <!-- decisões: SNMP/LLDP-MIB (não SSH) + walk vazio registra "nenhum". Contrato evoluído: DiscoverAsync → lista de DiscoveryProviderResult(Kind, Succeeded, Attributes, Error?, Summary?) — ICMP/SNMP 1 achado Host, LLDP N achados Neighbor; comando grava todos em 1 SaveChanges e só preenche DeviceId em Host (T010 casa vizinhos depois). LldpDiscoveryProvider: walk 1.0.8802.1.1.2.1.4.1.1 (lldpRemTable), colunas 2–8 agrupadas por timeMark.localPort.remIndex, Summary "<sys_name> — <porta>" (≤500), vazio → "Nenhum vizinho LLDP." (neighbors=0), exceção → indisponível pt-BR; DI Lldp (produção+testes); FakeSnmpClient settable p/ integração; docs/09 seção Provider LLDP; 14 unit + 1 integration -->
- [x] F4-T006 CDP  <!-- mesmo padrão do LLDP: CdpDiscoveryProvider walk cdpCacheTable (1.3.6.1.4.1.9.9.23.1.2.1.1), colunas .4 address/.5 version/.6 device_id/.7 device_port (OIDs confirmados via OpenNMS/MIB) agrupadas por ifIndex.deviceIndex, Summary "<device_id> — <porta>" ≤500, vazio → "Nenhum vizinho CDP.", exceção → indisponível pt-BR; Kind Neighbor sem vínculo DeviceId (T010); DI CdpDiscoveryProvider (produção+testes); docs/09 seção Provider CDP; 12 unit (provider) + 1 unit (comando) -->
- [x] F4-T007 ARP  <!-- decisões: 1 achado por entrada + sem filtro de tipo. ArpDiscoveryProvider: walk ipNetToMediaTable (IP-MIB 1.3.6.1.2.1.4.22.1), colunas .1 ifIndex/.2 physAddress/.3 netAddress/.4 type agrupadas por ifIndex.a.b.c.d, 1 achado Kind=ArpEntry por entrada Summary "<ip> — <mac>" ≤500 (entrada sem netAddress é descartada), vazia → "Nenhuma entrada ARP." (arp_entries=0), exceção → indisponível pt-BR; DI ArpDiscoveryProvider (produção+testes); docs/09 seção Provider ARP; 12 unit (provider) + 1 unit (comando) -->
- [x] F4-T008 DHCP  <!-- escopo aprovado: bindings de DHCP snooping (vendor Cisco). DhcpDiscoveryProvider: walk cdsBindingsTable CISCO-DHCP-SNOOPING-MIB 1.3.6.1.4.1.9.9.380.1.4.1, colunas .1 vlan/.2 mac/.3 addrType/.4 ip/.5 interface/.6 leasedTime/.7 status/.8 hostname agrupadas por índice vlan+mac (octetos), 1 achado Kind=DhcpLease por binding Summary "<ip> — <mac>" ≤500 (binding sem ip descartado), vazia → "Nenhuma entrada DHCP." (dhcp_bindings=0), exceção → indisponível pt-BR; DI DhcpDiscoveryProvider (produção+testes); docs/09 seção Provider DHCP; 8 unit (provider) + 1 unit (comando) -->
- [x] F4-T009 MAC Tables  <!-- decisões: filtro só status=3 (learned) + chave "Mac". MacTableDiscoveryProvider: walk dot1dTpFdbTable (BRIDGE-MIB RFC 4188 1.3.6.1.2.1.17.4.3), colunas .1 address/.2 port/.3 status agrupadas por índice MAC (6 octetos), 1 achado Kind=MacTableEntry por entrada learned Summary "<mac> — porta <port>" ≤500 (self/invalid/other descartados), vazia ou só não-learned → "Nenhuma entrada MAC." (mac_entries=0), exceção → indisponível pt-BR; DI MacTableDiscoveryProvider (produção+testes); docs/09 seção Provider MAC Tables; 12 unit (provider) + 1 unit (comando) -->
- [x] F4-T010 New Device Detection  <!-- decisões: vincular + flag (sem criar device) + chaves ip>mac>nome. StartDiscoveryCommandHandler casa achados Succeeded não-Host: Values.ip→GetByIpAsync, Values.mac→GetByMacAsync (sem separador/caixa), Values.sys_name/device_id/hostname→GetByNameAsync — prioridade ip>mac>nome, cache por chave no handler; casado → DiscoveryResult.DeviceId; sem match → Values["new_device"]="true"; achado negativo sem vínculo e sem flag; Host inalterado. IDeviceRepository + DeviceRepository com GetByIpAsync/GetByMacAsync novos; docs/09 seção New Device Detection; 6 unit (comando) -->

## Fase 5 — Monitoring

- [x] F5-T001 MonitoringCheck  <!-- instância por device; nasce do DeviceProfile (F2-T008/ADR-014 §4); entity MonitoringCheck (DeviceId, Kind string, Enabled) com índice único (device_id, kind) e FK cascade (delete do device remove rows); materialização em CreateDeviceCommandHandler e sincronização em UpdateDeviceProfileCommandHandler via IMonitoringCheckRepository.SynchronizeFromProfileAsync (adiciona faltantes Enabled=true, remove fora do perfil, preserva existentes — mesmo SaveChanges); GetDeviceChecksQuery (MonitoringRead, IQuery+IAuthorizedRequest, 404 pt-BR) em GET /monitoring/checks/{id}; ordenação por Kind em memória (coluna string ordenaria alfabeticamente); migração AddMonitoringChecks -->
- [x] F5-T002 MonitoringResult  <!-- histórico de polls; entity MonitoringResult (DeviceId FK cascade p/ Device, Kind, Succeeded, DurationMs, Error≤500, Values JSON opcional, CheckedAt=CreateDateTime) — FK no device e não no MonitoringCheck de propósito: histórico sobrevive a mudanças de perfil; escrita on-demand no RunCheckCommandHandler (docs/21 MON-006/007 — 1 linha por execução em device com IP; sem IP não grava; Values p/ checks SNMP); GetDeviceResultsQuery (MonitoringRead, limit 1..500 default 50, ordem CheckedAt desc, 404 pt-BR) em GET /monitoring/results/{id}; migração AddMonitoringResults com índice (device_id, CreateDateTime); SaveChangesAsync sobrescreve CreateDateTime (auditoria) — seeds testam via SQL -->
- [x] F5-T003 Ping  <!-- up/down sob demanda estilo Zabbix; PingMonitoringProvider (chave "Ping", timeout 2s) com seam IIcmpClient/SystemNetIcmpClient; execução via RunCheckCommand/RunAllChecksCommand (docs/21 MON-006/007, ADR-006) em POST /monitoring/run-check/{id}?check=Ping + POST /monitoring/run-check?check=Ping; device sem IP → Executed=false "Device sem IP cadastrado."; queries GET de execução removidas (GET fica só p/ leitura: checks/results); DI IIcmpClient+IMonitoringProvider; testes em RunCheckCommandTests/RunAllChecksCommandTests (provider falso/ICMP falso + Sqlite); histórico via F5-T002; sem worker (F5-T007) -->
- [x] F5-T004 SNMP  <!-- Printer MIB: toner e drum/cilindro % sob demanda; SnmpTonerProvider/SnmpDrumProvider (keys CheckKind.SnmpToner/Drum) com base PrinterSuppliesProvider — walk prtMarkerSuppliesTable OID 1.3.6.1.2.1.43.11.1.1 (colunas 6=desc, 8=max, 9=level), match por descrição (toner/ink/tinta; drum/cilindro/imaging), pior carretel em % via Values (levelPercent/description); seam ISnmpClient + SharpSnmpSnmpClient (Lextm.SharpSnmpLib 12.5.7, SNMPv2c, community configurável Monitoring:Snmp default public); MonitoringProviderResult ganhou Values opcional (retrocompatível); execução via RunCheckCommand (POST /monitoring/run-check/{id}?check=SnmpToner|SnmpDrum; check SNMP em non-Printer → 400 "só se aplica a devices do tipo Printer"; GET /monitoring/printers/{id}/supplies removido — histórico via GET /results com Values); credencial por device → F11 -->
- [x] F5-T005 HTTP/HTTPS  <!-- decisões: HTTPS+fallback HTTP, qualquer resposta = sucesso, validar cert TLS. HttpWanMonitoringProvider (key CheckKind.HttpWan, timeout 5s): sonda https://{ip}/ (IPv6 com colchetes) via IHttpProbeClient; resposta HTTP → Values url/scheme/status_code; falha → fallback http://{ip}/; ambos falham → "HTTP indisponível em {ip}." com Values https_error/http_error; exceção seam → "Falha ao consultar HTTP:" sem fallback; vazio → "Destino do HTTP é obrigatório.". Seam IHttpProbeClient+HttpProbeReply(Success,StatusCode,Error?) + SystemNetHttpProbeClient (HttpClient estático, ResponseHeadersRead, validação TLS padrão, timeout via CancellationToken, erro de rede → reply pt-BR "Tempo esgotado."); DI ambos (produção) + FakeHttpProbeClient (integração); docs/09 seção Check HTTP/HTTPS; 8 unit (provider) + 1 unit (comando) -->
- [x] MON-009 IntegrationTest  <!-- docs/21: testes de integração de monitoring em tests/InfraControl.IntegrationTests/Monitoring/; BaseIntegrationTest com PostgreSQL real via Testcontainers (imagem local postgres:17-alpine, sem pull), pipeline MediatR do aplicativo (CorrelationId/Logging/Performance/Validation/Audit/Transaction/Authorization behaviors) e repositórios Infrastructure reais; permissões vêm de RolePermissions (role → Permission → claim string) via TestCurrentUserContext; ICMP falso (sem rede); 4 testes: run-check com role Viewer persiste e lê de volta via GetDeviceResultsQuery, sem MonitoringRead → UnauthorizedAccessException pt-BR, sem IP não persiste, RunAllChecks só grava devices com IP -->
- [x] F5-T006 TCP  <!-- escopo aprovado: porta fixa 37777 + default Camera ganha Tcp (ADR-014). CheckKind.Tcp=5; TcpMonitoringProvider (key Tcp, porta 37777, timeout 2s): conecta {ip}:37777 via ITcpProbeClient; handshake → Values port; recusa/timeout → "Porta 37777 inacessível em {ip}." + Values tcp_error; exceção → "Falha ao consultar TCP:"; vazio → "Destino do TCP é obrigatório.". Seam ITcpProbeClient+TcpProbeReply(Success,Error?) + SystemNetTcpProbeClient (TcpClient.ConnectAsync, timeout CancellationToken); DeviceProfileDefaults Camera = [Ping, Tcp] (2 testes de perfil atualizados); DI produção + FakeTcpProbeClient (integração); docs/09 seção Check TCP; 11 unit (provider) + 1 unit (comando) -->
- [x] F5-T007 Worker  <!-- decisões: serviço + worker fino (padrão ICleanupService) + honrar checks habilitados; config MonitoringPolling:Enabled=true/IntervalSeconds=60. MonitoringPollingService (Application): devices com IP + IMonitoringCheckRepository.GetEnabledAsync (novo) → ExecuteAsync por device/check (guard impressora/provider ausente → Warning+pula; erro inesperado → Error+continua) → 1 SaveChanges/ciclo → retorna nº executado; MonitoringPollingWorker (Workers, BackgroundService) agenda via scope+config, log de contagem; AddHostedService + seção appsettings; docs/09 seção Polling periódico; 6 unit (serviço) -->
- [x] F5-T008 Recovery  <!-- coberta por F6-T001: o AUTO-RESOLVE aprovado é exatamente "volta a responder → fecha alerta" — Ping ok/toner≥mínimo/drum≥mínimo resolvem o Alert Open no pós-check (testes PingUp_ShouldResolveOpenAlert, TonerAtThreshold_ShouldResolveOpenAlert, DrumAtThreshold_ShouldResolveOpenAlert, integração OpenAlert_ShouldResolveAfterSuccessfulPingAndReadBackViaQuery); sem código novo; evento MonitoringCheckRecovered fica p/ F9 -->

## Fase 6 — Alerts/Incidents

- [x] F6-T001 Alert  <!-- decisões do usuário: thresholds FIXOS em código (AlertThresholds: toner<20%, drum<10%), AUTO-RESOLVE no próximo check, avaliação PÓS-CHECK no RunCheckCommandHandler.ExecuteAsync (compartilhado por RunCheck/RunAll/polling F5-T007); entidade Alert (DeviceId, Kind, Severity Warning|Critical, Status Open|Resolved, Message pt-BR, ResolvedAt?) + enums AlertSeverity/AlertStatus + migração AddAlerts; 1 alerta Open por (device, check) anti alert-storm (unicidade lógica no AlertEvaluator, histórico preservado); regras: Ping falho→Critical abre, ok→resolve, toner/drum abaixo do mínimo→Warning abre, ≥→resolve, sem Values (erro SNMP)→não avalia, kinds sem regra→não avalia; falha do avaliador→Warning log, check não falha; AlertEvaluator (Application/Alerts) + IAlertRepository (GetOpenAsync tracking p/ Resolve, SearchAsync join device); SearchAlertsQuery (MonitoringRead, status open|resolved inválido→400 pt-BR, PagedResult, mais recente primeiro); rota GET /api/v1/monitoring/alerts (Roles Admin,User,Viewer — padrão do controller, sem POST público); DI produção + BaseIntegrationTest (IAlertRepository + IAlertEvaluator); tests AlertTests (4 domínio) + AlertEvaluatorTests (11) + RunCheck fluxo (3) + integração AlertIntegrationTests (4: resolve via ping fake, filtro status, permissão, status inválido) + ajuste de construtores em 3 arquivos de teste; bug de namespace: testes de domínio vão em Alerts/ (UnitTests.Domain conflita com InfraControl.Domain); docs/03 seção Alert + docs/09 seção F6-T001 -->
- [x] F6-T002 Alert Rules  <!-- decisões do usuário: Ping N=3 via histórico (streak determinístico lido do banco, sem estado em memória — sobrevive restart), upstream DEIXADO P/ F6-T004 Correlation, anti-storm SÓ O BÁSICO (unicidade Open + streak); AlertThresholds.PingFailureStreak=3; alerta de down abre só com 3 falhas consecutivas (N-1=2 do histórico GetRecentByDeviceAndKindAsync por device+kind + atual); sucesso salvo no meio quebra a sequência e resolve o Open; Toner/Drum continuam imediatos (estado, não falha); novo método IMonitoringResultRepository.GetRecentByDeviceAndKindAsync + decorator CountingResultRepository; mensagem do alerta de down agora cita "3 falhas consecutivas"; testes: +6 unit (streak abre na 3ª, não abre na 2ª, sucesso quebra, resolve com histórico, isolamento por device, fluxo RunCheck 3 execuções) e ajuste de 3 testes pré-existentes para N=3; docs/09 seção F6-T002 + tabela atualizada + docs/03 -->
- [x] F6-T003 Incident  <!-- decisões do usuário: AUTO-ABERTURA no avaliador (Alert Critical → Incident), 5 ESTADOS DA UI (Open/Investigating/Identified/Monitoring/Resolved, transições livres ≠ atual, Resolved estampa ResolvedAt e reopen limpa), VÍNCULO N:N COM ALERTS já no T003; entidade Incident (Title/Description?/Severity Low-Medium-High-Critical/Status/DeviceId?/ResolvedAt?) + IncidentAlert (PK composto, Id ignorado) + enums IncidentStatus/IncidentSeverity + migração AddIncidents; regra anti-storm: 1 Incident Open por device, Warning nunca abre, alerta órfão ganha vínculo na próxima avaliação; Acknowledge (docs/15) = →Investigating; comandos Create/Acknowledge/UpdateStatus (IncidentsManage, guards pt-BR 400/404) + queries Search/Get (IncidentsRead, filtros device/status, AlertCount no search, AlertIds no get); API POST/GET/{id}/acknowledge/PUT {id}/status em IncidentsController (policy IncidentsRead/Manage); ACHADO CORRIGIDO: policies HTTP nomeadas (InventoryRead etc.) NÃO estavam registradas — controllers das fases 2/3/5 falhariam em runtime; PermissionPolicies.AddPermissionPolicies() registra policy por Permission exigindo claim PermissionClaim.Type (docs/05 atualizado, PermissionPolicyTests com FrameworkReference no UnitTests); IIncidentRepository + IncidentRepository (join de alerts, contagem no search); DI produção + BaseIntegrationTest; testes: IncidentTests (7 domínio) + IncidentCommandTests (12) + AlertEvaluator auto-abertura (4) + PermissionPolicy (3) + integração IncidentIntegrationTests (3); docs/03 seção Incident + docs/09 seção F6-T003 -->
- [x] F6-T004 Correlation  <!-- decisões do usuário: SUPRESSÃO TOPOLOGICAL (usa Connections do F3-T005) e TODOS os kinds do downstream; gatilho = BFS não-dirigido (visited, por distância) dos vizinhos até device alcançável com INCIDENT OPEN (si-mesmo excluído; Warning não suprime); implementação UpstreamCorrelationRepository = grafo em memória (2 queries LINQ, sem CTE — Sqlite+PG); efeito: alerta nasce/vira AlertStatus.Suppressed (string(20), SEM migração) e é vinculado ao incidente raiz via incident_alerts (idempotente), downstream NÃO abre incidente próprio; normalização resolve Suppressed; upstream sem incidente → Reopen + próprio incidente se Critical; Open→Suppressed na próxima avaliação cobre a corrida de ciclo (incidente já aberto não é mesclado — F6-T006 fecha); domínio Alert.Suppress()/Reopen() (Resolved definitivo); IAlertRepository.GetActiveAsync (Open OU Suppressed) substitui GetOpenAsync; busca de alert aceita status suppressed (400 pt-BR); avaliador ganha 4ª dependência (DI por convenção I*Repository + BaseIntegrationTest); testes +22 unit (AlertTests 6, UpstreamCorrelationRepositoryTests 8, AlertEvaluatorTests 6, AlertQueriesTests 2) +3 integração (CorrelationIntegrationTests) — 805 unit + 51 integration + 8 arch = 864, 0 falhas; docs/03 (3 estados + regra) + docs/09 seção F6-T004 -->
- [x] F6-T005 Root Cause  <!-- causa provável derivada (decisão dentro do pedido "faz tudo sem pedir": heurística fixa em código, padrão dos thresholds); campo Incident.RootCause (string? 500, migração AddIncidentRootCause) + SetRootCause (trim/null limpa/>500 → 400 pt-BR/no-op) exposto no IncidentDto; IRootCauseResolver (RootCauseResolver): (1) BFS upstream com Incident Open → "Falha no upstream '{nome}'", (2) vizinho 1-hop com alerta Ping Open MAIS ANTIGO que o incidente → "Vizinho '{nome}' sem resposta — provável enlace ou switch intermediário" (Suppressed não conta — vítima; regra de tempo evita culpar vítimas), (3) senão falha local; incidente manual sem device → null; avaliador seta na criação + RefreshAsync ao final de TODO ciclo (cobre corrida F6-T004: upstream abre depois → causa atualiza no ciclo seguinte); IConnectionRepository.GetNeighborDeviceIdsAsync (grafo não-dirigido) + GetOpenByDeviceAsync com tracking; testes +15 (IncidentTests 4, RootCauseResolverTests 8 — datas via SQL pois auditoria sobrescreve CreateDateTime, avaliador 2, integração 1) — 819 unit + 52 integration + 8 arch = 879, 0 falhas; docs/03 + docs/09 seção F6-T005 -->
- [x] F6-T006 Recovery  <!-- fechamento automático do incidente quando os alerts resolvem (interpretado dos docs: docs/09 "fechamento do incidente quando os alerts resolvem"); gatilho no avaliador: ResolveActiveAsync (Ping ok/supply ≥mínimo resolve Open OU Suppressed) → CloseRecoveredIncidentsAsync — verifica Incident Open do device + incidentes Open vinculados ao alert resolvido (novo IIncidentRepository.GetOpenIdsLinkedToAlertAsync via join IncidentAlerts→Incidents); se TODOS os alerts vinculados estão Resolved → ChangeStatus(Resolved) estampa ResolvedAt; guardas: incidente sem vínculos (manual) não fecha (só comando), vínculo com alerta ainda ativo (downstream Suppressed) mantém aberto e fecha no último; AsNoTracking do GetAlertsAsync contornado comparando por Id com o alert resolvido em memória; reabertura = novo alert → novo incidente (anti-storm T003, fechado não é reusado); testes +4 (avaliador 3: fecha 1 vínculo, fica aberto com vínculo ativo + fecha no último, manual não fecha; integração 1: RunCheck ICMP fake → alert resolve → SearchIncidents Resolved) — 822 unit + 53 integration + 8 arch = 883, 0 falhas; FASE 6 COMPLETA (F6-T001→T006); docs/03 + docs/09 seção F6-T006 -->

## Fase 7 — Tasks

- [x] F7-T001 Task <!-- decisões (padrão CRUD = Locations F2-T001): entidade `TaskItem` (NÃO `Task` — evita CS0104 com System.Threading.Tasks.Task em todo o codebase async; documentado em docs/04), enums `TaskKind` (11 tipos de docs/04) e `TaskState` (7 estados — nome evita colisão com TaskStatus BCL), ambos persistidos como string(20) sem migração extra; nasce Open; Title 120 / Description 500 (padrão codebase, docs/04 não define tamanhos); state/kind mutáveis só via F7-T007 (ChangeState hoje no-op no mesmo estado); CRUD completo Create/Update/Delete/Search/Get em TasksController (policies HTTP TasksRead/TasksManage — registradas pelo fix do F6-T003) + ViewModels + parse pt-BR de kind/state (400); DI por convenção I*Repository (ITaskItemRepository/TaskItemRepository — sem linha manual) + registro no BaseIntegrationTest; Delete ainda sem pré-check de vínculos (F7-T002→T005 adicionam; FK Restrict é o backstop); migração AddTasks; testes +18 unit (TaskItemTests 6, TaskCommandTests 12) +4 integração (TaskIntegrationTests) -- 840 unit + 57 integration + 8 arch = 905, 0 falhas; docs/04 seção Task -->
- [x] F7-T002 TaskAssignee <!-- vínculo N:N Task↔User (padrão IncidentAlert): entidade `TaskAssignee` (PK composta task_id+user_id, Id unmapped), FKs **Cascade nas duas pontas** (decisão: apagar task/usuário limpa assignees — Delete da F7-T001 continua sem pré-check e sem 409 surpresa); `ITaskAssigneeRepository` (Exists/UserExists/Find/GetUserIds + GetUserIdsByTaskIds group join por página sem N+1; `GetByIdAsync` lança NotSupportedException — chave é o par); UserExists consulta **DomainUsers** (não o DbSet Users do Identity — achado no meio: AppDbContext.Users é InfraControlUser); comandos Assign/Unassign (TasksManage): 404 task/usuário pt-BR, 409 duplicidade, 404 vínculo; rotas `POST/DELETE api/v1/tasks/{taskId}/assignees/{userId}` (204); TaskDto ganha `AssigneeCount`+`AssigneeIds` (padrão IncidentDto AlertCount/AlertIds) enriquecidos no Get e no Search via group join; BaseIntegrationTest + `IDomainEventService`/`DomainEventService` (achado: seed de DomainUsers dispara UserCreatedDomainEvent → OutboxDispatchHandler falhava sem o serviço — grava só OutboxMessages, sem broker) + ITaskAssigneeRepository; migração AddTaskAssignees; testes +12 unit (TaskAssigneeTests 3, TaskAssignmentCommandTests 9) +3 integração (TaskAssigneeIntegrationTests) -- 852 unit + 60 integration + 8 arch = 920, 0 falhas; docs/04 seção assignees -->
- [x] F7-T003 TaskDevice <!-- vínculo N:N Task↔Device (padrão IncidentAlert): entidade `TaskDevice` (PK composta task_id+device_id, Id unmapped); FK Task **Cascade** (apagar task limpa vínculos) e FK Device **Restrict** (decisão: padrão do codebase — Ports/Interfaces também bloqueiam exclusão de device); `ITaskDeviceRepository` (Exists/Find/GetDeviceIds/GetDeviceIdsByTaskIds group join sem N+1; GetByIdAsync lança NotSupportedException); comandos Link/Unlink (TasksManage): 404 task/device/vínculo pt-BR, 409 duplicidade; rotas `POST/DELETE api/v1/tasks/{taskId}/devices/{deviceId}` (204); TaskDto ganha `DeviceCount`+`DeviceIds` (Get join, Search group join); `IDeviceRepository.HasTasksAsync` + pré-check no `DeleteDeviceCommand` ("existem tasks vinculadas" — FK Restrict = backstop); migração AddTaskDevices; testes +12 unit (TaskDeviceTests 3, TaskDeviceCommandTests 9 — inclui delete device bloqueado e enrich do Search) +3 integração — 864 unit + 63 integration + 8 arch = 935, 0 falhas; docs/04 seção Devices -->
- [x] F7-T004 TaskIncident <!-- vínculo N:N Task↔Incident (padrão IncidentAlert): entidade `TaskIncident` (PK composta task_id+incident_id, Id unmapped); FK Task **Cascade** (apagar task limpa vínculos), FK Incident **Restrict** (não existe exclusão de incidente no produto — FK é backstop); `ITaskIncidentRepository` (Exists/Find/GetIncidentIds/GetIncidentIdsByTaskIds group join sem N+1; GetByIdAsync lança NotSupportedException); comandos Link/Unlink (TasksManage): 404 task/incidente/vínculo pt-BR, 409 duplicidade; rotas `POST/DELETE api/v1/tasks/{taskId}/incidents/{incidentId}` (204); TaskDto ganha `IncidentCount`+`IncidentIds` (Get join, Search group join — simetria com assignees/devices); lookup do incidente via IIncidentRepository.GetByIdAsync; migração AddTaskIncidents; testes +11 unit (TaskIncidentTests 3, TaskIncidentCommandTests 8) +3 integração — 875 unit + 66 integration + 8 arch = 949, 0 falhas; docs/04 seção Incidentes -->
- [x] F7-T005 TaskComment <!-- comentários livres (decisão dentro do pedido: modelo simples, docs/04 só cita "comentários"): entidade `TaskComment` (Id próprio, TaskId, AuthorId, Content 500 — padrão do codebase; sem edição/deleção — histórico vem no T006); FK Task **Cascade** (apagar task apaga comentários), FK Author **Restrict** (preserva autoria; produto não tem exclusão de usuário); `ITaskCommentRepository` com `GetByTaskIdAsync` (ordem cronológica + **left join** em DomainUsers → AuthorName null se autor saiu); `AddTaskCommentCommand` (TasksManage): autor = `ICurrentUserContext.UserId` (null → 401 pt-BR, padrão SshSessionCommands), 404 task, 400 conteúdo; `GetTaskCommentsQuery` (TasksRead): 404 task inexistente (consistência com GetTask); rotas `GET/POST api/v1/tasks/{taskId}/comments` (POST 201); resposta traz o DisplayName do join (unit test faz o próprio AuthorName via DomainUsers); migração AddTaskComments; testes +14 unit (TaskCommentTests 5, TaskCommentCommandTests 9) +4 integração (autor logado real via CurrentUser.UserId = user seedado — FK Restrict) — 889 unit + 70 integration + 8 arch = 967, 0 falhas; docs/04 seção Comentários -->
- [x] F7-T006 TaskHistory <!-- histórico de mudanças (heurística: docs/04 só cita "histórico"): entidade `TaskHistory` (TaskId, ActorId nullable = sistema, Action enum string(40), Detail 200; FK Task Cascade, **sem FK p/ actor** — auditoria, ator pode não existir mais); ações: Created, Updated, Assigned, Unassigned, DeviceLinked/Unlinked, IncidentLinked/Unlinked, Commented (StateChanged = F7-T007); gravação via `ITaskHistoryWriter` (actor = ICurrentUserContext capturado AQUI para não poluir os 9 handlers) injetado em Create/Update/Assign/Unassign/Link/Unlink×2/AddComment (Delete não grava — task some com cascade); leitura `GET api/v1/tasks/{taskId}/history` (TasksRead, 404 task) cronológica com ActorName (left join DomainUsers); DI manual do writer (não termina em Repository) + ITaskHistoryRepository (auto) — hunk seletivo no DependencyInjection.cs (arquivo compartilhado com a sessão paralela F1-T014, git apply --cached); migração AddTaskHistories; testes +10 unit (TaskHistoryTests 5, TaskHistoryCommandTests 5 — datas distintas via SQL pois auditoria sobrescreve CreateDateTime) +3 integração — Tasks 77 unit + 20 integration; repo completo 918+73+8=999 (inclui testes da sessão paralela), 0 falhas; **REPARO DE MIGRAÇÕES**: as 6 migrações F7 (AddTasks→AddTaskComments + AddTaskHistories) foram geradas com `--no-build` contra assembly STALE e contentavam erradamente `AddColumn RootCause` (duplicando o AddIncidentRootCause) com snapshot congelado no F6; corrigido: as 5 antigas viraram no-op comentado, a nova `AddTaskHistories (20260928001322)` cria as 6 tabelas (validado por `migrations script`: RootCause único + 6 CREATE TABLE), snapshot atualizado; **regra: NUNCA usar `--no-build` no dotnet ef**; docs/04 seção Histórico -->
- [x] F7-T007 Task Workflow <!-- workflow (heurística: docs/04 só cita "histórico"/estados — mapa ITSM decidido e documentado): mapa `TaskStateWorkflow` (Domain/Enums) com transições: Open→InProgress|Cancelled; InProgress→Waiting|Blocked|Resolved|Cancelled; Waiting→InProgress|Blocked|Cancelled; Blocked→InProgress|Cancelled; Resolved→Closed|InProgress; Cancelled→Open; Closed→Open (reativação); mesmo estado = no-op sem histórico. `TaskItem.ChangeState` valida e lança `InvalidOperationException` pt-BR ("Transição inválida: 'X' → 'Y'." → 409 pelo GlobalExceptionHandler); enum `TaskHistoryAction` ganha `StateChanged`; comando `ChangeTaskStateCommand` (TasksManage) em `TaskStateCommands.cs`: 404 task, 400 estado desconhecido/vazio (reusa `ParseState` do Queries), grava histórico com detail `"De → Para"`, devolve TaskDto completo (group joins dos 3 repositórios, padrão GetTaskQuery); rota `PUT /api/v1/tasks/{taskId}/state` (`ChangeTaskStateViewModel`); seeds de teste seguem o próprio mapa (sem bypass — sem InternalsVisibleTo). Sem migração (só enum string). Testes novos: +19 unit (TaskWorkflowTests 14 — mapa completo + lifecycle + reativação; TaskStateCommandTests 5 — 409/404/400/histórico) +6 integração (TaskStateIntegrationTests: ciclo Open→Closed com histórico, no-op, 409/400/403/404) — Tasks 96 unit + 26 integration; repo **937 unit + 79 integration + 8 arch = 1024**, 0 falhas; docs/04 seção Workflow -->

## Fase 8 — SLA/SLO

- [x] F8-T001 Service <!-- CRUD (heurística: docs/04 só lista Service entre as entidades do SLO — decidido dentro do pedido "sem pedir"): entidade `Service` (AggregateRoot, padrão Location) Name 120 obrigatório único case-insensitive (pré-check + migração `ux_services_name ON services (LOWER(name))` SQL bruto) + Description 500 opcional; tabela `services`. Permissões novas **`SlaRead`/`SlaManage`** (módulo inteiro Fase 8, SLA+SLO; Viewer +SlaRead, User +SlaManage; policies HTTP por reflection em PermissionPolicies; PermissionTests Viewer atualizado); namespace `Application/Sla/Services` (pasta .gitkeep já existia) com IServiceRepository (GetByName/Search) — auto-registro por convenção **não pega** no BaseIntegrationTest (a base não faz varredura — linha manual, mesmo achado do F7); rota `api/v1/sla/services` (`ServicesController`, policies SlaRead/SlaManage); Delete sem pré-check de dependentes ainda (Slo = F8-T005, FK Restrict backstop); migração `AddServices` **gerada SEM `--no-build`** (regra do reparo F7-T006) + índice SQL manual; testes +17 unit (ServiceTests 6 domínio, ServiceCommandTests 11 CRUD pt-BR) +5 integração (CRUD, 409, 403 SlaManage/SlaRead, 404) — suíte 960 unit + 84 integration + 8 arch = 1052, 0 falhas; docs/04 seção Service -->
- [x] F8-T002 SlaPolicy <!-- política de compromisso de resposta/resolução (heurística: docs/04 só define "compromisso de resposta e resolução" — campos decididos dentro do pedido "sem pedir"): entidade `SlaPolicy` (AggregateRoot) Name 120 obrigatório + ResponseMinutes/ResolutionMinutes inteiros > 0 (400 pt-BR: "O prazo de resposta/resolução deve ser maior que zero.") + `ServiceId` opcional (FK Restrict p/ services = política global ou por serviço); tabela `sla_policies`; migração `AddSlaPolicies` **sem `--no-build`** (FK Restrict validada no script). CRUD em `api/v1/sla/policies` (SlaRead/SlaManage, busca term+serviceId); `DeleteServiceCommand` ganhou pré-check `HasServicePoliciesAsync` → 409 "existem políticas de SLA vinculadas" (padrão HasRacks); Delete sem pré-check de TaskSla ainda (F8-T003, FK Restrict backstop — prazos históricos preservados). `ISlaPolicyRepository` registrado manualmente no BaseIntegrationTest (sem varredura). Testes +18 unit (SlaPolicyTests 7, SlaPolicyCommandTests 11 — inclui 409 de DeleteService) +5 integração — suíte 978 unit + 89 integration + 8 arch = 1075, 0 falhas; docs/04 seção SlaPolicy -->
- [x] F8-T003 TaskSla <!-- aplicação de SLA à task (heurística: docs/04 só diz "O SLA aplicado deve preservar os prazos históricos" — modelo decidido dentro do pedido "sem pedir"): entidade `TaskSla` (Entity, PK = TaskId = 1:1, Ignore Id padrão TaskIncident) com snapshot `ResponseMinutes`/`ResolutionMinutes` + `ResponseDueAt`/`ResolutionDueAt` (= task.CreateDateTime + minutos; mudanças na política não alteram o snapshot); FK Task **Cascade** / FK SlaPolicy **Restrict**; tabela `task_slas`; migração `AddTaskSlas (20260928133914)` sem `--no-build`. `ApplyTaskSlaCommand` (SlaManage): 404 task/política, 409 "Já existe um SLA aplicado a esta task.", grava `SlaApplied` (+enum) no histórico com os prazos; `GetTaskSlaQuery` (SlaRead, 404 se não aplicado); rotas `GET/POST api/v1/tasks/{taskId}/sla` (ApplyTaskSlaViewModel); `DeleteSlaPolicyCommandHandler` +pré-check `HasPolicySlasAsync` → 409 (prazos históricos); `TaskSlaRepository` manual no BaseIntegrationTest. Nota: DateTime do Postgres trunca microssegundos → asserção com tolerância de 1s na integração. Testes +10 unit (TaskSlaTests 3, TaskSlaCommandTests 7) +5 integração — suíte 988 unit + 94 integration + 8 arch = 1090, 0 falhas; docs/04 seção TaskSla -->
- [x] F8-T004 TaskSlaPause <!-- pausa do relógio de SLA (heurística: docs/04 só lista a entidade — modelo decidido dentro do pedido "sem pedir"): entidade `TaskSlaPause` (TaskId FK→task_slas **Cascade**, StartAt, EndAt nullable = aberta; duração = EndAt−StartAt; fechamento idempotente-1x); tabela `task_sla_pauses`; migração `AddTaskSlasPauses` sem `--no-build`. Comandos (SlaManage): `PauseTaskSla` (404 sem SLA, 409 pausa aberta dupla, histórico `SlaPaused`), `ResumeTaskSla` (404 sem SLA, 409 sem pausa aberta, **soma duração aos `*DueAt`** via `TaskSla.ExtendDueDates` — snapshot de minutos intacto, histórico `SlaResumed` com `"+N min nos prazos"`); query `GetTaskSlaPauses` (SlaRead, cronológico, 404 sem SLA); rotas `GET .../sla/pauses` + `POST .../sla/pause|resume` no TasksController; `TaskSlaRepository.GetByTaskIdAsync` **sem AsNoTracking** (Resume altera dueAts e persiste); `ITaskSlaPauseRepository` manual no BaseIntegrationTest. Achados: SQL direto não atualiza instância rastreada → `Entry.ReloadAsync()` no teste; Postgres trunca microssegundos (tolerância 5s). Testes +11 unit (TaskSlaPauseTests 5, TaskSlaPauseCommandTests 6) +5 integração — suíte **999 unit** + 99 integration + 8 arch = 1106, 0 falhas; docs/04 seção TaskSlaPause -->
- [x] F8-T005 Slo <!-- objetivo de serviço (heurística: docs/04 dá só o exemplo "Availability >= 99.95% / Window = 30 days" — campos decididos dentro do pedido "sem pedir"): entidade `Slo` (AggregateRoot) Name 120, TargetPercent `numeric(6,3)` com 0 < alvo ≤ 100 (400 pt-BR), WindowDays > 0, ServiceId obrigatório FK **Restrict** (SLO mede serviço); tabela `slos`; migração `AddSlos` sem `--no-build`. CRUD em `api/v1/sla/slos` (SlaRead/SlaManage, busca term+serviceId); `DeleteServiceCommand` +pré-check `HasServiceSlosAsync` → 409 "existem SLOs vinculados"; Delete de SLO sem pré-check de Measurements/Budget (F8-T006/T007, FK Restrict backstop). `ISloRepository` manual no BaseIntegrationTest (3º handler do DeleteService — Service+SlaPolicy+Slo). Testes +17 unit (SloTests 7, SloCommandTests 10) +5 integração — suíte **1016 unit** + 104 integration + 8 arch = 1128, 0 falhas; docs/04 seção Slo -->
- [x] F8-T006 SloMeasurement <!-- medição do SLO (heurística: docs/04 só lista a entidade — modelo decidido dentro do pedido "sem pedir"): entidade `SloMeasurement` (registro imutável, sem PUT) SloId FK **Restrict** → slos, MeasuredAt, ValuePercent `numeric(6,3)` 0–100 (400 pt-BR fora); tabela `slo_measurements`; migração `AddSloMeasurements` sem `--no-build`. Comandos: `RecordSloMeasurement` (SlaManage, 404 SLO, 201), `DeleteSloMeasurement` (404); queries Search (cronológico desc por MeasuredAt, SlaRead) + Get (404); rotas aninhadas `GET/POST api/v1/sla/slos/{sloId}/measurements` + `GET/DELETE .../slos/measurements/{id}` no SlosController; `DeleteSloCommandHandler` +pré-check `HasSloMeasurementsAsync` → 409 (2º injetor: Slo+Measurement); `ISloMeasurementRepository` manual no BaseIntegrationTest. Testes +11 unit (SloMeasurementTests 4, SloMeasurementCommandTests 7) +5 integração — suíte 1028 unit + 109 integration + 8 arch = 1145, 0 falhas; docs/04 seção SloMeasurement -->
- [x] F8-T007 SloBudget <!-- orçamento/erro budget do SLO (heurística: docs/04 só lista a entidade — modelo decidido dentro do pedido "sem pedir"): entidade `SloBudget` SloId FK **Restrict** → slos, PeriodStart, PeriodEnd nullable (null = aberto; se presente > início, 400 pt-BR), ConsumedPercent `numeric(6,3)` 0–100 (400 pt-BR fora); tabela `slo_budgets`; migração `AddSloBudgets` sem `--no-build`. Comandos: `RecordSloBudget` (SlaManage, 404 SLO, 201), `DeleteSloBudget` (404); queries Search (cronológico desc por PeriodStart, SlaRead) + Get (404); rotas aninhadas `GET/POST api/v1/sla/slos/{sloId}/budgets` + `GET/DELETE .../slos/budgets/{id}` no SlosController; `DeleteSloCommandHandler` +pré-check `HasSloBudgetsAsync` → 409 (3º injetor: Slo+Measurement+Budget); `ISloBudgetRepository` manual no BaseIntegrationTest. Testes +11 unit (SloBudgetTests 6, SloBudgetCommandTests 7) +5 integração — suíte 1043 unit + 114 integration + 8 arch = 1165, 0 falhas; docs/04 seção SloBudget; **Fase 8 completa** (F8-T001→T007) -->

## Fase 9 — Notifications

- [x] F9-T001 Notification  <!-- docs/07 ADR-012: entity Notification (EventId único p/ idempotência visual, EventType, Title≤200, Message≤1000, audit fields) + NotificationConfiguration (índice único event_id) + migração AddNotifications; fluxo completo docs/06: OutboxDispatchHandler<TEvent> (handler MediatR aberto — todo domain event do AppDbContext vira OutboxMessage na mesma transação, ADR-005) → OutboxDispatcherWorker (já existia) → RabbitMQ → NotificationEventConsumer (UserCreated/UserDisabled/UserRoleAssigned/UserRoleRemoved/InvitationAccepted; idempotência via IInboxStore EventId+Consumer "NotificationConsumer"; logs Information; consumidor registrado no MassTransit do Workers); payload outbox com enums string (OutboxJson); destinatários F9-T002, canais F9-T004+ -->
- [x] F9-T002 Recipient <!-- docs/07 destinatários (heurística "sem pedir"): entidade `NotificationRecipient` usuário OU role (XOR pt-BR), FK notification Cascade, índices únicos (notification_id,user_id) e (notification_id,role) = dedup; consumer anexa destinatário na mesma transação (UserCreated/Disabled → usuário, UserRole* → role, InvitationAccepted → usuário por e-mail com fallback sem destinatário + Warning); resolução role→usuários ativos únicos (`ResolveUserIdsAsync` = "única notificação lógica") em GET .../notifications/{id}/recipients e .../recipients/users ([Authorize] + RoleAccess.Read, ADR-016); migração `AddNotificationRecipients` sem `--no-build`. Correções durante a ADR-016 da sessão paralela: AuthorizationBehavior OR (Overlaps) — AND tornaria RoleAccess.Read inatingível; `FakeCurrentUser` 1-arg nas 4 chamadas antigas. Testes +11 unit (NotificationRecipientTests 6, NotificationRecipientQueryTests 5) +8 estendidos no consumer +4 integração — suites F9-T002: 22 unit Notifications + 4 integration; repo 1060 unit + 73 arch + 110/118 integration (8 falhas pré-existentes da ADR-016 em RemoteAccess/Retention = pendência T9, não desta task) -->
- [x] F9-T003 Preferences <!-- heurística docs/07: preferência = liga/desliga canal por usuário; enum `NotificationChannel` (SignalR/Email/Telegram/WhatsApp/Webhook) + entidade `NotificationPreference` (UserId FK **Cascade** → DomainUsers, Channel enum string(32), Enabled; índice único (user_id,channel)); tabela `notification_preferences`; migração `AddNotificationPreferences` sem `--no-build`. Comandos: `SetNotificationPreference` (upsert por (usuário,canal); 404 usuário, 400 canal; escrita **RoleAccess.Operate** — decisão documentada: ADR-016 não separa próprio/alheio) + `DeleteNotificationPreference` (404); query Search (canal asc, 404 usuário, RoleAccess.Read); rotas GET/PUT/DELETE `api/v1/notifications/preferences`; Views em ViewModels/Notifications. Correção: Entity já nasce com Id — upsert decide `isNew` pelo FindAsync, não por Id==Empty. Testes +12 unit (NotificationPreferenceTests 4, NotificationPreferenceCommandTests 8) +5 integration — unit Notifications 34, 0 falhas -->
- [x] F9-T004 SignalR <!-- primeiro canal (heurística docs/07 sem API definida): **introduz `NotificationDelivery`** (notification_id/user_id FK Cascade, channel/status enum string, sent_at, last_error≤500 pt-BR; índice (channel,status); enum `DeliveryStatus` Pending/Processing/Sent/Failed/Cancelled); consumer enfileira deliveries SignalR por usuário final resolvido (preferência desligada = sem delivery; canais F9-T005+ enfileiram os seus); `NotificationsHub` em `/hubs/notifications` ([Authorize], grupo `user:{id}` via `SignalRUserIdProvider` = claim NameIdentifier); porta `ISignalRNotificationDispatcher` (sem dependência SignalR) implementada por `SignalRNotificationDispatcher` (IHubContext, evento `notification`); `NotificationSignalRWorker` (BackgroundService no Api, 2s, lote 50, `ProcessPendingAsync` testável) marca Sent/Failed — retry fica na F9-T009; Program.cs AddSignalR+MapHub+hosted; migração `AddNotificationDeliveries` sem `--no-build`. Testes +9 unit (NotificationDeliveryTests 6, NotificationSignalRWorkerTests 3) +2 consumer estendidos +2 integration — unit Notifications 45; INotificationDeliveryRepository manual no BaseIntegrationTest; suíte 1116 unit + 73 arch + 128 integration = 1317, 0 falhas -->
- [x] F9-T005 Email <!-- espelho F9-T004 (heurística docs/07): consumer enfileira deliveries do canal Email por usuário final (preferência desligada = sem delivery; usuário sem e-mail = sem delivery + Warning); porta `IEmailNotificationDispatcher` (`EmailNotificationMessage` To/Subject/Body) com adaptador `LoggingEmailNotificationDispatcher` (Infrastructure, espelho `LoggingIdentityEmailSender` — sem SMTP real, provider troca sem tocar Application); `NotificationEmailWorker` sobre o novo **`NotificationChannelWorkerBase`** (loop comum 2s refactorado do F9-T004); `PendingDeliveryItem` + `RecipientEmail` (join DomainUsers); Program.cs hosted service; DI em DependencyInjection.cs. Testes +6 unit (EmailWorkerTests 3, consumer +2, 1 corrigido p/ filtro de canal) +1 integration; suíte unit Notifications 50; integração Notification 12, 0 falhas -->
- [x] F9-T006 Telegram <!-- decisões do usuário: (1) o ADMIN escolhe os canais pela seção Notifications:Channels no appsettings (Default = lista do fanout, default ["Email","SignalR"] — antes fixo em código; mesma seção na Api e nos Workers); (2) destino GLOBAL por canal (Telegram:ChatId), não por usuário; (3) fallback de e-mail: nenhum canal da lista enviável → consumer enfileira Email; (4) Telegram/WhatsApp/Slack = adapter de logging nesta fase (credencial real = pendência junto do SMTP); (5) Slack entra como task nova F9-T010. Escopo: NotificationChannelsOptions (Application/Notifications/Channels: SectionName + Default + Telegram.ChatId) + porta ITelegramNotificationDispatcher/TelegramNotificationMessage (espelho Email) + LoggingTelegramNotificationDispatcher (Infrastructure, DI) + NotificationTelegramWorker sobre o worker base (ChatId vazio → Failed com erro pt-BR apontando a seção) + consumer ganhou IOptions<NotificationChannelsOptions> com ResolveFanoutChannels (canal desconhecido = Warning e ignorado, duplicado não repete; lista vazia/sem enviáveis → fallback Email) e IsSendable (Email/SignalR sempre, Telegram exige ChatId, canais sem worker = Warning + não enviável) + AddOptions().Bind na Api e Configure nos Workers + seção nos 2 appsettings; preferência F9-T003 continua filtrando por canal. RED antes do GREEN (9 erros CS0234/CS0246 só em tests/); testes +9 (NotificationTelegramWorkerTests 4: Sent com ChatId, Failed pt-BR, ChatId vazio → erro de config, sem pendentes; NotificationEventConsumerTests +5: Telegram enfileirada, preferência desligada, sem ChatId pula e defaults seguem, só-Telegram-sem-ChatId → fallback Email, fanout exatamente a lista com "Slack" inexistente ignorado) → suítes 1202 unit + 84 arch, 0 falhas; build Release 0 erros (18 avisos pré-existentes); integração não roda (sem Docker) -->
- [x] F9-T007 WhatsApp <!-- espelho direto do Telegram (F9-T006), mesmas decisões do usuário: destino GLOBAL por canal (Notifications:Channels:WhatsApp:Phone) e adapter de logging nesta fase (credencial real = pendência junto do SMTP/bot token). Escopo: WhatsAppChannelOptions em NotificationChannelsOptions + porta IWhatsAppNotificationDispatcher/WhatsAppNotificationMessage (espelho Email/Telegram) + LoggingWhatsAppNotificationDispatcher (Infrastructure, linha de DI) + NotificationWhatsAppWorker sobre o worker base (Phone vazio → Failed com erro pt-BR apontando a seção) + case WhatsApp em IsSendable no consumer (Phone obrigatório; Email/SignalR sempre enviáveis) + seção WhatsApp nos 2 appsettings (binding da seção já existia nas hosts — Program.cs não mudou). RED: 5 erros CS0234/CS0246 só no teste novo; testes +8 (NotificationWhatsAppWorkerTests 4: Sent com Phone, Failed pt-BR, Phone vazio → erro de config, sem pendentes; NotificationEventConsumerTests +4: WhatsApp enfileirada, preferência desligada, sem Phone pula e defaults seguem, só-WhatsApp-sem-Phone → fallback Email) → suítes 1210 unit + 84 arch, 0 falhas; build Release 0 erros (18 avisos pré-existentes) -->
- [x] F9-T008 Webhook <!-- espelho WhatsApp/Telegram (decisões F9-T006): destino global `Notifications:Channels:Webhook:Url` (sem Url = Warning sem delivery; fallback Email se nada enviável) + porta `IWebhookNotificationDispatcher`/`WebhookNotificationMessage` (Url/Title/Message) + `LoggingWebhookNotificationDispatcher` (Infrastructure, DI — nenhum POST real, HTTP troca sem tocar Application) + `NotificationWebhookWorker` sobre o worker base (Url esvaziada → Failed pt-BR apontando a seção) + case Webhook em `IsSendable` + seção Webhook nos 2 appsettings + hosted service no Program.cs. RED antes do GREEN (erros só em tests/). Testes +8 (NotificationWebhookWorkerTests 4: Sent com Url, Failed, Url vazia → erro de config, sem pendentes; NotificationEventConsumerTests +4: Webhook enfileirada, preferência desligada, sem Url pula e defaults seguem, só-Webhook-sem-Url → fallback Email) — unit Notifications 75; suíte 1218 unit + 84 arch + 129 integration = 1431, 0 falhas -->
- [x] F9-T009 Retry/DLQ <!-- fecha o worker base p/ todos os canais (heurística docs/07): `NotificationDelivery` + `AttemptCount`/`NextAttemptAt` + `RegisterFailedAttempt(error, now, maxAttempts, backoff)` (backoff exponencial base dobrando; 5ª falha = DLQ = Failed sem próximo) + colunas `attempt_count`/`next_attempt_at` (migração `AddDeliveryRetry` sem `--no-build`); `ListPendingByChannelAsync` passa a devolver Pending OU Failed com `NextAttemptAt <= agora` (DLQ nunca volta); `NotificationChannelWorkerBase.MaxAttempts = 5` + `RetryBackoff = 30s` e `MarkFailed` → `RegisterFailedAttempt` com log Error na DLQ/warning com retry n/5; `MarkSent` limpa `NextAttemptAt`. RED antes do GREEN (30 erros só em tests/). Testes +7 unit (NotificationDeliveryTests +3: backoff, progressão, DLQ; NotificationSignalRWorkerTests +3: retry vencido → Sent, não vencido → skip, DLQ → nunca; ) +1 integration (repositório devolve retry vencido e exclui DLQ) — unit Notifications 89, integration Notification 13; suíte: 1235 unit (2 falhas NÃO desta task: CorrelationTracingTests 2/2 quebradas pela sessão paralela com ADR-021 em andamento — MassTransitEventBus/Workers Program modificados fora do meu commit) + 84 arch + 130 integration, 0 falhas fora da ADR-021 -->
- [x] F9-T010 Slack <!-- decidido pelo usuário na F9-T006: enum `NotificationChannel` + `Slack = 6` (enum string no banco = sem migração) + destino global `Notifications:Channels:Slack:Channel` (sem Channel = Warning sem delivery; fallback Email se nada enviável) + porta `ISlackNotificationDispatcher`/`SlackNotificationMessage` (Channel/Title/Message) + `LoggingSlackNotificationDispatcher` (Infrastructure, DI — nenhuma chamada real, bot API = pendência junto do SMTP) + `NotificationSlackWorker` sobre o worker base (Channel esvaziado → Failed pt-BR apontando a seção) + case Slack em `IsSendable` + seção Slack nos 2 appsettings + hosted service no Program.cs; executa antes do F9-T009 (que fecha o worker base para todos). RED antes do GREEN (erros só em tests/). Testes +8 (NotificationSlackWorkerTests 4: Sent com Channel, Failed, Channel vazio → erro de config, sem pendentes; NotificationEventConsumerTests +4: Slack enfileirada, preferência desligada, sem Channel pula e defaults seguem, só-Slack-sem-Channel → fallback Email; comentário do teste de fanout atualizado — Slack agora é canal conhecido) — unit Notifications 83; suíte 1227 unit + 84 arch + 129 integration = 1440, 0 falhas -->

## Fase 10 — Knowledge

- [x] F10-T001 Category <!-- heurística docs/04 (Knowledge Base mínimo): entidade `KnowledgeCategory` (name≤120 **único case-insensitive**: pré-check handler → 409 pt-BR + índice `ux_knowledge_categories_name ON (LOWER(name))` via SQL bruto, precedente Location; description≤500 opcional); tabela `knowledge_categories`; migração `AddKnowledgeCategories` sem `--no-build`. Porta `IKnowledgeCategoryRepository` (HasNameAsync c/ excludeId, SearchAsync term→nome/descrição asc); comandos Create (201)/Update/Delete (404, escrita RoleAccess.Operate) + queries Search/Get (RoleAccess.Read); rotas `api/v1/knowledge/categories` no novo `KnowledgeController` + ViewModels com [Required]/[MaxLength] (400 pt-BR); `IKnowledgeCategoryRepository` manual no BaseIntegrationTest; FK dos artigos fica p/ F10-T002. RED antes do GREEN (12 unit + 4 integration erros só em tests/). Testes +17 unit (KnowledgeCategoryTests 7, KnowledgeCategoryCommandTests 10) +5 integration — suíte 1253 unit + 86 arch + 135 integration = 1474, 0 falhas; docs/04 §Categorias -->
- [x] F10-T002 Article <!-- heurística docs/04 (Knowledge Base mínimo). Decisões da task: (1) FK SetNull — apagar categoria não apaga nem bloqueia o artigo (T001 sem FK; constraint vem aqui); (2) enum KnowledgeArticleStatus (Draft/Published/Archived, string(32)) já existe e a entidade nasce Draft, mas Update NÃO mexe no status e não há endpoint de transição — Publishing = F10-T003; (3) content ≤20.000 (maior texto livre do repo = 1.000; corpo de KB exige mais — decisão da task); (4) RoleAccess ADR-016 como a T001 (leitura Read, escrita Operate); (5) controller novo KnowledgeArticlesController porque KnowledgeController já ocupa api/v1/knowledge/categories. Escopo: entity KnowledgeArticle (title 200/content 20000/category_id?/status, invariantes pt-BR) + KnowledgeArticleStatus; Knowledge/Articles/ (Dto com enum Status, porta IKnowledgeArticleRepository com SearchAsync por termo em title/content mais recentes primeiro, comandos Create 201/Update/Delete — categoria de destino inexistente = 404 pt-BR antes de persistir —, queries Search/Get) + KnowledgeArticleRepository + KnowledgeArticleConfiguration (tabela knowledge_articles, FK SetNull, índice category_id) + DbSet + migração AddKnowledgeArticles (script TEMP, sem --no-build, output-dir Persistence/Migrations) + rota api/v1/knowledge/articles + ViewModels 400 pt-BR + linha manual no BaseIntegrationTest. RED antes do GREEN (10 erros CS0234/CS0246 só em tests/). Testes +18 unit (KnowledgeArticleTests 9 invariantes, KnowledgeArticleCommandTests 9 CRUD/404/busca) +5 integration (KnowledgeArticleIntegrationTests: CRUD com categoria, 404 artigo, 404 categoria, 400 título, 403 sem Operate) — suíte 1271 unit + 88 arch, 0 falhas; 1 flaky na validação final (CorrelationTracingTests.MassTransitEventBus_WithoutCorrelation_ShouldPublishPlain — timeout 24s sob carga, isolado 4/4 verde, registrado na Fase P; não desta task); integração compila mas não executa (Docker não instalado — desinstalado em 27/07/2026); docs/04 §Artigos -->
- [x] F10-T003 Publishing <!-- heurística docs/04 (Knowledge Base): ciclo **Draft → Published → Archived → Draft** no domínio (`KnowledgeArticle.TransitionTo`; qualquer outra transição, inclusive repetir o mesmo estado, = **409** `Transição inválida: 'X' → 'Y'.` via InvalidOperationException → GlobalExceptionHandler); status fora do enum = 400 `Status inválido: 'X'. Use 'draft', 'published' ou 'archived'.` (precedente AlertQueries; WithMessage com sufixo .* pelo `(Parameter 'Status')` do ArgumentException); rota `POST api/v1/knowledge/articles/{id}/status` (`RoleAccess.Operate`, 200 devolve dto) + VM `[Required]`; **rascunho (Draft) só para quem escreve** (ADR-016 ≡ Operate): `SearchAsync` ganhou `includeDrafts` (WHERE Status != Draft junto com a contagem) e GET/{id} de Draft sem escrita = 404 mesmo (não vaza existência); queries Search/Get passam a receber `ICurrentUserContext`. RED antes do GREEN (9 erros CS0246 só em tests/). Testes +17 unit (KnowledgeArticleTests +6 transições válidas/inválidas; novo `KnowledgeArticleStatusCommandTests` 11: ciclo completo via handler, 409×2, 404, 400 status, busca oculta/mostra Draft, GET Draft leitor 404 / operador ok / leitor Published ok) +3 integration (KnowledgeArticleIntegrationTests: ciclo via API, transição inválida 409 pt-BR, Draft invisível para Viewer — busca/404/403) — suíte **1288 unit + 88 arch, 0 falhas** (1 flaky conhecido RdpRemoteProviderTests...NlaFailurePtBr na rodada completa — já na Fase P; 1 falha própria de asserção no meio, corrigida com o sufixo .* conforme precedente); integração compila mas não executa (Docker não instalado); docs/04 §Publicação -->
- [x] F10-T004 Relationships <!-- heurística docs/04 ("Relacionamentos possíveis com Device, Incident e Service"): 3 junctions N:N espelhando TaskDevice (KnowledgeArticleDevice/Incident/Service, Entity com chave composta article_id+<target_id>, Ignore(Id), factory com Guid vazio = 400 pt-BR); configs knowledge_article_devices/incidents/services com FK CASCADE DOS DOIS LADOS — decisão do usuário (30/09): apagar device/incident/service remove só o vínculo, o artigo permanece (sem pré-check nos deletes de outros módulos); apagar artigo limpa os vínculos; migration AddKnowledgeArticleLinks (script TEMP, sem --no-build; ON DELETE CASCADE confirmado no arquivo). Portas IKnowledgeArticle{Device|Incident|Service}Repository (ExistsAsync/FindAsync/Get*IdsAsync) + repositórios (GetByIdAsync = NotSupportedException, padrão TaskDevice); 6 comandos Link/Unlink (Operate): artigo 404 → alvo 404 pt-BR → duplicidade 409 "O device já está vinculado ao artigo."; GET /{id} devolve deviceIds/incidentIds/serviceIds (init props no dto, a busca não detalha — sem N+1); 6 rotas POST/DELETE {id}/{devices|incidents|services}/{targetId} (204); 3 linhas manuais no BaseIntegrationTest. RED antes do GREEN (20+ erros CS0246/CS1061 só em tests/). Testes +24 unit (KnowledgeArticleLinkTests 9 invariantes, KnowledgeArticleLinkCommandTests 15 — incluem os 2 de cascade da decisão do usuário) +4 integration (KnowledgeArticleLinkIntegrationTests: lifecycle device, 404 alvo, 409 duplicidade, 403 sem Operate) — suíte 1312 unit + 88 arch, 0 falhas; integração compila mas não executa (Docker não instalado); docs/04 §Relacionamentos -->
## Fase 11 — Remote Access

- [x] F11-T001 RemoteSession  <!-- entity RemoteSession (Open/Close/Fail com invariantes pt-BR, FailureReason≤500) + Permission.RemoteSessionsRead (= docs/10 device.remote.read; Admin herda automaticamente, User/Viewer não — matriz de connect/use em F11-T004) + GetDeviceSessionsQuery (IQuery+IAuthorizedRequest, ordem StartedAt desc, limit 1–500, 404 pt-BR) + GET /api/v1/inventory/devices/{id}/remote/sessions ([Authorize(Roles="Admin")] conservador até F11-T004) + DTO RemoteSessionDto sem campos de credencial (teste de reflexão, docs/10) + tabela remote_sessions (FK device cascade, índices (device_id, started_at)) + migração AddRemoteSessions; abrir/conectar sessão real só com provider SSH/RDP em F11-T002/T003; AuditEntry explícito em F11-T005; 16 unit + 2 integration -->
- [x] F11-T002 SSH Provider  <!-- ports IRemoteProvider/IRemoteCredentialSource/IRemoteConnectionRegistry + Permission.RemoteSshConnect (= device.ssh.connect; Admin herda, User/Viewer não — matriz completa em F11-T004) + OpenSshSessionCommand/CloseSshSessionCommand (BaseCommand+IAuthorizedRequest, sem restrição de tipo — decisão do usuário; device sem IP → ArgumentException 400, credencial ausente → InvalidOperationException 409 sem persistir sessão, falha de conexão → sessão Failed persistida com FailureReason≤500 (senha mascarada) + 409, fechar sessão de outro device → 404, já finalizada → 409) + SshRemoteProvider (SSH.NET 2026.0.0 — 2025.1.0 tinha advisories NU1903; Connect() síncrono via Task.Run, ConnectionInfo.Timeout, erro vira RemoteConnectResult pt-BR sem exceção vazando) + ConfigurationRemoteCredentialSource (seção RemoteAccess:Ssh provisória, vazia no git — F11-T006 Credential Store) + RemoteConnectionRegistry (singleton em memória, dispose no close) + POST/DELETE /api/v1/inventory/devices/{id}/remote/sessions (201/200, [Authorize(Roles="Admin")]) + 22 unit + 5 integration + 2 commands na regra BuildingBlocks; provider é falso nos integration tests (nenhuma rede) -->
- [x] F11-T003 RDP Provider  <!-- probe TCP/X.224, decisão do usuário (sem autenticação — NLA/CredSSP fica para task futura; computador Windows/Workstation sem restrição de tipo): Permission.RemoteRdpConnect (= device.rdp.connect; Admin herda, User/Viewer não — matriz em F11-T004) + OpenRdpSessionCommand (BaseCommand+IAuthorizedRequest; SEM IRemoteCredentialSource — probe não usa credencial; device sem IP → ArgumentException 400, probe falho → sessão Failed persistida com FailureReason≤500 + 409 "Falha ao abrir sessão RDP em '{name}'") + RENOMEIO CloseSshSessionCommand → CloseRemoteSessionCommand (lógica era protocolo-agnóstica e há um único DELETE; RequiredPermissions = RemoteSshConnect+RemoteRdpConnect AND, Admin-only hoje — matriz real em F11-T004; refatora T002: helpers internos viram RemoteSessionFlow) + RdpRemoteProvider (TcpClient + X.224 CR com RDP Negotiation Request SSL|HYBRID sem cookie; parse do CC: Neg Response → sucesso mantendo socket como conexão da sessão, Neg Failure → erro pt-BR mapeado (SSL/NLA/etc), TPKT/lixo → "Endpoint não respondeu como RDP", porta fechada/timeout → RemoteConnectResult falho sem exceção vazando, ct cancelado → rethrow; RemoteRdpOptions RemoteAccess:Rdp Port=3389/TimeoutSeconds=10 sem segredo) + DI Configure+AddScoped + seção appsettings + POST .../remote/sessions/rdp (201, [Authorize(Roles="Admin")]) + 3 commands na regra BuildingBlocks (2 no lugar do antigo — rename contado) + 20 unit + 5 integration (RdpRemoteProviderTests com FakeRdpServer TcpListener loopback: CC válido → sucesso, Neg Failure NLA → pt-BR, resposta não-RDP → falha, porta fechada → falha, silêncio → timeout 1s, ct cancelado → throw; RdpSessionCommandTests ciclo Open→Close com fakes; PermissionTests User/Viewer NotContain; RdpSessionIntegrationTests Admin abre→fecha, Viewer negado "*RemoteRdpConnect*") — provider é falso nos integration tests (nenhuma rede) -->
- [x] F11-T004 Permissions  <!-- matriz decisão do usuário "Viewer lê, User lê+conecta": Viewer=RemoteSessionsRead; User=+RemoteSshConnect+RemoteRdpConnect; Admin=catálogo completo (herda por Enum.GetValues); novos Permission.CredentialUse/CredentialManage Admin-only para F11-T006 (nenhum command os exige hoje); device.remote.session descartado como redundante com ssh/rdp.connect e removido do lista docs/10; RemoteSessionsController sincronizado (GET Admin,User,Viewer / POST/rdp/DELETE Admin,User); PermissionTests atualizados (matriz por role + NotContain Credential*), integration novo Viewer lê histórico e User abre→fecha SSH, negative de RemoteSessionsRead trocou UseRole(Viewer) por UseNoPermissions() porque toda role agora lê; docs/10 lista de permissões + seções T001/T002/T003 atualizadas + seção F11-T004; 490 unit + 8 arch + 20 integration -->
- [x] F11-T005 Audit  <!-- integra com audit existente (F1-T010): AuditBehavior agora preenche ActorId com ICurrentUserContext.UserName (decisão do usuário: login legível — antes sempre null/UI mostrava "sistema"; vale para todos os requests) + novo marker IAuditableRequest.AuditResource (BuildingBlocks ao lado de IAuthorizedRequest) usado pelo AuditBehavior com fallback "ApplicationRequest" — 1 entrada por request, sem duplicar; os 4 requests remote declaram AuditResource="RemoteSession" (action = nome do comando); path Failed grava error=tipo da exceção com ator; 3 unit AuditBehaviorTests + 1 integration (Admin open→close grava entries com ActorId) -->
- [x] F11-T006 Credential Store  <!-- decisões do usuário: DB criptografado + chave só em env, rotação manual PUT+audit, API GET/PUT/DELETE por device; tabela device_credentials (device_id único, FK cascade, username≤256, protected_password≤1024, rotated_at) + migração AddDeviceCredentials; cifra AES-256-GCM (AesGcmCredentialProtector prefixo v1:, chave base64 32 bytes em RemoteAccess:CredentialEncryption:Key — appsettings vazio no git, erros pt-BR sem vazar detalhe criptográfico; decisão técnica: AES-GCM com chave em env em vez de Data Protection — sem key ring em container); cadeia StoreRemoteCredentialSource (store por device → fallback ConfigurationRemoteCredentialSource global → 409 pt-BR); DI troca IRemoteCredentialSource; endpoint /devices/{id}/remote/credential [Authorize(Admin)] + CredentialManage; DeviceCredentialDto sem senha (teste de reflexão); 3 slices na regra BuildingBlocks; Get sem credencial → HasCredential=false (não 404); CredentialUse permanece sem consumo (exigir no OpenSsh quebraria a matriz User da F11-T004 — documentado); 34 unit + 5 integration; docs/10 seção F11-T006 + notas T002/T004 atualizadas -->

## Fase 12 — Operação

- [x] F12-T001 Cleanup  <!-- decisão do usuário: expandir limpeza fixa (sem RetentionPolicy — fica na T002) cobrindo as 4 tabelas de histórico existentes; RetentionOptions +MonitoringResultsDays 180, +NotificationsDays 90, +RemoteSessionsDays 90, +DiscoveryRunsDays 180 (só FinishedAt != null — runs ativos nunca são tocados) com cutoffs; CleanupService +4 deletes em batch; CleanupResult/worker log com 7 contagens; appsettings Retention: +4 chaves; regra docs/11 de inventário coberta por teste (_KeepRecentActiveAndInventory: devices/locations intactos); testes estendidos (defaults/cutoffs/fluxo) — 528 unit + 8 arch + 26 integration; docs/11 cobertura mapeada -->
- [x] F12-T002 Retention  <!-- decisões do usuário: nova Permission RetentionManage (Admin-only, = operations.retain.manage) e cobrir os 7 tipos do cleanup; entidade RetentionPolicy (retention_policies, data_type único, days 1..3650 pt-BR) + migração AddRetentionPolicies (snake_case); GET/PUT/DELETE em api/v1/operations/retention-policies[/{type}] com fonte Policy|Config; CleanupService mescla policies sobre Retention:* a cada execução; slice Application.Operations.Retention (3 na regra); pacote Microsoft.Extensions.Options no Application; PermissionTests +NotContain; 17 unit + 6 integration; docs/11 -->
- [x] F12-T003 Backup  <!-- decisões do usuário: BackupWorker .NET + diretório local + retenção 7d; PostgresBackupService com seam IProcessRunner (SystemProcessRunner mata processo no cancelamento), pg_dump -Fc, senha só via PGPASSWORD do ConnectionString (nunca em argv/config nova); arquivos prefixo-aaaammdd-hhmmss.dump, podar prefixo-* além de RetentionDays só em sucesso, status heartbeat backups/latest.json, falha/timeout apaga parcial sem tocar no latest; BackupWorker a cada IntervalHours (1ª imediata, padrão CleanupWorker), Enabled=false desliga; seção Backup no Workers appsettings; 5 unit novos (sucesso+podar, falha sem parcial, senha fora de argv, timeout, defaults); docs/operations/BACKUP-RESTORE.md com runbook; 550 unit + 8 arch + 32 integration -->
- [x] F12-T004 Restore Test  <!-- decisão do usuário: E2E round-trip automatizado (sem script/job extra); RestoreRoundTripTests no PostgreSQL real do Testcontainers: marker → PostgresBackupService de verdade (pg_dump -Fc) → CREATE DATABASE infracontrol_restore_test → pg_restore --no-owner → marker 1 linha + contagem de locations + contagem de tabelas public iguais; exige pg_dump/pg_restore no PATH com falha instrutiva (nunca skip escondido); PostgresConnectionString exposta no BaseIntegrationTest; IntegrationTests referencia o projeto Workers; CI = teste periódico; docs/operations/BACKUP-RESTORE.md seção Restauração; 1 integration novo (550 unit + 8 arch + 33 integration) -->
- [x] F12-T005 Performance  <!-- escopo decidido pelo usuário: auditoria + 3 correções (sem paralelismo); Dia 5 do docs/20. N+1: ExecuteAsync salvava por device → SaveChanges em lote no fim do RunAll (atomicidade via TransactionBehavior), teste com contador de SaveChanges==1; blocking: RetentionPolicyRepository sem ContinueWith/.Result → await puro; medição: PerformanceBehavior Warning acima de Performance:SlowRequestMs (default 1000, seção appsettings Api+Workers) com IOptions<PerformanceOptions> (Application.Common.Interfaces), 3 testes novos incluindo histograma via MeterListener; jobs/async sem problema (Task.Run só no SSH.NET síncrono, serial nos checks por DbContext segurar — registrado); novo docs/26-PERFORMANCE.md + índice; 554 unit + 8 arch + 33 integration -->

## Fase 14 — Frontend: completar áreas da plataforma

Prototipação das páginas que faltam conforme `docs/14-UI-UX.md`, com mock data em `src/data/`, rota, item de sidebar e componentes PrimeReact. Backend alinha depois.

- [x] F14-T001 Users & Roles (prioridade: backend F1 já existe)
- [x] F14-T002 Audit
- [x] F14-T003 Incidents
- [x] F14-T004 Notifications
- [x] F14-T005 Locations & Racks
- [x] F14-T006 SLA/SLO & Services
- [x] F14-T007 Switches/Ports como áreas próprias (hoje embutidas em Network/Devices)
- [x] F14-T008 Autenticação do dashboard (tela de Login no estilo UniFi com alternância Senha/Google Identity Services e **sem** One Tap; página "Esqueci minha senha" com resposta anti-enumeração; `authSlice` com `status` (`checking`/`authenticated`/`anonymous`/`unavailable`) em vez de `isAuthenticated`; `redux-persist` com whitelist apenas de `user`; `SessionGate` que revalida com `GET /api/v1/auth/me` no boot e nunca confia em `localStorage`; `POST /auth/google` com o `id_token` (o `refreshToken` da resposta é descartado); proteção de rotas por sessão; usuário/role reais e botão "Sair" no AppLayout; `AuthApiError` sem vazar mensagem do servidor)
- [x] F14 polish UI (Health no sidebar, chunks vendor/redux no Vite, accent unificado)
- [x] F14-T009 Autorização por roles no frontend (menu, rotas e botões) <!-- decisões do usuário (questionário antes do plano): escopo = menu + rotas + botões; nível administer em /users, /audit, /settings E /sessions (a UI fica mais estrita que o self-service do backend nesta — decisão consciente); URL acima do nível = tela 403 dedicada ("Acesso restrito" + voltar), não redirect. Espelho do ADR-016 na UI: novo `src/app/router/access.ts` (ROUTE_ACCESS com padrão read + `useCan`/`canReach` reaproveitando `types/roles.ts`); `ProtectedRoute` em cascata (sem sessão → /login como antes; sessão ok + nível insuficiente → `AccessDenied`); AppLayout filtra `navigation`/`secondaryNavigation`/abas recentes por nível; componente `<Can level="operate">` nos 8 CTAs de escrita (Devices "Adicionar", Incidents "Novo Incidente", Knowledge "Novo Artigo", Network "Adicionar", Remote "Nova Conexão", Sla "Novo Serviço", Tasks "Nova Tarefa", WiFi "Criar Rede") — Users/Sessions/Settings já ficam atrás do gate de rota. Backend intocado (continua autoridade: 403 no envelope é a rede de segurança); doc-comments de roles.ts/authSlice/AppLayout ajustados (exibição, não autorização efetiva). Validação: `npx tsc -b` + `npm run build` = 0 erros (sem runner de testes no front) -->

## Transversal — Autorização por roles (ADR-016)

Substitui o modelo de permissões (decisão humana, 2026-09-29). `User` → `Operator`; a role passa a ser a única unidade de acesso.

- [x] ADR-016 — decisão, supersessão do ADR-008 e matriz de equivalência <!-- docs/05 §ADR-016: role como fonte única; 3 níveis canônicos; enum `Permission`, `RolePermissions` e claims `permission` removidos -->
- [x] Migrar núcleo (enum, claims, `ICurrentUserContext`, `AuthorizationBehavior`, `RequiredRoles`) <!-- sem claims de permissão: `IdentityUserRoleRepository` e o bootstrap do Admin não sincronizam mais claims; `AuthorizationBehavior` exige autenticado e usa OU (basta 1 role exigida) -->
- [x] `RoleAccess` como hierarquia única + policies HTTP `Operate`/`Administer` <!-- `RoleAccess.Read/Operate/Administer` consumidos por MediatR e por `AuthorizationPolicies`; leitura usa `[Authorize]` -->
- [x] Converter 126 requests + 126 atributos de controller <!-- 52 leitura, 64 `Operate`, 10 `Administer` — equivalência 1:1 com o mapa anterior, sem reclassificar endpoint -->
- [x] Frontend sem permissões <!-- `types/roles.ts` espelha os níveis; sai a matriz de permissões; `AuthUser` sem `permissions`; `Operator` nos mocks -->
- [x] Migration de dados `'User'` → `'Operator'` <!-- `20260929010000`: `DomainUserRoles.Role`, `AspNetRoles.Name` **e** `NormalizedName`; esvazia `UserSessions` e rotaciona `SecurityStamp`; cookie antigo falha em `Operate`/`Administer` (fail-closed) -->
- [x] Guardas e testes <!-- `RoleAuthorizationStandardizationTests` (arquitetura, proíbe literal de role e policy por permissão), `AuthorizationPolicyTests` (9), `AuthorizationBehaviorTests` (12, matriz 3×3 positiva — sem ela uma regressão para AND passaria verde), `RenameUserRoleToOperatorMigrationTests` (3, executa o SQL da própria migration) -->
- [x] Documentação e estado <!-- docs/05, 09, 10, 11, 12, 15, 24 e ADR-008 com nota de supersessão -->

## Fase 15 — Integração Docker + Rastreio de requisição (concluída 2026-09-30)

Entrega o caminho de produção e a plataforma de logs (decisões do usuário
"faz tudo", registradas em ADR-021 e ADR-022; o "sequenciamento" de todas as
telas com API real fica na F14/pendências).

- [x] ADR-021 + ADR-022 <!-- decisões: Seq como plataforma única (workers entram na mesma config da API), nginx same-origin em vez de CORS (cookie Secure volta a valer), compose único com profiles (infra sem profile; app; obs), Seq SEM porta pública atrás de nginx com basic auth -->
- [x] Serilog nos Workers (O1) <!-- Adiciona Serilog.Extensions.Hosting/Settings.Configuration/Sinks.Console/Sinks.Seq nos Workers; Program.cs = Log.Logger via ReadFrom.Configuration + ClearProviders + AddSerilog; seção "Serilog" no appsettings dos Workers (espelho da API) -->
- [x] RequestLoggingMiddleware (O2) <!-- 1º middleware do pipeline; respeita X-Correlation-Id do cliente senão usa TraceIdentifier; LogContext.PushProperty; loga pelo template de rota; silent em /health -->
- [x] Propagação do correlation id (O3) <!-- ICurrentCorrelation + NullCurrentCorrelation (Application; Infrastructure registra o null e a Api sobrescreve no Program.cs — última-registração ganha); OutboxMessage.CorrelationId (coluna correlation_id, migração AddOutboxCorrelationId 20260930004311 isolada das colunas de retry da F9-T009); DomainEventService persiste; IEventBus.PublishAsync(object,Type,ct,correlationId) + MassTransitEventBus com Pipe.Execute<PublishContext> (header "CorrelationId"); OutboxDispatcherWorker empurra LogContext e o id; CorrelationIdConsumeObserver nos Workers (ConditionalWeakTable); testes CorrelationTracingTests (4) provam escritas + header ponta-a-ponta -->
- [x] Docker (D1-D3) <!-- docker/Dockerfile multi-stage com targets api/workers/frontend (build validado nos 3), docker/nginx.conf (443 SSL autoassinado + /api e /hubs + Seq em 5341 com basic auth + resolver dinâmico p/ seq/api — nginx sobe mesmo sem os serviços), docker-compose.yml unificado (profiles app/obs; otel-collector; docker-compose.infrastructure.yml removido), .dockerignore, .env.example estendido, README atualizado -->
- [x] Seq (O4) <!-- serviço seq sem ports:, exposto só pelo nginx com basic auth (env SEQ_AUTH_USER/PASSWORD; entrypoint gera htpasswd); smoke-testado: 401 sem auth, HTTPS 200, redirect 301 -->
- [x] Tela /logs (O5) <!-- LogsPage vira porta de entrada do Seq: botão "Abrir plataforma de logs" + busca por CorrelationId montando deep link #/events?filter=CorrelationId='...' (formato datalust.co/docs/urls); URL via VITE_LOGS_URL (default http://localhost:5341) -->
- [x] Alinhamento de endpoints front ↔ back <!-- decido pelo usuário 2026-09-30 com a pergunta "usa também o servidor nginx": nginx ganhou `location = /health` + `location /health/` (antes GET https://host/health caía no try_files e devolvia o index.html) e `location /api` sem barra (`/api/` não casava `/api`); SPA mudou de `/health` para `/status` (router + item do sidebar) para não disputar a rota com a API; RolesController `api/identity/users` → `api/v1/admin/user-roles` (único fora do versionamento; não `admin/users` porque colidiria com o `DELETE {userId}/roles/{role}` do UsersController = AmbiguousMatchException); `UseForwardedHeaders` no pipeline (X-Forwarded-For/Proto do nginx: rate limit por IP e links de e-mail — hoje todos os anônimos dividiam o IP do container e os links saíam `http://api:8080`); `UseHttpsRedirection` removido (TLS termina no nginx, não há porta HTTPS na API); proxy do Vite ganhou `/health`; `VITE_LOGS_URL` virou ARG/ENV no Dockerfile + build arg no compose + `.env.example` (a LogsPage apontava para a porta errada) e as envs passaram a ser declaradas no `vite-env.d.ts`. Teste novo `EndpointAlignmentTests` lê Program.cs/nginx.conf/vite.config/router e trava cada ponte; RED 8 → GREEN 98 arch + 1318 unit; `nginx -t` ok; `npm run build` ok -->
- [x] Boot da stack + redirect HTTPS com a porta certa <!-- validação em runtime da task acima (decisões do usuário: "restaurar appsettings + env por índice", "corrigir redirect", "parar api/workers"): (1) a stack não subia — API e Workers em crash loop (exit 139) com `Serilog:WriteTo:Seq:Name has no 'Name' element`: a env `Serilog__WriteTo__Seq__Args__serverUrl` do compose criava a seção nomeada "Seq" sem Name (a chave nomeada não casa o WriteTo array do appsettings); fix = env por **índice** (`Serilog__WriteTo__0__Name=Console`, `__1__Name=Seq`, `__1__Args__serverUrl=http://seq:80`, funciona com ou sem o bloco no appsettings) + **restauração do bloco Serilog no appsettings.json** (diff alheio não commitado que removia os 2 sinks — dev ficava sem log no console; `git checkout` do arquivo, o diff era só isso); (2) redirect HTTP→HTTPS `return 301 https://$host$request_uri` **perdia a porta** (HTTP_PORT=8085 → `https://localhost/`), e a correção óbvia `$http_host` mantém a porta ERRADA (a HTTP de origem, 8085, não a HTTPS 8445 — testado e reprovado em runtime); solução = porta HTTPS do host por **envsubst**: `docker/nginx.conf` virou `/etc/nginx/templates/nginx.conf.template` (`ENV NGINX_ENVSUBST_OUTPUT_DIR=/etc/nginx` + `NGINX_HTTPS_PORT` default 443 no Dockerfile, `${HTTPS_PORT}` no compose) com `map` que engole `:443`; (3) smoke real: `up -d --build` → `/status` 200 HTML, `/health` e `/health/ready` **502 do nginx** (prova do location novo; sem ele seria 200 com index.html), `/api/v1/auth/me` 502 (upstream fora), `/` e rota SPA 200, `http://localhost:8085/` → **301 https://localhost:8445/**, bundle do front com `localhost:5342` (VITE_LOGS_URL), htpasswd do Seq gerado no boot; (4) **bloqueio restante, fora de escopo**: API/Workers param em `MassTransit.ConfigurationException: License must be specified` (MassTransit 9.2.2 é comercial, `MT_LICENSE`) — pendência já registrada em PROJECT_STATE:155; containers parados (`docker compose stop api workers`) para não ficarem em loop. Testes: `EndpointAlignmentTests` 10 (+1 do redirect) → 99 arch + 1318 unit verdes; `nginx -t` ok -->
- [x] Rodada "testa tudo, resolve os bugs" + smoke completo <!-- pedido do usuário 01/10/2026 ("testa tudo cada integração, front e back, resolve todos os bugs encontrados"; ciclo fechado com commit/push na main). **Bugs achados e corrigidos** (RED→GREEN em todos): (1) API morria no boot sem `IProblemDetailsService` → `AddProblemDetails()` no Program.cs + teste `HostBootTests.Api_Should_Register_ProblemDetails_For_The_Empty_UseExceptionHandler`; (2) `AddMassTransit` duplicado nos Workers (Api e Workers registravam o bus 2×) → `AddInfrastructure(IConfiguration, configureBus)` recebe o registro dos consumers/observers por parâmetro (`Workers/Program.cs`), teste `MassTransit_Should_Be_Configured_Exactly_Once_Per_Repo`; (3) **X-Forwarded-Host perdido**: nginx não repassava o Host do cliente → links de e-mail/convite saíam `http://api:8080` → `proxy_set_header X-Forwarded-Host $http_host` nos 4 locations + flags `XForwardedHost` no `UseForwardedHeaders` (prova: link forgot-password = `https://localhost:8445/...`) + teste `Nginx_Should_Forward_The_Client_Host_With_Its_Port`; (4) **`userId` sempre nulo em `/auth/me`/logout/me** — o claim da sessão é `ClaimTypes.NameIdentifier` (URI) e os controllers liam `"sub"`/`"nameidentifier"` → novo `Services/ClaimsPrincipalExtensions.cs` (`GetSessionUserId[OrNull]` cobre os 3 formatos) aplicado em AuthenticationController (3×) e InvitationsController + testes novos `AuthenticationMeTests`/`InvitationsControllerTests`/logout com o claim real; (5) **login carregava roles vazias** (as 3 rotas de sessão não incluíam `RoleAssignments` → cookie sem claims de role → 403 em toda policy) → `.Include(u => u.RoleAssignments)` nos 3 caminhos do `IdentityAuthenticationService` (a Fase Q corrigiu por `AutoInclude` no EF — redundante com o Include, mantidas as duas); (6) compose não repassava `BootstrapAdmin__*` (bootstrap morto) → envs com defaults + teste `Compose_Should_Forward_The_Opt_In_Admin_Bootstrap`; (7) senha de exemplo violava a política Identity (RequiredLength 12) → `.env.example`/`.env` atualizados + teste `Example_Admin_Password_Should_Satisfy_The_Identity_Policy`; (8) `DockerPgToolRunner` só achava `docker.exe` → resolve por plataforma + `--add-host host.docker.internal:host-gateway` (fecha pendência Fase P; integração 164/164); (9) front sem lint → `eslint`+`typescript-eslint`+`eslint-plugin-react-hooks` (devDeps) + `eslint.config.js` → `npm run lint` 68 arquivos, 0 problemas (CI hoje só builda — lint extra é manual); (10) `withUrl(hubUrl, ...?: undefined)` não compilava → `IHttpConnectionOptions` sempre definido no `signalr/connection.ts` (negotiate do `/hubs` provado 400 sem token antiforgery → 200 com). **Repair do merge `a0a5acb`** (trouxe de volta código que a F18-T004/Fase Q tinham resolvido): `RolesController.cs` restaurado sem o `RoleViewModels.cs` que usava (build quebrado — removido de novo, completando a F18-T004); guard do `Logout` duplicado nas 2 versões (CS0165/CS0136 — mantida a `GetSessionUserIdOrNull`); usings duplicados em 2 testes (CS0105). **Validação final**: build Release **0 erros**; unit **1336/1336**, arch **102/102**, integração **164/164**; front `npm run lint` 0 e `npm run build` ok; smoke Docker: `/me` 401→200 com `userId`+`roles`, logout 204→401, forgot-password com link na porta HTTPS, convite 201, rate limit 6ª tentativa → 429 `Retry-After: 60`, redirect 301, `/health` 200, workers sem erro de licença (MassTransit **8.4.1** MIT fecha a pendência `MT_LICENSE`) -->

Decisões de coordenação entre sessões (registro): a migration `AddOutboxCorrelationId`
foi gerada com colunas `attempt_count`/`next_attempt_at` da F9-T009 ainda não
commitadas; o agente isolou o arquivo para conter SÓ `correlation_id` (gerando
`dotnet ef migrations script` limpo: cada coluna uma vez, tabelas distintas).
`docker-compose.infrastructure.yml` foi removido conforme ADR-022 e restaurado
por engano na F9-T009 (`bbff9ef`); removido de novo com registro explícito aqui.

## Fase 16 — Integrações

Integrações com sistemas externos (decisões do usuário 2026-09-30: Zabbix →
InfraControl via webhook; notificação de incidente em task separada com
destinatários Operator + Admin). Arquitetura em ADR-023, runbook em docs/27.

- [x] F16-T001 Zabbix Webhook  <!-- ADR-023: POST api/v1/integrations/zabbix/problems — primeiro endpoint público ([AllowAnonymous] explícito + entrada nova na allowlist de Public_Endpoints_Should_Be_The_Explicit_Allowlist); auth header X-Zabbix-Token vs Integrations:Zabbix:Token (vazio→404 desligado, ausente/errado→401 envelope; comparação ordinal; binding AddOptions+Bind no Program.cs); contrato {eventId?, host?, ip?, name, severity 0..5, status PROBLEM|OK} com validação no handler (name→severity→status, ArgumentException 400 pt-BR); PROBLEM → device por IP com fallback hostname (GetByIpAsync/GetByNameAsync) → Incident.Create (sem match → deviceId nulo com host/ip/eventId na descrição — decisão do usuário), dedup anti-storm título+device não-Resolved idempotente "duplicate"; OK → resolve correspondente "resolved" senão "none"; severidade Zabbix 0/1→Low, 2→Medium, 3→High, 4/5→Critical; comando BaseCommand sem IAuthorizedRequest (guard = token; Admin_Slices não muda); port nova GetOpenByTitleAndDeviceAsync em IIncidentRepository+IncidentRepository; sem migration; slice Application/Integrations/Zabbix + controller Integrations + VM; testes unit (handler + controller com Moq) + integration + allowlist arch -->
- [x] F16-T002 Notificação de incidente  <!-- IncidentCreatedDomainEvent (Domain/Events/IncidentDomainEvents.cs) levantado em Incident.Create — um único ponto cobre manual F6, auto-abertura F6 e webhook F16-T001; caminho outbox via AppDbContext → MediatR → OutboxDispatchHandler → DomainEventService (mesma transação, ADR-005 — sem migration e sem IEventBus direto); NotificationEventConsumer ganhou IConsumer<IncidentCreatedDomainEvent> (registro automático: AddConsumers assembly scan dos Workers); HandleAsync generalizado de recipientRole (string?) para recipientRoles (string[], dedup OrdinalIgnoreCase, usuário OU 1..N roles) — 32 call sites migrados; destinatários por role Operator e Admin (decisão do usuário 2026-09-30); msg pt-BR título "Incidente aberto", corpo "O incidente '{title}' foi aberto ({severity})."; fanout resolve roles → usuários ativos (NotificationRecipientRepository) nos canais default Email+SignalR; idempotência via inbox (1 notification, 2 recipients, 1 ProcessedMessage); unit +6 (IncidentTests +3, IncidentCreatedNotificationTests +3), integration +2 (outbox manual e via webhook) -->

## Fase P — Pendências (tudo que não foi concluído)

Consolidado único (decisão do usuário, 2026-09-29) de todo item em aberto até aqui:
tasks de fase com checkbox ainda `[ ]` **e** pendências soltas de tasks já
concluídas. É a lista oficial do "o que falta" — ao fechar um item, marque aqui
**e** na origem (linha da fase ou ADR).

### Tasks por concluir (checkboxes abertos nas fases acima)

- [ ] F0-T010 Configurar CI — **BLOCKED**: cota do GitHub Actions gratuita esgotada
  (2.000/2.000 min), reset em 01/10/2026
- [x] F9-T006 Telegram
- [x] F9-T007 WhatsApp
- [x] F9-T008 Webhook
- [x] F9-T009 Retry/DLQ (mensageria)
- [x] F9-T010 Slack
- [x] F10-T001 Category
- [x] F10-T002 Article
- [x] F10-T003 Publishing
- [x] F10-T004 Relationships
- [x] F16-T001 Zabbix Webhook
- [x] F16-T002 Notificação de incidente

### Pendências soltas (de tasks concluídas e decisões registradas)

- [ ] **Revalidar a suíte de integração** (Testcontainers/PostgreSQL) onde houver
  Docker — revalidada em 2026-09-30 nesta máquina (Docker Desktop 4.93.0
  instalado no ciclo pós-F16-T002): **152/152 verdes** na primeira execução
  completa da suíte nesta máquina (13m12s; antes: 130/130 junto da F15 e
  149/152 na primeira rodada com 3 falhas já corrigidas e commitadas);
  revalidada de novo no ciclo da Fase Q (30/09): **161/161** (152 + 9 e2e);
  e em 01/10 na auditoria de alinhamento front×back: **164/164** (161 + 3
  facts de alinhamento); pendência mantida para o caso de outras máquinas sem
  Docker
- [ ] **`postgresql-client` na imagem dos Workers** (`docker/Dockerfile`) —
  `PostgresBackupService` faz spawn de `pg_dump`/`pg_restore`, mas a imagem
  multi-stage **não instala o cliente PostgreSQL**: no deploy "Servidor com
  Docker" (decisão do usuário) o BackupWorker falharia com binário não
  encontrado (achado em 30/09/2026 na primeira execução completa da suíte com
  Docker). Resolve com `apk add --no-cache postgresql-client` no target
  `workers` — requer autorização (muda artefato de deploy)
- [ ] **Provider real de e-mail** (SMTP/API + credenciais) — hoje só adaptadores
  de logging (`LoggingIdentityEmailSender` + `LoggingEmailNotificationDispatcher`);
  cobre forgot-password, convite (F1-T017) e canal Email (F9-T005). Requer
  escolha do usuário
- [ ] **Página de aceite de convite no frontend** — o link e-maiado em F1-T017
  aponta para a API (`POST /auth/invitations/{id}/accept`) sem página GET
- [ ] **Página de redefinição de senha no frontend** — achado na auditoria de
  alinhamento front×back de 01/10/2026: o link do e-mail de forgot-password
  (`IdentityAuthenticationService` monta
  `{baseUrl}/api/v1/auth/reset-password?email=...&token=...`) bate no GET da
  API (rota só existe em POST → 405) e o front não tem rota pública
  `/reset-password` (só `/login` e `/forgot-password`) — o fluxo nasce no
  frontend e morre no e-mail. `POST /auth/reset-password` está alinhado
  (body `{email, token, newPassword}`, 204/400 no envelope) mas sem
  consumidor; resolve com página + origem do link (origin do frontend)
  decidida
- [x] **Aba Feed na NotificationsPage** (pendência da F18-T001, fechada
  em 01/10/2026 no ciclo de integração front×back pós-auditoria) — a aba
  tinha saído no F17-T010 por não existir listagem; a F18-T001 entregou
  `GET /api/v1/notifications` e esta task entrega a UI.
  `services/api/notifications.ts` ganhou `listFeed(page, pageSize)` →
  `PagedResult<NotificationFeedItemDto>` =
  `{id, eventType, title, message, createdAt}` (cabeçalho do serviço
  corrigido: a listagem existe desde a F18-T001). Página com abas Feed |
  Preferências (restaurando o layout de sublinhado do protótipo): feed
  20/página com pager (padrão AlertsPage), chip com `eventType` cru
  (string livre — não enum, sem rótulo inventado), data via
  `formatDateTime`, estados de loading/vazio/erro honestos com retry;
  **sem mutations** (decisão registrada da T001: sem estado de leitura —
  `read`/`status`/`recipient`/`channel` do protótipo não têm contrato e
  ficaram de fora); preferências intactas e banner de erro movido para
  dentro da aba ativa. Validação: `npx tsc -b` 0 erros + `npm run build`
  ok (front sem runner; `src/` intocado — o contrato do feed já é coberto
  pelos 2 e2e da F18-T001). Próximas integrações front×back na fila:
  filtro `kind` da ClientsPage (F18-T002) e transição de estado da
  TasksPage (pendência do F17-T001).
- [x] **e2e HTTP pelo pipeline completo** (`WebApplicationFactory`) — **FECHADA
  em 30/09/2026** (Fase Q): `tests/InfraControl.IntegrationTests/EndToEnd/` =
  `E2EHost` (PostgreSQL+Redis reais via Testcontainers, bootstrap real de
  `Program`, seed de operador/viewer/sem-papel, XSRF lido do cookie) +
  `FullPipelineE2ETests` com **12 cenários** (contrato login/`/auth/me` que o
  frontend consome, 401 pt-BR de senha errada, 400 de ModelState, matriz
  Operate, 403 da segunda camada sem papel, matriz Administer + antiforgery,
  criação/leitura de task por papel, webhook Zabbix público por token, 404 no
  envelope, e mais 3 de alinhamento front×back em 01/10: logout sem body →
  204+mata sessão, lista de sessões no formato exato da SessionsPage com
  revoke-others/404/revoke-própria, forgot-password com `message`) — fecha a
  pendência declarada nas ADR-017/ADR-018; os testes de e2e acharam e
  corrigiram 6 bugs reais de boot/autorização (ver PROJECT_STATE, ciclo
  Fase Q)
- [ ] **Rate limit específico do login com código MFA** — pendência da ADR-019
  (o `HttpRateLimit` de login cobre o passo 1)
- [ ] **Investigar flakiness** de `RdpRemoteProviderTests.ConnectAsync_NegotiationFailure_ShouldReturnNlaFailurePtBr`
  (timeout de loopback sob carga da suíte completa; passa isolado e em rerun)
- [ ] **Investigar flakiness** de `CorrelationTracingTests.MassTransitEventBus_WithoutCorrelation_ShouldPublishPlain`
  (F15/ADR-021 — `TaskCanceledException` de 24s sob carga da suíte completa;
  passa isolado 4/4; observado em 30/09 na validação da F10-T002)
- [ ] **Conectar as páginas com mock ao backend** — F14 foi prototipação com dados
  estáticos; `SessionsPage` (F1-T016d) foi a primeira com API real. Frente
  iniciada em 01/10/2026 (F17), uma página por vez:
  - [x] **F17-T001 `TasksPage`** — quadro por estados REAIS de `TaskState`
    (7 colunas; o mock usava `todo/in_progress/done`), chip de `TaskKind`,
    contagens de responsáveis/dispositivos/incidentes, contagem por
    `totalCount` e criação via `POST /tasks` atrás de `Can operate`
    (`services/api/tasks.ts`); campos do mock sem contrato no backend
    (`priority`, `dueDate`, `device`, `tags`) removidos — backend é a
    autoridade. Pendências da própria página: transição de estado
    (`PUT /tasks/{id}/state`) e filtros `term/kind/state` na UI
  - [x] **F17-T002 `AuditPage`** — `services/api/audit.ts`
    (`GET /admin/audit?action/outcome/page/pageSize`, policy Administer);
    bug de contrato corrigido: o mock esperava `outcome` "Success"/"Failure"
    mas o backend grava **"Succeeded"/"Failed"** (`AuditBehavior`) — a tela
    exibiria tudo como "Falha"; filtro de `action` virou igualdade exata no
    submit (contrato do reader), `from`/`to` existem na API sem seletor na
    tela; paginated com `PagedResult`; mock sem endpoint ("Pesquisar"
    substring em action/resource/ator e "Exportar") removido
  - [x] **F17-T003 `IncidentsPage`** — `services/api/incidents.ts`
    (`GET /incidents?deviceId/status/page/pageSize` → `PagedResult<IncidentDto>`,
    `POST` `{title,severity,description?,deviceId?}` → 201); bugs de contrato
    do protótipo: status **`Open`** (auto-abertura) inexistente no filtro,
    enums são **PascalCase** (chaves do mock minúsculas caíam no fallback),
    e `assignee`/`slaBreached`/`device` não existem no `IncidentDto`
    (responsável e coluna SLA saíram; `resolvedAt` entrou; `device`/
    `relatedAlerts` → `deviceId`/`alertCount`); criação atrás de
    `Can operate`; paginação sobre o `PagedResult`
  - [x] **F17-T004 `KnowledgePage`** — `services/api/knowledge.ts`
    (`GET /knowledge/articles?term/page/pageSize` → `PagedResult<KnowledgeArticleDto>`;
    `POST` `{title,content,categoryId?}` → 201 com validações DataAnnotations
    pt-BR; categorias em `GET /knowledge/categories` para chip e select);
    achado de contrato: `status` viaja como **número** (0/1/2 — o DTO expõe o
    enum e o MVC não registra `JsonStringEnumConverter`), `category`/
    `lastUpdate`/`tags` do mock não existem no DTO (nome resolvido pelo
    `categoryId`, data e tags saíram, `status` entrou); busca `term` ligada
    ao parâmetro real (substring); rascunho já escondido pelo backend para
    quem não escreve
  - [x] **F17-T005 `SlaPage`** — `services/api/sla.ts` (`GET /sla/services`,
    `/sla/slos`, `/sla/policies` com term/serviceId/page/pageSize e
    orçamentos em `/sla/slos/{sloId}/budgets`; `POST /sla/services`
    `{name,description?}`); colunas inventadas do protótipo sem contrato
    (`status` operacional/degradado, `uptime`, `slaTarget`/`sloTarget` em %,
    `openIncidents`, `owner`, `burnRate`) removidas — SLA virou a política
    real (`responseMinutes`/`resolutionMinutes`), SLO os `SloDto`
    (alvo % + janela) e Error Budget os `SloBudgetDto` (`consumedPercent`
    por período, rótulo Esgotado/Atenção/Normal derivado do número real);
    orçamento é 1 pedido por SLO (sem endpoint agregado — pendência se
    crescer)
  - [x] **F17-T006 `UsersPage`** — `services/api/adminUsers.ts`
    (`GET /admin/users` coleção sem paginação, policy Operate; `PATCH
    {id}/status` `{active}`, `POST/DELETE {id}/roles/{role}` e `POST
    /admin/invitations` `{email,roles[num],expiresInDays}` — tudo
    Administer). **Decisão de rota tomada**: a página usa SÓ
    `api/v1/admin/users` (prefixo v1 + controller Admin); o `RolesController`
    (`api/identity/users`, fora do padrão v1, com DELETE duplicado) fica de
    fora do front — limpeza do backend segue como observação. Enums: `roles`
    na listagem e no body do convite saem **numéricos** (1/2/3), no path da
    rota vai o **nome**; colunas sem contrato do protótipo (sessões, último
    login, status "Pendente", role única) removidas — `roles` vira chips
    múltiplos e "Criado em" vira "Atualizado em" (`updatedAt`). Gestão real
    na tela: ativar/desativar, remover/atribuir papel (409 do último admin
    aparece no banner), convite por e-mail+role. Aba Roles: catálogo
    canônico ADR-016 (enum, sem endpoint — contagem REAL por role derivada
    da lista)
  - [x] **F17-T007 `DevicesPage`** — `services/api/inventory.ts`
    (`GET /inventory/devices` → `PagedResult<DeviceDto>` com parâmetros
    reais `type`/`term`/`locationId`, teto 100/página; `GET
    /inventory/locations` resolve o `locationId` = setor; `GET
    /network/switches/{id}/ports` → `SwitchPortDto` com `linkUp`
    tri-estado; `POST /inventory/devices` `{locationId,name,type,...}` →
    201). Sem contrato (saíram): status online/atenção/offline (não há
    endpoint de estado atual — histórico de polls é por device), uptime,
    firmware, "VLAN n" (`vlanId` é Guid sem número), uplink/storage, PoE/
    speed/VLAN por porta e a lista de clientes (domínio separado); entraram
    vendor/serial e a contagem real "ativas/total" (`linkUp===true`). Filtro
    de tipo client-side sobre a página carregada (os contadores por tipo
    exigiriam N pedidos; o param `type` do endpoint fica documentado como
    caminho de escala); portas buscadas só ao abrir a aba "Por Switch"
    (1 pedido por switch, sem endpoint agregado). **`DeviceDetailPage`
    segue mock — task separada** (as tasks seguintes do grupo:
    Network/Inventário e Dashboard)
  - [x] **F17-T008 `NetworkPage`** — `services/api/network.ts`
    (`GET/POST /api/v1/network/vlans` → `PagedResult<VlanDto>` `{id,name,
    vlanNumber,description}`; `POST` `{name,vlanNumber,description?}` → 201
    com 409 pt-BR de nome/número únicos). Divergências do protótipo:
    "VLAN {id}" → **VLAN {vlanNumber}** (número 802.1Q real); `color`,
    `devices`, `subnet`, `gateway` e `dhcp` do card saíram (sem contrato).
    **Abas DHCP, Rotas e Firewall removidas — não há endpoint no backend**
    (nenhuma rota correspondente nos controllers); card ganhou a
    `description` real, filtro operacional `term` documentado no serviço e
    pager real de 100/página
  - [x] **F17-T009 `DashboardPage`** — sem endpoint próprio: montada com
    os serviços reais já criados + novo `services/api/alerts.ts`
    (`GET /monitoring/alerts?status=open|resolved|suppressed` →
    `PagedResult<AlertDto>`; `severity`/`status` em PascalCase via
    `ToString()`). Mini-stats reais: Dispositivos (`totalCount` de
    `/inventory/devices`), Alertas (`totalCount` com `status=open` +
    críticos derivados de `severity`), Tarefas (abertas = fora de
    Resolved/Cancelled/Closed, em progresso = `InProgress`). Painéis:
    dispositivos (nome/modelo), alertas abertos (5: device·check +
    mensagem), tarefas (6: título + chip de estado) e Eventos Recentes
    (`GET /admin/audit` — policy Administer: sem nível o painel avisa
    "acesso restrito" em vez de quebrar; 403 detectado por
    `ApiError.isForbidden`). Removidos sem contrato: stats de
    Clientes/Download/WiFi, Status do Sistema (subsystems), painel Gateway
    (CPU/mem/tráfego/WAN), gráfico de tráfego, Top Clients e Speed Test;
    "Ver todos" virou navegação real (/devices, /alerts, /tasks, /audit)
  - [x] **F17-T010 `NotificationsPage`** — `services/api/notifications.ts`
    (`GET /notifications/preferences?userId=` → coleção de
    `NotificationPreferenceDto`, `PUT` upsert `{userId,channel,enabled}`;
    `channel` é enum → **numérico** (1=SignalR…6=Slack)). **Aba Feed
    removida — não existe listagem de notificações** (só
    `GET {id}/recipients`, que exige um notificationId inacessível sem
    lista; pendência se F9 publicar a listagem). Semântica real de docs/07:
    **default habilitado — desligar = sem delivery**, então o estado inicial
    de cada canal é `true` e as linhas gravadas sobrescrevem. Escrita atrás
    de `useCan("operate")` (PUT é Operate; sem nível o toggle vira chip
    fixo Ativo/Desativado); coluna "Eventos" saiu (não existe no DTO)
  - [x] **F17-T011 `AlertsPage`** — reusa `services/api/alerts.ts` (T009;
    `GET /monitoring/alerts?deviceId/status/page/pageSize` →
    `PagedResult<AlertDto>`). Botão "Filtrar" morto virou segmented control
    do **parâmetro real `status`** (Todos/Abertos/Resolvidos/Suprimidos →
    vazio|open|resolved|suppressed; só mandamos os três do contrato para
    não cair no 400 pt-BR). Divergências: chip "Info" saiu (enum só tem
    Warning/Critical), `title`/`detail` inexistentes → título real
    `deviceName · check` + `message`; entraram status
    (Aberto/Resolvido/Suprimido) e `openedAt`/`resolvedAt`. Sem mutation —
    alerta nasce automático pós-check e resolve sozinho (F6), não há POST
  - [x] **F17-T012 `RemotePage`** — novo `services/api/remoteSessions.ts`
    (`GET/POST /inventory/devices/{id}/remote/sessions[?limit]`,
    `POST .../rdp`, `DELETE .../{sessionId}`; F11/ADR-013). **Sessões são
    por device — não há listagem global**, então as tabelas globais de
    ativas/histórico do protótipo saíram: a página virou seletor de
    dispositivo (reusa `listDevices` do T007; card = nome/type/ip/model,
    "online" e "lastAccess" não existem) + histórico daquele device
    (limit 50, mais recentes primeiro). Ações: Abrir SSH (credenciais do
    device) / Abrir RDP (probe TCP sem credencial, NLA = F11-T006+) /
    Encerrar — POST/DELETE são Operate (botões atrás de
    `useCan("operate")`), falha de conexão = **409 pt-BR** no banner,
    sem IP = 400 (botão desabilitado com explicação). Enums reais em
    PascalCase: protocol `Ssh`/`Rdp` (exibido SSH/RDP), status
    `Active`/`Closed`/`Failed` + `failureReason` no chip de Failed.
    Divergências: "Nova Conexão" global, IP/`user`-nome/`bytes`/duração
    pronta por linha não têm contrato (duração agora é calculada de
    `startedAt`→`endedAt`, usuário = GUID curto)
  - [x] **F17-T013 Discovery (N/A — página inexistente)** — o item do
    checklist estava impreciso: não há `DiscoveryPage` nem rota
    `/discovery` no protótipo F14 (o único "Discovery" no front é o
    `TaskKind` do TasksPage). Sem página com mock não há o que conectar, e
    criar tela nova estaria fora do escopo da F17 ("conectar páginas
    EXISTENTES com mock") — se houver desejo de telar Discovery sobre
    `DiscoveryRunsController` (GET runs, GET runs/{id}/results, POST
    trigger), isso vira task nova. **Observação relacionada**: outras
    páginas ainda em mock também ficam fora do checklist da F17
    (`LocationsPage`, `LogsPage`, `SwitchesPage`, `TopologyPage`) —
    pendência de decisão humana sobre escopo
  - [x] **F17-T014 `WiFiPage`** — **o backend não tem domínio WiFi**
    (sem entidade de rede SSID/segurança/banda/canal, sem telemetria de
    AP — clientes/CPU/mem/canais/tx — e sem POST de rede); tudo isso saiu
    (mesma decisão do T008 com as abas sem endpoint), incluindo o CTA
    "Criar Rede" (o `<Can level="operate">` do F14-T009 ficava sem ação
    por trás). Restou o que é real: a página lista os devices de
    `DeviceType.AccessPoint` via `GET /inventory/devices?type=AccessPoint`
    (parâmetro real, serviço do T007; `totalCount` com filtro + aviso de
    truncamento em 100) com card = nome/modelo/ip/vendor/serial/descrição
  - [x] **F17-T015 `ClientsPage`** — novo `services/api/discovery.ts`
    (`GET /discovery/runs` ordenado StartedAt DESC, `GET
    runs/{id}/results` CreatedAt DESC; F4-T001/T002). **Não existe
    domínio de "clientes conectados"** (telemetria de controller AP —
    sinal/velocidade/uso/SO — sem endpoint); o que existe de cliente de
    rede são os achados ARP/DHCP/MAC, e só por execução → a página mostra
    a **última execução** (`pageSize=1`) e pagina os resultados (20/pág).
    Colunas do protótipo sem contrato saíram (Sinal/Velocidade/Uso/SO/
    Conexão, botão "Filtrar" — a query não tem filtro por kind, pendência
    server-side); "Tipo" WiFi/LAN virou o `kind` real (ARP/DHCP/MAC/
    Vizinho/Host) e as chaves de `values` fornecem ip/mac/hostname/vlan/
    `local_if`. Achados negativos ("Nenhuma entrada ARP.") viram linha
    informativa em vez de sumir (remover quebraria a contagem da query).
    Subtítulo = contagem real + data/status da descoberta (errorMessage do
    run Failed aparece). Sem mutations
  - [x] **F17-T016 `InsightsPage`** — **N/A de contrato**: os três painéis
    (DPI por aplicação, Tráfego por Cliente, Speed Test) não têm domínio
    no backend (varredura: nenhum controller/entidade de DPI/tráfego/
    speed test — os mesmos gráficos já saíram do Dashboard no T009). Sem o
    que conectar e sem dado real para manter, a página foi **esvaziada com
    estados vazios honestos** ("Não implementado no backend…") em vez de
    exibir arrays fabricados como reais; rota/menu permanecem estáveis
    (feature não é deletada sem decisão humana). **Escalado**: quando
    houver produto de tráfego, decidir entre construir o backend, apagar a
    página/rota ou manter como demo — decisão humana registrada aqui e no
    PROJECT_STATE
  - [x] **F17-T017 `DeviceDetailPage`** — novo `services/api/monitoring.ts`
    (`GET /monitoring/results/{id}?limit=` → `DeviceResultsDto`
    `{check,succeeded,durationMs,error,values,checkedAt}`) + extensão de
    `inventory.ts` (`GET /inventory/devices/{id}` com 404 → tela "não
    encontrado"; `network-interfaces|ip-addresses|mac-addresses?deviceId=`
    → coleções de 100) + `deviceId` exposto em `listAlerts` (parâmetro
    real do endpoint, T011). **Todos os `*View` por tipo do protótipo
    saíram** (toner/drum, linhas de telefone, CPU/mem/discos/serviços,
    portas/volumes do NAS, bateria da UPS — telemetria de agente sem
    backend; `vm`/`nas`/`gateway` nem existem no enum `DeviceType`). A
    página virou 4 abas: **Geral** (identificação completa + checks do
    perfil vindos no próprio `DeviceDto.monitoringChecks` + localização
    resolvida por `listLocations`), **Monitoramento** (20 polls com
    `values` exibidos tipo `roundtrip_ms` + 5 alertas do device com total
    real), **Endereços** (interfaces/IPs/MACs com join local
    interfaceId→nome e aviso de truncamento) e **Portas** (só
    `DeviceType.Switch`, lazy ao abrir a aba, reusa o serviço do T007 com
    `linkUp` tri-estado). Chip Online/Offline e `department`/`firmware`/
    `uptime` saíram (sem contrato — mesma decisão do T007); abas carregam
    uma vez (ref contra duplo-effect do StrictMode)
  - [x] **F17-T018 `LocationsPage`** — `listRacks` novo em `inventory.ts`
    (`GET /inventory/racks?locationId=` → `RackDto {id, locationId, name,
    description}`); página usa 3 leituras de página única (100, clamp do
    endpoint): `listLocations` + `listRacks` + `listDevices` (esta só
    para as **contagens por local** — agrupamento local por `locationId`,
    pois não existe endpoint agregado; header mostra os `totalCount`
    reais). Saiu sem contrato: `type` do local; em racks a barra de uso
    de U (`usedUnits`/`units`), potência, temperatura e a lista de
    devices da rack (nenhuma métrica tem backend). Contagens por card
    calculadas sobre a primeira página truncada ganham `~` + rodapé
    explicando; nome do local do rack resolvido no cliente; sem mutations
    (mock já era somente leitura)
  - [x] **F17-T019 `LogsPage`** — página **já era majoritariamente real**
    (link + rastreador de Seq, ADR-021/ADR-022 com `VITE_LOGS_URL` e
    filtro `CorrelationId` — permanecem). O que saiu: a tabela mock
    "Últimos eventos (amostra local)" de `logsData` — **não existe
    endpoint de logs da aplicação** na API (níveis/fontes/mensagens são
    gravados no Seq junto com workers/outbox e a API não os expõe;
    ações de usuário auditadas têm página própria em `/audit`,
    `AuditPage` + `services/api/audit.ts`, já conectada). No lugar da
    tabela, painel honesto apontando para a plataforma de logs e para a
    Auditoria; comentários `uni-logs-table`/`uni-log-level` no CSS
    ficaram sem uso (semântica preservada caso o painel volte)
  - [x] **F17-T020 `SwitchesPage`** — `listSwitches` adicionado ao
    `services/api/network.ts` **existente** (T008, VLANs — na task o
    arquivo foi reescrito por engano e restaurado do HEAD; diff final
    verificado puramente aditivo). Fontes: `GET /network/switches`
    (F3-T001 — switch é Device com type=Switch, devolve `DeviceDto`;
    `term`/`locationId`/`rackId` reais) + localização resolvida via
    `listLocations` + `GET /network/switches/{id}/ports` por seleção
    (serviço do T007, `linkUp` tri-estado; guarda de ref anti
    duplo-fetch do StrictMode; seleção por card OU pelo `select` do
    cabeçalho, auto-seleciona o primeiro). Saiu sem contrato: chip
    Online/Offline e "X online" (estado atual — decisão T007); por card
    `activePorts/totalPorts`, `poePorts`, `firmware` (sem contagem
    agregada sem 1 requisição por switch); na tabela Dispositivo/Tipo/
    Velocidade/PoE/VLAN (`SwitchPortDto` só tem name/description/linkUp;
    VLAN por porta = `GET /network/switch-ports/{id}/vlans`, 1 pedido
    POR PORTA — futuro). Grade visual e contagens refeitas com `linkUp`
    (Up/Down/—); sem mutations
  - [x] **F17-T021 `TopologyPage`** (última página mock) — `getTopology`
    novo em `network.ts`: `GET /network/topology` (F3-T006 — nodes =
    **interfaces**, 1 device pode ter N; edges = Connections sem tipo/
    label/cor no contrato; `status` = último Ping do device dono:
    `"up"`/`"down"`/`null`). **API não devolve x/y** → grade calculada
    no cliente: devices ordenados por tipo em 7 colunas, interfaces em
    satélite (raio 30) ao redor do device, arestas neutras ligando
    interfaces com as duas pontas visíveis; zoom/hover/tooltip
    preservados (tooltip = deviceType · ip · interface · status · Local
    resolvido via `listDevices`+`listLocations`). 4 leituras em paralelo
    (topology/devices/locations/vlans). Stats e cards de categoria =
    devices distintos por tipo e por status do ping. Saiu sem contrato:
    tipos `wan`/`gateway`/`vm`/`nas`/`client` (enum real), status
    "warning", arestas tipadas backbone/PoE/WiFi/storage, painel
    Storage, badges de VLAN por device (fan-out 1 req/porta — sidebar
    mostra só o inventário de VLANs do T008, sem contagem de devices) e
    **vista por Rack** (DTO sem posição em U/rackId — racks vivem na
    LocationsPage T018). Estado honesto: "Nenhuma interface mapeada —
    a topologia nasce das Connections (F3-T005)"; sem mutations
  - Sub-checklist FECHADO (T001–T016: 14 páginas conectadas + Discovery
    N/A + Insights esvaziado); **todas as páginas do protótipo estão
    conectadas ao backend ou com estado honesto** (T016 Insights, T019
    Logs, T021 Topologia). O grupo permanece aberto apenas pela decisão
    de produto do Insights (construir backend / apagar página / manter
    demo) — última pendência da F17.
    Os gaps de backend identificados aqui viram a **frente
    F18** (subseção própria abaixo)
- [ ] **Busca global no frontend** (F2-T007, task futura declarada na própria task)
- [ ] **RDP com NLA/CredSSP (autenticação)** — adiado pela decisão do usuário na
  F11-T003 (probe de porta sem autenticação)
- [ ] **Limpar `frontend/InfraControl.Web/tsconfig.tsbuildinfo` do versionamento**
  (rastreado por engano; regra vigente = nunca stagear — levar a um cleanup
  próprio)
- [ ] **Warnings `CS8601`/`CS8605`** em `RestoreRoundTripTests.cs` no build
  Release (pré-existentes, fora de escopo até aqui)
- [x] **`MT_LICENSE` para a API/Workers subirem** <!-- FECHADA 01/10/2026:
  resolvida por **downgrade MassTransit 9.2.2 → 8.4.1** (MIT) no
  InfraControl.Infrastructure.csproj — sem env de licença; smoke real:
  containers `api`/`workers` no ar com RabbitMQ e log sem
  `License must be specified` -->
- [x] **`DockerPgToolRunner.ResolveDockerCli` só procura `docker.exe`** <!--
  FECHADA 01/10/2026: resolve por plataforma (`docker` em Linux/macOS,
  `docker.exe` em Windows) + `--add-host host.docker.internal:host-gateway`
  quando traduz `-h localhost` (Linux não define esse host por default);
  `RestoreRoundTripTests.BackupAndRestore_ShouldRecoverMarkerRowAndSchema`
  verde na suíte completa -->

### Backend: gaps levantados na F17 — frente F18 (criada em 01/10/2026)

Levantamento sistemático feito ao conectar as páginas (F17-T001…T016): o
protótipo prometia domínios que o backend **não tem**. Cada item é uma
task independente com objetivo/escopo/critérios/testes próprios (regra:
task que não cabe numa descrição clara está grande demais — dividir). Os
domínios novos (T005–T008) exigem **ADR + questionário de escopo antes do
plano** — não se implementa arquitetura de coleta sem decisão humana.

- [x] **F18-T001 Feed de notificações** — `GET /api/v1/notifications`
  (raiz do controller, nova action `GetFeed`) com `page`/`pageSize`
  (clamp 1..100). **Decisão prévia registrada: sem estado de
  leitura** — DTO entregue `{id, eventType, title, message, createdAt}`
  (`createdAt` = `CreateDateTime`), sem `ReadAt`/`POST {id}/read`
  (a task autorizou "entregar sem não lidas"; evita migração de schema;
  extensão futura documentada no DTO). Implementação: port
  `INotificationFeedRepository` (novo pacote `Notifications/Feed/`) +
  `NotificationFeedRepository` com predicado correlato `Any()` —
  destinatário direto OU role (`IN` das roles do `ICurrentUserContext`),
  **dedup por construção** (alvo direto + role = 1 linha), ordenação
  `CreateDateTime` desc com desempate por `Id` (paginação estável);
  handler `GetNotificationFeedQuery` (ADR-016 `RoleAccess.Read`, usuário
  do contexto, não da query; sem usuário → `UnauthorizedAccessException`
  pt-BR). Registrado no `BaseIntegrationTest` (a base não varre) e
  auto-registrado na API via `RepositoryRegistrar`. Testes: 5 unit
  (Sqlite — só os do feed entraram: ordenação/dedup/clamp/falha sem
  usuário), 3 integração (PagedResult + gate ADR-016 + dedup) e 2 E2E
  HTTP no `FullPipelineE2ETests` (401 anônimo + envelope
  `{isSuccess,data:{items,totalCount,page,pageSize},errors}`).
  Achado no caminho: `ApplyAuditFields` do `AppDbContext` sobrescreve
  `CreateDateTime` de toda entrada `Added` com `AppTimeProvider.Current`
  — data determinística do teste gravada em segunda passada (`Modified`
  não toca `CreateDateTime`). **Desbloqueia** a aba Feed da
  NotificationsPage (UI = task futura; F17-T010 documentou a remoção)
- [x] **F18-T002 Filtro `kind` em discovery results** — parâmetro opcional
  `kind` em `GET /discovery/runs/{runId}/results`. Implementação:
  `SearchDiscoveryResultsQuery` ganhou `Kind` (`string?`), validado no
  handler **antes** do 404 de run (`ParseKind`: case-insensitive +
  `Enum.IsDefined` rejeitando números que o `TryParse` aceitaria; nome
  inválido → `ArgumentException` → 400 pt-BR **listando os válidos**:
  "Valores válidos: Neighbor, ArpEntry, MacTableEntry, DhcpLease,
  Host"); porta/repositório `SearchByRunAsync` ganharam
  `DiscoveryResultKind? kind` com filtro aplicado **antes do COUNT**
  (totalCount respeita o filtro); action `Search` expõe `?kind=`.
  Testes: +3 unit (filtro+paginação com totalCount filtrado,
  case-insensitive, texto e número inválidos → 400 pt-BR) e +2
  integração (filtro server-side no pipeline real + 400 pt-BR).
  Validação: unit 1350, integração+E2E 171, arch 90 (todos verdes).
  **Desbloqueia** o filtro honesto da ClientsPage (pendência do
  F17-T015 — UI = task futura)
- [x] **F18-T003 Endpoint agregado de SLA budgets** — nova rota
  `GET /api/v1/sla/slos/budgets?serviceId=&page=&pageSize=` (action
  `SearchBudgetsByService` no `SlosController`; rota literal `budgets`
  sem guid não colide com `{sloId:guid}/budgets` nem
  `budgets/{id:guid}`; decisão: não criar controller `api/v1/sla/budgets`
  para não duplicar rota de budget único). Implementação:
  `SearchSloBudgetsByServiceQuery` (`ServiceId?` nulo = todos) +
  `SearchByServiceIdAsync` no port/repo (subquery nos SLOs do serviço,
  filtro **antes do COUNT**, mesma ordenação `PeriodStart` desc e clamp
  1..100 da busca por SLO; DTO idêntico `SloBudgetDto`). **Frontend**:
  `sla.ts` ganhou `listServiceBudgets` (comentário do N+1 corrigido em
  `listSloBudgets`, que fica para drill-down); `SlaPage.load` substituiu
  o loop `Promise.all` por SLO por **1 pedido agregado** — 4 requests
  fixos paralelos por visão, independentes do nº de SLOs — agrupando por
  `sloId` no cliente. Critério atendido: UI passa de N pedidos a 1 por
  visão. Testes: +1 unit (filtro por serviço com TotalCount/paginação/
  ordem/clamp; serviço alheio fora; `serviceId` nulo = todos) e +1
  integração (2 serviços em 1 pedido). Validação: unit 1351,
  integração+E2E 172, arch 90, `tsc -b` 0 erros, `npm run build` ok
- [x] **F18-T004 Limpeza do `RolesController`** — **decisão registrada:
  remover o slice inteiro** (alinhar manteria a duplicação): rota
  `api/identity/users` (fora do v1), o `RolesController` e o
  `RoleViewModel`, os comandos `AssignUserRoleCommand`/
  `RemoveUserRoleCommand`, a porta `IUserRoleRepository`, o adaptador
  `IdentityUserRoleRepository` (sync do ASP.NET Identity não é lido por
  nenhum pipeline de authz — o login monta as claims do cookie a partir
  do domínio), os `using`s órfãos em `Program.cs`/
  `DependencyInjection.cs` e as 2 entradas no allowlist do arch test.
  **Achado grave na migração dos testes**: o caminho vivo
  (`POST/DELETE /admin/users/{id}/roles/{role}` →
  `IdentityAdministrationService`) estava **quebrado em runtime** —
  `UserRoleAssignment.Id` era `ValueGeneratedOnAdd` (convenção Guid)
  mas nasce preenchido no `Entity()`, então o `DetectChanges` anexava
  a filha NOVA como `Modified` e o save estourava
  `DbUpdateConcurrencyException` (500) — nunca houve teste de rota
  (os comandos usavam repositório fake). Corrigido com
  `ValueGeneratedNever` em `UserRoleAssignmentConfiguration` (esquema
  intocado: EF confirmou "no pending model changes"). Migração dos
  testes: `IdentityAdministrationServiceTests` (5 facts) cobre os 2
  facts de `RoleCommandTests` + regressão do bug (atribuição persiste,
  desconhecido → 404, último admin → 409, remoção comum persiste).
  Docs (05/24/25) e comentários do front atualizados. Validação: unit
  1354 (1351 − 2 + 5), integração+E2E 172, **arch 88** (90 − 2 casos de
  theory por controller do `RolesController` removido — conferido no
  TRX, nada escondido), `tsc -b` 0 erros, `npm run build` ok
- [ ] **F18-T005 Domínio DHCP/Rotas/Firewall** — leitura dos serviços de
  rede prometidos pelas abas removidas no F17-T008 (leases DHCP, rotas,
  regras de firewall). **Exige ADR + questionário antes do plano**:
  fonte de dados (SNMP/SSH por vendor/agent), escopo read-only vs
  gestão, quais tabelas/vendor suportados. Critérios: definidos no ADR
- [ ] **F18-T006 Domínio WiFi** — redes (SSID/segurança/banda/canal/
  largura/VLAN) + telemetria de AP (clientes, CPU, mem, canais,
  potência, tráfego) — tudo o que saiu da WiFiPage no F17-T014.
  **Exige ADR + questionário**: fonte (controller de AP? SNMP? API de
  vendor?), quais métricas e com que frequência. Critérios: definidos
  no ADR; quando pronto, a WiFiPage deixa de ser só inventário
- [ ] **F18-T007 Domínio de tráfego/Insights** — DPI por aplicação,
  tráfego por cliente, speed test e painéis do Dashboard removidos no
  F17-T009/T016. **Exige ADR + questionário + decisão sobre a página
  Insights** (construir backend, apagar página/rota ou manter demo —
  escalado no F17-T016). Critérios: definidos no ADR
- [ ] **F18-T008 `GET /clients` (clientes ao vivo)** — lista agregada de
  clientes da rede (IP/MAC/hostname/vlan) sem depender de "último run
  de discovery" (decisão do F17-T015). **Decisão prévia**: servir
  agregando ARP/DHCP já descobertos vs. nova tabela mantida por
  ingestão contínua — pode nascer junto de F18-T005/T006. Critérios:
  contrato paginado estável; testes unit + integration
