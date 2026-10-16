# ADR-057: Integração vCenter — inventário e atributos de VM via vSphere Automation API (read-only)

- **Status**: **rascunho — aguarda aprovação humana antes de qualquer código** (F31-T005)
- **Data**: 2026-10-16
- **Contexto**: Fase 31 (docs/53). O lote 1 (T001–T004) fechou domínio,
  vínculos, agregado e front; a F31-T005 (integração com a API do vCenter)
  está condicionada por docs/53 §4.5 a uma ADR aprovada. Este documento
  também resolve as pendências de decisão de docs/53 §6 (identidade
  anti-duplicação e "vCenter como Device").

## Contexto

A plataforma já cadastra VMs, hypervisors e vCenter como `Device`
(F31-T001: `DeviceType.VirtualMachine`/`VCenter`) e vínculos 1:1 via FK
self (F31-T002: `hypervisor_device_id`/`vcenter_device_id`), mas o
cadastro é **manual** e os atributos de operação da VM (power state,
vCPU/RAM, guest OS — docs/53 D5) não existem em lugar nenhum do domínio.
A F31-T005 (docs/53 §4.5) prevê "settings + client + sync read-only no
padrão F26/F27, atributos ao vivo da VM" — e `Device` não tem esses
campos hoje.

O precedente do produto para integração externa está no F26/F27:
`UniFiIntegrationSetting` (linha única, senha cifrada), `UnifiSyncWorker`
(intervalo com clamp, trava única, timeout, upsert-only) e `UniFiSyncRun`
(rastreabilidade de cada execução). ADR-048 fixa a camada de integração;
ADR-051 mostrou o formato de sobrepôr uma restrição **com salvaguardas** —
aqui a postura é a mesma, porém mantendo a integração **somente leitura**.

## Decisão

### 1. Escopo — somente leitura, explícito

| Permitido na T005 | Proibido neste ADR |
|---|---|
| Listar VMs e hosts do vCenter | Ligar/desligar, criar, apagar, clonar VMs |
| Ler power state, vCPU/RAM, guest OS | Qualquer escrita na API do vCenter |
| Sincronizar inventário e vínculos locais | Executar comandos no guest |
| Ler estado de conexão dos hosts | vMotion / reconfig de hardware |

Toda escrita futura na API do vCenter exige **novo ADR** (mesma lógica
que levou do ADR-048 ao ADR-051). Quem escreve é sempre o operador local;
o sync nunca escreve no vCenter.

### 2. Contrato — vSphere Automation API (REST)

- **Base**: `https://{vcenter}/api` — **vCenter 7.0 Update+** exigido pelo
  namespace `/api/vcenter/*` (validar a versão-alvo no spike, §8).
- **Sessão**: `POST /api/session` (token `vmware-api-session-id`);
  fallback documentado `POST /rest/com/vmware/cis/session` para 6.5–6.7.
- **VMs**: `GET /api/vcenter/vm` (lista `Summary`: `vm`, `name`,
  `power_state`, `cpu_count`, `memory_size_MiB`, `guest_OS`) e
  `GET /api/vcenter/vm/{vm}` (detalhe).
- **Power**: `GET /api/vcenter/vm/{vm}/power/state`.
- **Hosts**: `GET /api/vcenter/host` (identidade + `connection_state`).
- **Timeout HTTP**: 30s (mesmo teto da escrita UniFi, ADR-051 §3).
- **Rate limit**: o vCenter limita requests REST concorrentes (VMware
  "Endpoint Limits for Concurrent REST Requests") — o sync é sequencial e
  single-flight por construção (§5).

Os campos exatos e a correlação VM→host (host MoRef/IPs do guest) são
**item de spike** (§8): qualquer correção de contrato atualiza ESTE
documento antes do código seguir. Nada de payload inventado.

### 3. Configuração e credenciais

- Nova entidade `VCenterIntegrationSetting` — **linha única (singleton)**
  por settings, espelho byte a byte de `UniFiIntegrationSetting`:
  `BaseUrl`, `Username`, `PasswordEncrypted` (AES-GCM via
  `ICredentialProtector`, F11-T006), `AllowInvalidTlsCertificate`
  (default **false** — true só para lab, com aviso na UI).
- Enquanto não existir linha, fallback de env `Integrations:VCenter`
  (precedente F20/UniFi).
- A senha **nunca** aparece em DTO, log ou resposta (REGRA 8 / ADR-048);
  gravação exige antiforgery (ADR-017).

### 4. Identidade e anti-duplicação (resolve docs/53 §6.2 / docs/37 §8.2)

Critério de produto: "evitar cadastrar a mesma VM duas vezes por
mecanismos de descoberta diferentes" (docs/37 §8.2).

- Nova coluna `external_ref` em `devices` (nullable, **índice único
  parcial**): guarda o identificador estável do vCenter — MoRef `vm-…`
  nas VMs e `host-…` nos hosts.
- Ordem de matching do sync:
  1. `external_ref` igual → **update** (vínculos + runtime + IP);
  2. senão nome global único (case-insensitive, pré-existente) com o
     mesmo tipo → **adoção**: grava `external_ref` e complementa
     vínculos — a VM cadastrada manualmente vira a mesma VM sincronizada;
  3. senão → **create**.
- Colisão de rename (vCenter renomeou para um nome já usado localmente):
  erro **pt-BR** registrado no run (fail-soft) — nunca sobrescrita
  silenciosa.
- **O sync não renomeia** devices existentes: o nome local é do operador;
  mudanças de nome no vCenter não se propagam (alternativa considerada e
  rejeitada — ver Alternativas §5).

### 5. Sincronização — padrão F26/F27

- `SyncVCenterInventoryCommand` + handler **upsert-only**: falha
  transitória **nunca apaga** dados do run anterior (regra UnifiSyncWorker).
- `VCenterSyncWorker : BackgroundService`, espelho de `UnifiSyncWorker`:
  `VCenterSync:Enabled`, `VCenterSync:IntervalMinutes` com clamp
  **5min–24h**, trava atômica (sem execuções simultâneas), timeout por
  execução (2min) e chamada **direta** ao handler (sem pipeline MediatR —
  a AuthorizationBehavior exige usuário HTTP autenticado).
- `VCenterSyncRun` (espelho `UniFiSyncRun`): início/fim/sucesso/contagens
  (criados, atualizados, adotados, erros)/erro — base do histórico no
  dashboard.
- Gatilho manual síncrono além do worker, para o operador validar a
  conexão (mesmo padrão do sync UniFi/Zabbix).
- Devices **criados pelo sync** vão para uma Location dedicada
  auto-criada `Virtualização` (espelho de `UniFiLocationName = "UniFi"`,
  decisão do usuário 03/10/2026). Devices já existentes (inclusive VMs
  manuais adotadas) **mantêm** a localização e o perfil do operador.
- O sync só escreve nos campos do escopo §1: identidade externa,
  vínculos (`hypervisor_device_id`/`vcenter_device_id`), IP e runtime
  (§6). Localização, descrição, checks e demais campos operacionais são
  do operador.

### 6. Atributos de VM (docs/53 D5) — onde vivem

- Nova tabela **`vm_runtime_states`**, 1:1 com `devices`
  (`device_id` PK/FK, `power_state`, `cpu_count`, `memory_mb`,
  `guest_os`, `captured_at_utc`) — escrita **apenas** pelo sync (upsert),
  nunca por CRUD.
- Leitura: a visão da F31-T003 ganha os campos de runtime com **"última
  leitura em …"** datada. vCenter de pé = dado fresco; vCenter fora do ar
  = últimos valores **com data** — nunca métrica sem procedência (mesmo
  princípio do S5b: não inventar).
- Por que não em `Device`: campos voláteis que mudam a cada sync
  poluiriam o inventário operacional e o agregado; por que não só live
  sob demanda: 1 request por VM por renderização e dependência total do
  vCenter de pé (Alternativas §6).

### 7. vCenter como Device (resolve docs/53 §6.3)

- **Mantém** `DeviceType.VCenter` (D2): o vCenter é um device do
  inventário — endereçável, monitorável (perfil D3) e listável como os
  demais. NÃO vira settings.
- `VCenterIntegrationSetting` ganha `VCenterDeviceId : Guid?` **obrigatório
  na prática**: o operador cria o Device `VCenter` (fluxo T001) e aponta o
  settings para ele; o sync usa esse id como `vcenter_device_id` das VMs
  sincronizadas. Sem esse vínculo o sync recusa com erro pt-BR (400 na
  validação / run Failed) — não há "vCenter implícito" inventado pelo
  sistema.

### 8. RBAC, auditoria e segurança

| Controle | Decisão |
|---|---|
| Settings (get/save/test) | `RoleAccess.Administer` + antiforgery (espelho UniFi settings) |
| Sync manual | `RoleAccess.Administer` |
| Leitura da visão | `Read` (já em vigor desde a T003) |
| Credencial | AES-GCM (`ICredentialProtector`), fora de DTO/log |
| TLS | `AllowInvalidTlsCertificate` default false |
| Execução | Worker single-flight + timeout; fail-soft (erro no run, dados anteriores intactos) |
| Escrita no vCenter | **Proibida** por este ADR (§1) |

### 9. Consequências

- **Positivo**: inventário de VMs automático com anti-duplicação; power
  state/vCPU/RAM/guest OS visíveis e datados; vínculos VM→host→vCenter
  preenchidos sem digitação; zero escrita externa.
- **Negativo**: superfície de dependência nova (versão do vCenter, rate
  limit, segredos); 2 tabelas novas (`vm_runtime_states`,
  `vcenter_integration_settings`) + coluna `external_ref` (+ migração).
- **Risco aceito**: drift de contrato entre versões do vCenter — mitigado
  pelo spike (§8) + fail-soft (o sync falha sem corromper dados locais);
  devices criados pelo sync podem ser recriados no run seguinte se
  apagados localmente enquanto existirem no vCenter (comportamento
  idêntico ao UniFi F26 — quem "possui" o objeto sincronizado é o
  vCenter; para manter uma VM fora da plataforma, excluí-la no vCenter ou
  desligar o sync).

## Alternativas consideradas

1. **SOAP (vSphere Web Services API)** — cobertura total (inclui
   `runtime.host`), mas SDK .NET pesado e curva de manutenção alta;
   fica como **fallback documentado** se o spike provar que o REST não
   cobre a correlação VM→host (§8).
2. **Zabbix templates VMware (docs/37 §8.2)** — resolve métricas, não
   inventário/atributos de gestão; é fase própria do docs/37 e continua
   fora do escopo da F31.
3. **SSH/PowerCLI no vCenter** — proibido (ADR-048; credenciais em risco).
4. **Não fazer** — status quo: VMs manuais, sem power state e duplicação
   por descoberta (exatamente o que docs/37 §8.2 manda evitar).
5. **Propagar rename do vCenter** — rejeitado: o nome local é identidade
   de apresentação do operador (títulos, buscas, Knowledge); renomear
   silenciosamente quebraria referências. Divergências ficam por conta do
   `external_ref`.
6. **Atributos live sem tabela** — 1 req/VM por renderização + cego sem
   vCenter; descartado em favor de `vm_runtime_states` datada (§6).

## Itens a validar no spike (T005 etapa 0 — antes do código seguir)

- [ ] Versão-alvo do vCenter (≥ 7.0u?) e namespace disponível (`/api` vs `/rest`).
- [ ] Contrato real: session, `GET /api/vcenter/vm` (campos exatos), detalhe, hosts.
- [ ] Correlação VM→host pelo REST; se ausente, acionar fallback SOAP (§Alternativas 1) e **revisitar este ADR**.
- [ ] Comportamento de rename/exclusão no matching (casos do §4).
- [ ] Rate limit/timeout com o inventário real (quantidade de VMs).
- [ ] Resultado do spike **atualiza este ADR**; status só vira "aceito"
      com aprovação humana — até lá, F31-T005 sem código.

## Referências

- docs/53 (D1–D6, §4.5, §6) · docs/37 §8.2 · docs/03 (tipos e vínculos)
- ADR-048 (camada de integração) · ADR-051 (precedente: escopo mínimo +
  salvaguardas) · ADR-016 (RBAC) · ADR-017 (antiforgery)
- F26/F27: `UniFiIntegrationSetting`, `UnifiSyncWorker`, `UniFiSyncRun`,
  `SyncUnifiInventoryCommand` (location dedicada auto-criada)
- vSphere Automation API: `/api/session`, `/api/vcenter/vm`,
  `/api/vcenter/host` (validar no spike)
- VMware KB/blog: "Endpoint Limits for Concurrent REST Requests from
  vCenter APIs"
