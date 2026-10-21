# 62 — F34-T000 · Fase 0: inventário e políticas do hospital

> **Task**: `F34-T000` (Fase 34 — acesso remoto, ADR-060). Este documento **é** o
> entregável da task: "documento de estado atual com cada item marcado
> confirmado/pendente e as perguntas respondidas".
> **Gate**: destrava `F34-T004` (piloto Linux) → `F34-T009`. Enquanto houver
> ⛔ nas seções 1–6, o piloto não começa (regra de fase sequencial do PROJECT_STATE).
> **Preenchimento**: operador/usuário — são dados do hospital, fora do repositório.
> A IA montou a estrutura (2026-10-21); as confirmações são humanas.
>
> Referências: `docs/adr/ADR-060-ACESSO-REMOTO-GATEWAY.md` (decisões D1–D5) ·
> `docs/57-ACESSO-REMOTO-OPERACAO.md` (operação do gateway) · `TASKS.md` (Fase 34).

**Como preencher**: troque ⛔ por ✅ quando confirmar; o que não souber, deixe ⛔ e
descreva na linha de observação. Pendência assumida explicitamente (com data e
responsável) também encerra a task.

## Status resumo

| # | Item | Estado |
| --- | --- | --- |
| 1 | Inventário Windows/Linux por setor | ⛔ pendente |
| 2 | IP/MAC confiáveis | ⛔ pendente |
| 3 | Linux: chave SSH + política de senha | ⛔ pendente |
| 4 | Windows: RDP de entrada (piloto) | ⛔ pendente |
| 5 | Técnicos + MFA (TOTP, D2) | ⛔ pendente |
| 6 | LGPD: responsável + retenção (D3) | ⛔ pendente |

## 1. Inventário por setor (Windows/Linux)

| Setor | Windows (qtd) | Linux (qtd) | Servidores | Observações |
| --- | --- | --- | --- | --- |
| _preencher_ | | | | |

Máquinas do **piloto** (D1: 1 Windows **fora das VLANs de produção** + 1 servidor Linux):

- Linux (nome, IP, setor): ___
- Windows (nome, IP, setor): ___

## 2. Endereçamento confiável (IP/MAC)

Por que importa: a associação **sessão ↔ equipamento** (F34-T007) e o **Zabbix na
ficha** (F34-T008) dependem de identificação estável; IP dinâmico sem MAC confiável
derruba o pareamento.

| Equipamento (piloto) | IP | MAC | IP estável? | Observações |
| --- | --- | --- | --- | --- |
| _preencher_ | | | ☐ sim ☐ não | |

## 3. Linux — acesso SSH e contas (base do F34-T004)

- [ ] Servidor do piloto aceita autenticação por **chave SSH** (ou será preciso
      provisionar — descrever como o hospital permite: ___)
- [ ] Conta de serviço **dedicada, mínimo de privilégio**, disponível ou aprovada
      para criação: ___
- [ ] Política de senha vigente descrita (complexidade/expiração): ___
- [ ] Porta SSH (padrão 22/custom): ___

A chave ficará **só no Credential Store** (F11-T006, cifrada AES-256-GCM); o
navegador nunca vê credencial (critério do T004).

## 4. Windows — RDP de entrada (base do F34-T005)

- [ ] Edição do Windows do piloto **permite RDP de entrada** (Pro/Enterprise —
      **Home não permite**). Modelo/edição: ___
- [ ] RDP será ativado com **NLA obrigatório** (probe RDP F11-T003 pré-verifica a
      porta/protocolo antes da sessão real — D5)
- [ ] Porta 3389 restrita à rede necessária (regra de firewall): ___
- [ ] Política de ativação aprovada por: ___ / data: ___

## 5. Técnicos e MFA (decisão D2)

- [ ] Lista dos técnicos com acesso inicial (usuário AD de cada um): ___
- [ ] Confirmado o cenário: hospital **AD sem MFA** → MFA = **TOTP na plataforma**
      (ADR-019/F1-T016a); cada técnico ativa no primeiro acesso
- [ ] Escopo inicial: operador único = usuário (resposta 5 da ADR-060), ou lista
      acima

## 6. LGPD — gravação de sessões (decisão D3)

A ADR-060 já confirmou que **grava** (RDP e SSH). Falta a política — sem ela,
`F34-T008` (governança) não fecha.

- [ ] Responsável LGPD identificado: ___ · reunião/data: ___
- [ ] Retenção das gravações (proposta → aprovação): ___ dias
- [ ] Descarte (automático/manual): ___
- [ ] Quem pode acessar o material gravado: ___

## 7. Perguntas objetivas (critério de fechamento da task)

1. Relação de Windows/Linux por setor pronta? ___
2. Quais máquinas têm IP/MAC confiáveis? ___
3. Aceitam chave SSH? Qual a política de senha? ___
4. MFA: técnica confirmada (TOTP) e lista de técnicos pronta? ___
5. Responsável LGPD e retenção definidos? ___
6. Windows do piloto validado para RDP de entrada (edição + política)? ___

## 8. Pré-requisitos operacionais antes do piloto (lado do gateway)

Itens de infraestrutura já conhecidos (pendências da F34-T002), responsáveis pelo usuário:

- [ ] **Firewall de egress do guacd no HOST**: guacd → apenas destinos SSH (22) e
      RDP (3389) da rede do hospital; nada mais
- [ ] `docker-compose.production.yml` ainda **sem** profile `remote` — montar antes
      de piloto fora do dev

## 9. Encerramento

- Data do preenchimento: ___ · por: ___
- Pendências assumidas (se houver): ___

**Próximo passo com ✅ nas seções 1–6**: `F34-T004` (piloto Linux) e, em seguida,
`F34-T005` (piloto Windows) — o restante da fase (T006–T009) depende dos pilotos.
