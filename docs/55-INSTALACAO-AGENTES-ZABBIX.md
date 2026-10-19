# 55 — Instalação de agentes Zabbix (Windows e Linux) e sincronização

> **Escopo**: como instalar o agente Zabbix em máquinas Windows e Linux e sincronizá-lo com o
> servidor Zabbix (passivo e ativo). Baseado em operação real executada em 2026-10-17 no
> computador `HSDS_SUPTI666` (Windows 11) e no ambiente Docker deste projeto.
>
> Relacionado: `27-INTEGRACAO-ZABBIX.md` (§7 infraestrutura), `37-F27-MONITORAMENTO-ZABBIX.md`.

---

## 1. Conceitos — como o agente se sincroniza com o Zabbix

O "batimento" entre agente e servidor acontece por **dois caminhos independentes**:

| Modo | Direção da conexão | Porta padrão | Config no agente | Exigência |
| --- | --- | --- | --- | --- |
| **Passivo** | Servidor → agente | `ListenPort` (10050) | `Server=` (IPs permitidos) | Host cadastrado no frontend com interface "Zabbix agent" + IP/porta corretos |
| **Ativo** | Agente → servidor | 10051 (`ZBX_LISTENPORT` do server) | `ServerActive=` e `Hostname=` | `Hostname` do agente **idêntico** ao nome do host no frontend |

- **Passivo**: o servidor agenda coletas e conecta no agente a cada intervalo do item.
- **Ativo**: o agente agenda as próprias coletas e envia tudo ao servidor — sobrevive a NAT/firewall
  que bloqueie conexões de entrada; recomendado quando o servidor não alcança a máquina.

### 1.1 Contrato de configuração (o que precisa bater)

```ini
Server=192.168.11.121,<IP-ou-subnet-do-servidor>   # quem PODE coletar (passivo)
ServerActive=192.168.11.121:10051                  # para onde ENVIAR (ativo)
Hostname=NOME-EXATO-DO-HOST-ZABBIX                 # identidade (ativo)
ListenPort=10050                                   # porta de escuta (padrão 10050)
```

Regras de ouro:

1. `Hostname` = nome do host no Zabbix **caractere por caractere** (sem DNS; string literal).
2. `Server`/`ServerActive` devem conter o **IP do servidor Zabbix como visto pelo agente**.
   Em Docker Desktop, conexões do container chegam ao host **SNATadas no gateway da rede**
   (ex.: `172.30.240.128` da rede `172.30.240.0/24`) — por isso o gateway também entra no `Server=`.
3. A interface do host no frontend usa a **mesma porta** do `ListenPort` do agente.

### 1.2 Como verificar a sincronia

| Sinal | Onde | Esperado |
| --- | --- | --- |
| `Zabbix agent ping` | Latest data | `1` |
| `Version of Zabbix agent running` | Latest data | versão do instalado |
| `Uptime` | Latest data | > 0 e subindo |
| Host availability (API `host.get`) | `availability.ZABBIX_PASSIVE/ACTIVE` | `1` (0 = desconhecido, 2 = indisponível) |
| Itens unsupported | Items (state) | `0` (ou apenas os aceitos como ruído) |

---

## 2. Windows — instalação real (HSDS_SUPTI666, 2026-10-17)

### 2.1 Instalação

1. Baixar o instalador oficial (Zabbix Agent 7.4.x, pacote `zabbix_agentd` clássico):
   <https://www.zabbix.com/download_agents> → Windows → versão do servidor.
2. Instalar (serviço criado como **"Zabbix Agent"** em `C:\Program Files\Zabbix Agent\`).
3. Editar `C:\Program Files\Zabbix Agent\zabbix_agentd.conf` (exemplo real em produção):

```ini
LogFile=C:\Program Files\Zabbix Agent\zabbix_agentd.log
Server=192.168.11.121,172.30.240.128
ListenPort=10060
ServerActive=192.168.11.121:10051
Hostname=HSDS_SUPTI666
Include=C:\Program Files\Zabbix Agent\zabbix_agentd.d\
```

> **Por que porta 10060 e não 10050?** No ambiente deste projeto o container
> `infracontrol-zabbix-agent` do Docker **publica a 10050 no host** (`0.0.0.0:10050`, processo
> `com.docker.backend`). Um agente nativo tentando 10050 morre com
> `bind() ... [0x00002740] "Only one usage of each socket address"`. A porta `10060` está livre e
> já cadastrada no host do Zabbix (interface `192.168.11.121:10060`).

### 2.2 Firewall (inbound na porta do agente)

PowerShell **administrador**:

```powershell
New-NetFirewallRule -DisplayName 'Zabbix Agent 10060' -Direction Inbound -Protocol TCP `
    -LocalPort 10060 -Action Allow -Profile Any
```

### 2.3 Iniciar o serviço (requer admin)

```powershell
Start-Service 'Zabbix Agent'    # sem admin: "Não é possível abrir o serviço" (acesso negado)
```

### 2.4 Verificação (executada — tudo verde)

```powershell
Get-Service 'Zabbix Agent'                          # Status: Running
Get-NetTCPConnection -LocalPort 10060 -State Listen # listener em 0.0.0.0
# caminho servidor->agente (do container do Zabbix):
docker exec <zabbix-server> nc -w 3 192.168.11.121 10060 </dev/null   # resposta ZBXD
```

Resultado real: host `HSDS_SUPTI666` (hostid 10843) passou a reportar ~200 itens ao vivo
(CPU, RAM, disco, serviços, BitLocker, TPM, interfaces de rede) via template
"Windows by Zabbix agent".

### 2.5 Gotchas do Windows (com evidência)

| Sintoma | Causa real | Solução |
| --- | --- | --- |
| Log: `listener failed: bind() ... [0x00002740]` + `Zabbix Agent stopped` | Porta ocupada (aqui: container Docker na 10050) | Trocar `ListenPort` (ex.: 10060) **e** a interface do host no Zabbix, ou remapear `ZABBIX_AGENT_PORT` do compose |
| Erro ao iniciar: "Não é possível abrir o serviço" | Shell sem admin | PowerShell elevado (UAC) ou services.msc |
| Itens "Windows updates: ..." em estado *unsupported*, erro `WMI query timeout` | WMI lento (múltiplos AVs — Avast + Kaspersky + Reason EDR no host) | Aumentar timeout dos itens **ou** desabilitar os 8 itens (só informativos; monitoramento de rede não é afetado) |
| Item `State of service "Zabbix Agent" = 0` logo após start | Valor coletado antes do start | Próximo ciclo corrige (stale) |
| Diagnóstico "quem editou o quê" | Comparar `LastWriteTime` do `.conf` e do `.log` | No caso real: conf salvo 2s **após** a última falha — revelou que a porta já havia sido corrigida |

---

## 3. Linux

### 3.1 Instalação nativa (Debian/Ubuntu e RHEL/Rocky)

```bash
# repositório oficial (ex.: Ubuntu 24.04, Zabbix 7.4)
wget https://repo.zabbix.com/zabbix/7.4/release/ubuntu/pool/main/z/zabbix-release/zabbix-release_latest+ubuntu24.04_all.deb
sudo dpkg -i zabbix-release_latest+ubuntu24.04_all.deb && sudo apt update
sudo apt install -y zabbix-agent2

# /etc/zabbix/zabbix_agent2.conf — mesmo contrato do Windows:
sudo tee /etc/zabbix/zabbix_agent2.conf >/dev/null <<'EOF'
Server=192.168.11.121,172.30.240.128
ListenPort=10050
ServerActive=192.168.11.121:10051
Hostname=SERVIDOR-OU-ESTACAO-LINUX
EOF

# firewall
sudo ufw allow 10050/tcp            # Ubuntu; RHEL: sudo firewall-cmd --add-port=10050/tcp --permanent && sudo firewall-cmd --reload

sudo systemctl enable --now zabbix-agent2
sudo systemctl is-active zabbix-agent2   # active
```

RHEL/Rocky: trocar o pacote por `sudo dnf install zabbix-agent2` (repo
`https://repo.zabbix.com/zabbix/7.4/rhel/9/x86_64/`).

### 3.2 Agente em container (este projeto)

O `docker-compose.yml` já define o serviço `zabbix-agent`
(`zabbix/zabbix-agent2:alpine-trunk`, profile `zabbix`, linhas ~530–552):

```bash
# subir só o ambiente Zabbix (db+server+web+agent)
docker compose --profile zabbix up -d

# estado/health e logs
docker ps --filter name=zabbix
docker logs infracontrol-zabbix-agent --tail 20
```

Variáveis do serviço: `ZBX_SERVER_HOST=zabbix-server` (DNS interno do compose) e
`ZBX_HOSTNAME="Zabbix server"` (host técnico que o agent container monitora).
Porta publicada: `${ZABBIX_AGENT_PORT:-10050}:10050` — **atenção ao conflito** com agentes
nativos (§2.5).

---

## 4. Sincronização — cadastro no Zabbix (frontend)

1. **Hosts → Create host**:
   - **Host name** = `Hostname` do agente (literal);
   - **Groups**: grupo do ativo (ex.: "Windows workstations");
   - **Templates**: `Zabbix agent` + por SO (`Windows by Zabbix agent` / `Linux by Zabbix agent`);
   - **Interfaces**: tipo *Zabbix agent*, IP da máquina, **porta = `ListenPort` do agente**;
   - Ativo → `Monitored by: Zabbix server`.
2. Aguardar 1 ciclo do poller (≈60s) e conferir §1.2.
3. Se ficar em availability `2`/unsupported: ver §5.

Caso real cadastrado: `HSDS_SUPTI666` — interface passiva `192.168.11.121:10060`
(hostid 10843); checks ativos também funcionam via `ServerActive=192.168.11.121:10051`
(porta 10051 publicada pelo container `infracontrol-zabbix-server`).

---

## 5. Troubleshooting de sincronia (erros comuns)

| Erro / sintoma | Causa | Solução |
| --- | --- | --- |
| `No such host` / host cinza no mapa | Hostname do agente ≠ nome no Zabbix | Igualar literalmente; ativo usa o nome, não o IP |
| `Get value from agent failed: TCP DGRAM` / `Connection refused` | Agente parado, porta errada ou firewall | Conferir serviço, `ListenPort`, regra inbound, interface do host |
| `Timeout` intermitente | Latência/AV interceptando WMI ou SMB | Aumentar `Timeout` do item; revisar AV/EDR |
| `ZBX_NOTSUPPORTED: Invalid item key format` em testes manuais com `nc` | Resposta esperada ao enviar lixo ao protocolo | **Não é erro** — prova que o agente responde |
| `No permissions to referred object` (API) | Papel do usuário no frontend | Usar role com acesso a hosts/grupos |
| `bind() ... only one usage of each socket address` | Porta ocupada no host | Porta alternativa ou liberar a ocupante (Docker) |
| `WMI query timeout` (itens Windows updates) | WMI lento com múltiplos AVs | Timeout maior ou desabilitar itens |

---

## 6. Referências

- `docs/27-INTEGRACAO-ZABBIX.md` — §7 infraestrutura Docker, §8 frota de switchs;
- `docs/37-F27-MONITORAMENTO-ZABBIX.md` — checklist de monitoramento;
- `docker-compose.yml` — serviço `zabbix-agent` (~linhas 530–552);
- <https://www.zabbix.com/download_agents> — pacotes oficiais por SO e versão.
