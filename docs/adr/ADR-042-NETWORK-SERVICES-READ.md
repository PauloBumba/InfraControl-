# ADR-042: Network Services Read — leases DHCP, rotas e regras de firewall (somente leitura)

- **Status**: aceito
- **Data**: 2026-10-10
- **Tasks**: F18-T005 (escopo decidido em question 2026-10-10)

## Contexto

O F17-T008 removeu da `NetworkPage` as abas **DHCP, Rotas e Firewall**
porque "não há endpoint no backend". A F18-T005 devolve esses serviços
com fonte real. O questionário obrigatório da task foi respondido pelo
usuário em 2026-10-10:

- **fonte de dados**: **SNMP + SSH por vendor** — SNMP (somente
  leitura) para leases e rotas; SSH de leitura por fabricante para
  regras de firewall (SNMP não expõe ACLs);
- **escopo**: **somente leitura** — nenhuma escrita/gerência (é
  exatamente o que as abas removidas prometiam).

Reuso de base existente: `ISnmpClient` (Lextm.SharpSnmpLib, F5-T004) e
SSH.NET (F11-T002, `SshRemoteProvider`) já estão no repo — **nenhum
pacote NuGet novo**.

## Decisão

1. **Alvo único por config (v1)** — seção `NetworkServices:*`:
   `Target` (IP do equipamento), `Vendor` (driver de firewall, v1 =
   `cisco`), `SshUsername`/`SshPassword`/`SshPort`/`TimeoutSeconds`
   (senha via env, nunca no banco). `Target` vazio = integração
   desligada → **503 + motivo** no envelope (semântica da casa, igual
   UniFi §8/§9). Múltiplos alvos ou alvo vindo do inventário
   (`Device.Ip`/`Vendor`) = extensão futura, task própria.
2. **SNMP v2c read-only** com a credencial global já existente
   (`Monitoring:Snmp`):
   - **leases**: walk de `1.3.6.1.2.1.4.22.1`
     (`ipNetToMediaTable` RFC1213 / `ipNetToPhysicalTable` RFC4293 —
     colunas `.1` ifIndex, `.2` physAddress, `.3` netAddress são iguais
     nas duas variantes) → `{ip, mac, ifIndex}`; MAC normalizado para
     `aa:bb:cc:dd:ee:ff` quando o valor vier como hex de 12 dígitos,
     senão fica cru; entrada sem IP é descartada (não identifica lease).
   - **rotas**: walk de `1.3.6.1.2.1.4.21.1` (`ipRouteTable`) →
     destino/máscara/próximo-salto/ifIndex/métrica + tipo e protocolo
     com os rótulos do RFC1213 (`direct`/`indirect`, `local`/`ospf`/
     `rip`/`bgp`-via-nativo…) e fallback numérico cru quando o número
     não está no mapa.
3. **Firewall via SSH, driver por vendor**: um `show access-lists`
   (driver Cisco v1) por leitura; o par (comando + parser) fica no
   `IFirewallVendorReader` do vendor — vendor desconhecido →
   `InvalidOperationException` pt-BR listando os suportados → 503
   honesto. Falha de conexão SSH vira 503 com motivo (nunca 500
   silencioso); a senha jamais aparece em erro ou log. Cada linha de
   ACL vira `FirewallRuleDto(acl, scope, sequence, action, protocol,
   expression)` — a expressão é o **texto cru** do device (sem
   inventar semântica de host/any/wildcard).
4. **Somente leitura garantido**: o código só executa `walk` (GET
   SNMPv2c) e um comando `show`; não existe mutation nem endpoint de
   escrita nesta camada.
5. **Sem persistência e sem cache**: sondagem direta a cada chamada
   (dados de estado atual do equipamento). Se virar gargalo, cache
   curto no padrão do §9 do docs/29 é extensão futura.
6. **Endpoints** (só autenticado; `RoleAccess.Read` no
   `IAuthorizedRequest` — nível único ADR-030 §3):
   - `GET /api/v1/network/services/dhcp/leases`
   - `GET /api/v1/network/services/routes`
   - `GET /api/v1/network/services/firewall/rules`
7. **UI**: as abas DHCP/Rotas/Firewall voltam à `NetworkPage`
   consumindo esses endpoints; com a integração desligada cada painel
   mostra o motivo honesto (503) em vez de dado inventado.

## Consequências

- Sem equipamento real no ambiente (§46), a validação ponta-a-ponta
  fica pendente; os testes unitários cobrem parse SNMP com OIDs
  realistas, parse de ACL com amostras Cisco, resolução de vendor,
  semântica de "não configurado" e o mapeamento 503.
- Um alvo por instância (config) — múltiplos switches exigem a
  extensão prevista no item 1.
- SNMP v2c com community global: v3 fica para quando o ambiente
  exigir; SSH usa credencial de serviço via env.
- O número 042 é local; a renumeração dos ADRs remotos 034–040
  (pendência do usuário) segue válida a partir de 043.
