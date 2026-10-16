# docs/53 — Plano do Módulo de Virtualização (VMs + vCenter)

> **Origem:** pedido do usuário em 2026-10-16 — "precisa criar a mesma ideia
> do F3 para as VMs; [o material atual] não menciona as VMs e o vCenter".
> **Sem ADR** no lote inicial: é mudança de domínio no monólito no mesmo
> formato da Fase 3 Network (F3 não teve ADR); a **integração vCenter API**
> (F31-T005) exige ADR própria antes de código (contrato externo +
> credenciais — precedente ADR-042/043).
> **Requisitos vêm do usuário** — nada aqui é inventado; itens não
> cobertos ficam como pendência §6.
>
> **Status (2026-10-16):** T001 concluída (commit `99510b6`), T002
> concluída (commit `7fceacc`, migração `AddVirtualMachineLinks`), T003
> concluída (commit `baf66fc` — gate UnitTests 2564/2564, Arch 154/155)
> e T004 concluída (página front — gate vitest 270/271 com flake
> KnowledgePage pré-existente isolado verde, tsc 0, eslint 0). Lote 1
> fechado; T005 (integração vCenter API) retida na ADR — rascunho
> **ADR-058** redigido, aguardando aprovação.

---

## 1. Objetivo e Motivação

Hoje o InfraControl **não tem VM nem vCenter em lugar nenhum**:

- `DeviceType` cobre só equipamentos (Other/Server/Workstation/Switch/
  Router/Firewall/AccessPoint/Printer/Camera/Phone/UPS) — uma VM que atua
  como servidor só pode ser cadastrada "disfarçada" de `Server`, sem
  distinguir física de virtual;
- as fichas técnicas `docs/42..49` cobrem equipamentos físicos;
- a única menção a virtualização é o checklist do plano Zabbix
  (`docs/37 §8.2` — métricas via template VMware, Fase 8 daquele plano) —
  que é **monitoramento**, não inventário/relações.

A **Fase 31** segue a MESMA ideia da Fase 3 Network: começar pelo domínio
(tipos + relações + CRUD), uma task por vez com TDD, visão agregada ao
final — e integração externa só numa fase própria, como F3 (sem
integração) vs. F20/F26/F27 (integrações depois).

## 2. Decisões (kickoff 2026-10-16)

| # | Decisão |
| --- | --- |
| D1 | **Domínio + CRUD primeiro; integração vCenter API depois** (ritmo F3 → F20/F26/F27). Sincronização/atributos ao vivo ficam na T005 com ADR. |
| D2 | **VM = `DeviceType.VirtualMachine` (11)** e **vCenter = `DeviceType.VCenter` (12)** — tipo próprio, sem entidade nova (reuso do fluxo Device, igual `Switch = Device type=Switch` no F3-T001). |
| D3 | **VM "entra na categoria de servidores"**: `DeviceProfileDefaults` de `VirtualMachine` e `VCenter` = perfil de `Server` (`[Ping, SnmpSystem]`) — física/virtual só muda o TIPO, o monitoramento é o de servidor. |
| D4 | **Vínculos em coluna**: `Device` ganha `HypervisorDeviceId` e `VCenterDeviceId` (`Guid?`, FK self → devices, RESTRICT) — 1 hypervisor e 1 vCenter por VM; a FK self evita tabela de junção (F3-T004 usou junção porque era N:N; aqui é 1:1). |
| D5 | **Atributos de VM** (power state, vCPU/RAM, guest OS) **não entram no lote 1** — são ao vivo/voláteis e pertencem à integração (T005); inventário primeiro. |
| D6 | **Escopo de plataforma: VMware/vCenter** (pedido literal "vCenter"). Hyper-V/Proxmox ficam fora — se entrar depois, é decisão nova. |

## 3. Modelo de domínio (delta do docs/03)

```text
DeviceType:  ... UPS = 10 │ VirtualMachine = 11 │ VCenter = 12   (NOVOS)

Device (devices)
 ├─ type = VirtualMachine ──► HypervisorDeviceId ──► Device (hypervisor)
 │                            VCenterDeviceId    ──► Device (type=VCenter)
 ├─ type = VCenter       (gerenciador — alvo de vínculo de VMs)
 └─ demais tipos         (intocados)

Guards (domínio, pt-BR):
 • hypervisor alvo: existe, ≠ si mesmo, ≠ VirtualMachine (VM não hospeda VM)
 • vCenter alvo: existe e type = VCenter
 • DeleteDevice com devices apontando para ele → 409 (molde HasPortsAsync)
```

## 4. Fatiamento (Fase 31)

1. **F31-T001 — VMs e vCenter no inventário**: enum + perfis default (D2/D3);
   queries dedicadas `SearchVirtualMachinesQuery`/`SearchVCentersQuery`
   delegando ao fluxo de devices com type fixo (molde `SearchSwitchesQuery`
   do F3-T001) + `GetVirtualMachineQuery` com guarda de tipo; rotas
   `GET api/v1/inventory/virtual-machines` e `/vcenters` (InventoryRead);
   docs/03 + docs/53; TDD.
2. **F31-T002 — Vínculos VM→hypervisor→vCenter**: colunas + migração
   (sem `--no-build`), setters/comandos `Link`/`Unassign` com guards D4,
   `DeleteDevice` 409, queries "VMs do host"/"VMs do vCenter"; TDD
   (guards 400/404 pt-BR, 409, migração).
3. **F31-T003 — Visão de virtualização (backend)**: query agregadora estilo
   F3-T006 — contagens de VMs por hypervisor/vCenter e **órfãs** (sem
   vínculo), listagem com nomes resolvidos; `GET /inventory/virtualization`.
4. **F31-T004 — Front: página Virtualização**: índice no padrão `EntityView`
   (tabela/quadro/grid), colunas host/vCenter, contagens e órfãs visíveis;
   testes de render.
5. **F31-T005 — Integração vCenter API (FUTURA, fora do lote)**: settings +
   client + sync read-only no padrão F26/F27, atributos ao vivo da VM.
   **Exige ADR aprovada antes** (TDD só depois do ADR) — rascunho
   redigido em `adr/ADR-058-VCENTER-INTEGRATION-READONLY.md`
   (2026-10-16, status "rascunho", aguardando aprovação).

## 5. Fora de escopo desta fase

- Métricas VMware via Zabbix — `docs/37 §8.2` (Fase 8 daquele plano), não aqui.
- Power state/vCPU/RAM/guest OS (D5 — T005).
- Hyper-V, Proxmox, Nutanix (D6).
- Modelo de cluster/datastore/resource pool (não pedido — pendência §6 se
  o operador pedir).

## 6. Pendências de decisão

1. **Cluster/datastore**: VM em VMware pertence a um cluster e datastore —
   modelar se/ quando o operador pedir (não pedido no kickoff).
2. **VM duplicada**: se a T005 sincronizar, evitar cadastro duplo vs. VM
   já criada manualmente (critério `docs/37 §8.2` "evitar cadastrar duas
   vezes") — identificador estável (MoRef/UUID) é decisão da T005/ADR.
3. **vCenter como Device**: manter `VCenter` como tipo de Device ou virar
   settings de integração na T005? (mantido como Device no lote 1 por D1;
   a T005 decide se herda o Device ou o settings.)

## 7. Sequência e critérios

```text
T001 (tipos+perfil+listagem) → T002 (vínculos+migração)
→ T003 (agregado backend) → T004 (front) → [checkpoint]
→ T005 (integração) SÓ com ADR aprovada
```

Critérios globais: TDD com RED antes do GREEN; suíte completa verde
(UnitTests + Arch, falha nginx conhecida = baseline); docs/03 atualizado
na task que mudar domínio; commit por task com autorização; push nunca
automático.
