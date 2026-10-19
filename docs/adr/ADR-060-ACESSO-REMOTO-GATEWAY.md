# ADR-060: Acesso remoto — gateway Guacamole para RDP/SSH com SSO pela plataforma

- **Status**: **aceita** — aprovada pelo usuário em 2026-10-17 (F34-T001); autoriza o código das Fases 2–7 do plano (execução sequencial, uma task por vez)
- **Data**: 2026-10-17
- **Contexto**: Plano de acesso remoto (Linux e Windows) aprovado pelo
  usuário em 2026-10-17 (7 fases). As decisões D1–D5 foram escolhidas
  pelo usuário no chat de 2026-10-17 e a aceitação formal ocorreu no
  mesmo dia (F34-T001). Tarefas em `TASKS.md` → Fase 34.

## Contexto — Fase 0: estado atual confirmado no código (2026-10-17)

| Item | Estado verificado |
|---|---|
| Acesso SSH | **F11-T002 entregue** — `SshRemoteProvider` (SSH.NET 2026.0.0), `OpenSshSessionCommand`/`CloseRemoteSessionCommand`, sessão em `RemoteSession` |
| Acesso RDP | **F11-T003 entregue como probe** TCP/X.224 **sem autenticação** — NLA/CredSSP é pendência explícita no TASKS ("adiado pela decisão do usuário"); **sessão RDP real não existe hoje** |
| Credential Store | **F11-T006 entregue** — tabela `device_credentials`, AES-256-GCM com chave só em env; usada **apenas** pelo OpenSsh |
| Permissões | **F11-T004 matriz completa** (`device.ssh.connect`, `device.rdp.connect`, `credential.use`, `credential.manage`) — **`credential.use` não está consumida** (documentado no F11-T006) |
| Guacamole | **ausente** de `src/` e `docs/` (0 ocorrências) — tecnologia nova |
| MFA | TOTP na plataforma (ADR-019/F1-T016a) — **hospital tem AD e não tem MFA** |
| Zabbix | agente Linux modelado (doc 49); agente Windows não modelado |
| Inventário | `DeviceType.Workstation`/`Server` existem; **`Device` não tem campo de sistema operacional** — gap real para o botão da Fase 4 |

### Perguntas da Fase 0 — respostas (2026-10-17)

1. Repositório completo com backend? **Sim** (F11 íntegro; F30 verdes).
2. Inventário Windows/Linux por setor? **Pendente** — dados do hospital, fora do repositório (F34-T000).
3. Active Directory e MFA? **AD sim, sem MFA** → MFA do gateway será o TOTP da plataforma (D2).
4. Gravação de sessões? **Permitida** → D3 grava RDP e SSH; retenção/descarte com o LGPD (Fase 6).
5. Quem opera o gateway? **O usuário** → operador único; procedimentos de atualização/backup/bloqueio são obrigatórios (Fase 6).

## Decisão

### D1 — Guacamole para RDP **e** SSH

Gateway único no navegador para os dois protocolos (opção escolhida
2026-10-17; era a que a comparação favorecia). O SSH nativo F11-T002
**permanece** para probes/validações e pré-verificação — quem transporta
a sessão interativa é o Guacamole. Motivo: experiência única, um único
fluxo de gravação/auditoria (D3), um único alvo de rede a proteger.

### D2 — SSO pela plataforma (MFA no login da plataforma)

O técnico se autentica **na plataforma** (MFA TOTP obrigatório —
infraestrutura ADR-019 já existe); a plataforma cria a conexão no
Guacamole via API server-to-server e entrega ao navegador apenas o
ticket de sessão do gateway (header autenticado; mecanismo exato do
Guacamole — header auth — validado no spike da F34-T002). Assim:

- hospital **sem MFA no AD** é coberto pelo TOTP da plataforma;
- 1 única identidade/1 credencial ("uma fonte única");
- revogação = plataforma revoga → conexão encerrada (Fase 6).

### D3 — Gravação de sessão: **RDP e SSH**

Hospital permite (resposta 4). Gravação é feita pelo próprio Guacamole
(extension de recording). **Retenção, acesso ao material e descarte:**
definidos com o responsável LGPD na Fase 6 (F34-T008) — ainda sem data.

### D4 — Credencial de equipamento: plataforma é a única fonte

Fluxo: plataforma abre conexão no gateway **server-to-server**, injetando
a credencial vinda do Credential Store F11-T006 (por dispositivo,
cifrada). O navegador **nunca** vê credencial; nada de credencial
digitada manualmente é guardado pelo gateway (contas do gateway
serviço-only, nunca de administrador de equipamento).

### D5 — Probe RDP mantido como pré-verificação

O probe TCP/X.224 (F11-T003) roda **antes** de acionar o gateway e
gera as mensagens claras da Fase 4 ("equipamento indisponível",
"protocolo bloqueado"). NLA/CredSSP continua pendência própria (TASKS).

## Consequências

- **Novo container-sistema** no Compose (padrão ADR-022): gateway +
  guacd + banco próprio do gateway; rede restrita, destinos alcançáveis
  só a partir do gateway (regra de firewall por fase 2).
- **Banco próprio do gateway** entra no backup/restauração do projeto,
  com teste de restauração (Fase 2 e Fase 6).
- **HTTPS** no reverse proxy existente (`docker/nginx.conf`); segredos
  fora do código (env — convenção vigente).
- **Kill switch**: um passo único desativa todo o acesso remoto —
  testado em ambiente de teste (Fase 2).
- **`Device` precisa de campo de SO** para o botão "Acesso remoto"
  (Fase 4) — modelagem nova (coluna + migração) decidida na task.
- **Operador único** (usuário): risco de ponto único — checklist de
  atualização/backup/bloqueio obrigatório antes da expansão.
- Pilot: **1 Windows + 1 Linux**, fora das VLANs de produção no caso
  Windows (escopo da Fase 3).

## Pendências (não decididas aqui)

- Inventário real (setores/IPs/agente) — F34-T000 (hospital).
- Retenção/descarte de gravação — com LGPD (F34-T008).
- Agente Zabbix Windows — só se o levantamento mostrar necessidade (Fase 5, docs/49).
- NLA/CredSSP no probe — pendência já registrada no TASKS (inalterada).

## Referências

- `docs/10-REMOTE-ACCESS-OBSERVABILITY-SECURITY.md` (matriz F11-T004, regras "nunca expor credenciais ao frontend", "preferir VPN/rede interna")
- TASKS.md F11-T001…T006 (RemoteSession, providers, permissões, audit, Credential Store)
- ADR-019 (MFA TOTP), ADR-022 (Docker/Compose), ADR-048 (referência externa — Zabbix)
- TASKS.md Fase 34 (tarefas deste plano)
