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
- [ ] F3-T006 Topology  <!-- prioridade baixa para o problema atual -->

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
- [ ] F5-T008 Recovery  <!-- volta a responder → fecha alerta -->

## Fase 6 — Alerts/Incidents

- [ ] F6-T001 Alert  <!-- prioridade: device down, toner < 20%, drum < 10% (thresholds do perfil) -->
- [ ] F6-T002 Alert Rules  <!-- consecutive failures, dependência de upstream, anti alert-storm -->
- [ ] F6-T003 Incident
- [ ] F6-T004 Correlation
- [ ] F6-T005 Root Cause
- [ ] F6-T006 Recovery

## Fase 7 — Tasks

- [ ] F7-T001 Task
- [ ] F7-T002 TaskAssignee
- [ ] F7-T003 TaskDevice
- [ ] F7-T004 TaskIncident
- [ ] F7-T005 TaskComment
- [ ] F7-T006 TaskHistory
- [ ] F7-T007 Task Workflow

## Fase 8 — SLA/SLO

- [ ] F8-T001 Service
- [ ] F8-T002 SlaPolicy
- [ ] F8-T003 TaskSla
- [ ] F8-T004 TaskSlaPause
- [ ] F8-T005 Slo
- [ ] F8-T006 SloMeasurement
- [ ] F8-T007 SloBudget

## Fase 9 — Notifications

- [x] F9-T001 Notification  <!-- docs/07 ADR-012: entity Notification (EventId único p/ idempotência visual, EventType, Title≤200, Message≤1000, audit fields) + NotificationConfiguration (índice único event_id) + migração AddNotifications; fluxo completo docs/06: OutboxDispatchHandler<TEvent> (handler MediatR aberto — todo domain event do AppDbContext vira OutboxMessage na mesma transação, ADR-005) → OutboxDispatcherWorker (já existia) → RabbitMQ → NotificationEventConsumer (UserCreated/UserDisabled/UserRoleAssigned/UserRoleRemoved/InvitationAccepted; idempotência via IInboxStore EventId+Consumer "NotificationConsumer"; logs Information; consumidor registrado no MassTransit do Workers); payload outbox com enums string (OutboxJson); destinatários F9-T002, canais F9-T004+ -->
- [ ] F9-T002 Recipient
- [ ] F9-T003 Preferences
- [ ] F9-T004 SignalR
- [ ] F9-T005 Email
- [ ] F9-T006 Telegram
- [ ] F9-T007 WhatsApp
- [ ] F9-T008 Webhook
- [ ] F9-T009 Retry/DLQ

## Fase 10 — Knowledge

- [ ] F10-T001 Category
- [ ] F10-T002 Article
- [ ] F10-T003 Publishing
- [ ] F10-T004 Relationships

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
- [x] F14 polish UI (Health no sidebar, chunks vendor/redux no Vite, accent unificado)
