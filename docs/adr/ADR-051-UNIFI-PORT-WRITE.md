# ADR-051: Escrita UniFi — Configuração de Portas de Switch (enable/disable/VLAN)

- **Status**: aceito
- **Data**: 2026-10-13
- **Contexto**: Fase 07e — o usuário solicitou capacidade de configurar portas de switch via plataforma. O ADR-048 estabeleceu a integração UniFi como somente-leitura; esta ADR sobrepõe essa restrição **exclusivamente** para operações de porta em switches UniFi, com salvaguardas explícitas.

## Contexto

A plataforma já exibe portas UniFi ao vivo (Fase 07a) e sincroniza o cadastro automaticamente (Fase 07d). Falta a capacidade de **operar** as portas: habilitar/desabilitar e mudar VLAN trunk/native. A API UniFi para escrita de portas é **não-oficial** (não documentada publicamente), variável entre versões, e uma operação errada pode derrubar conectividade do switch (inclusive o próprio switch se a porta de management for desabilitada).

## Decisão

### 1. Escopo mínimo e explícito

Apenas duas operações são permitidas:

| Operação | Campo UniFi | Risco |
|---|---|---|
| Habilitar/Desabilitar porta | `port_table[].portconf_id` + `disabled` | Alto — porta de management pode derrubar o device |
| Definir VLAN (native/trunk) | `port_table[].port_vlan`, `port_vlan_enabled` | Médio — pode causar loop ou perder acesso |

**Proibido** neste ADR: PoE on/off, speed/duplex, mirroring, port isolation, ACLs por porta, anycast gateway.

### 2. Mecanismo: GET → modify → PUT (nunca POST cego)

A escrita usa o ciclo completo:

1. `GET /stat/device` → device atual com `port_table` completo
2. Modifica **apenas** o campo alvo do porto específico (índice)
3. `PUT /rest/device/{mac}` com o device completo de volta

Isso evita o risco de "escrita cega" (POST parcial) que pode sobrescrever configurações não relacionadas. O GET garante que outras portas e configurações do device permanecem intactas.

### 3. Salvaguardas obrigatórias

| Salvaguarda | Implementação |
|---|---|
| **RoleAccess.Operate** | Command exige Operator ou Admin (ADR-016) |
| **Confirmação no front** | Dialog com nome da porta, switch e operação antes de executar |
| **Proteção da porta de management** | Se a porta tem `is_management_port` ou é a porta com o IP do device, recusa com 409 pt-BR |
| **Auditoria** | `IAuditableRequest` — toda escrita gera entrada de auditoria com who/what/when |
| **Rollback manual** | Sem rollback automático — operador pode reverter pela UI (enable de volta / VLAN anterior) |
| **Timeout curto** | 30s — o controller pode demorar em devices grandes |

### 4. Contrato do comando

```
SetUnifiPortConfigCommand(
    SwitchId: Guid,        // Device local (guard type=Switch)
    PortIndex: int,        // Índice da porta no port_table
    Disabled: bool?,       // null = não alterar
    PortVlan: int?         // null = não alterar
) : BaseCommand<SetUnifiPortResultDto>, IAuthorizedRequest, IAuditableRequest
```

Resultado: `SetUnifiPortResultDto(bool Success, string Message)`.

### 5. Por que não é automático

Escrita em equipamento de rede é **operação deliberada**, não monitoramento. Diferente do sync de leitura (Fase 07d, automático a cada 30min), a escrita:
- Exige intenção humana explícita (botão + confirmação)
- Pode causar indisponibilidade se errada
- Não tem "valor padrão seguro" — cada caso é único

### 6. Consequências

- **Positivo**: operador pode habilitar/desabilitar portas e ajustar VLANs direto da plataforma, sem acessar o controller UniFi
- **Negativo**: superfície de ataque aumenta (um Admin comprometido pode derrubar portas); mitigado por auditoria + RoleAccess.Operate
- **Risco aceito**: API não-oficial pode mudar entre versões do UniFi; se quebrar, a operação falha com erro claro (não corrompe dados locais)

## Alternativas consideradas

1. **Webhook do controller** → não suportado em todas as versões; complexidade alta
2. **SSH direto ao switch** → viola ADR-048 (nunca SSH); credenciais de device em risco
3. **SNMP write** → SNMPv3 com privacidade; infraestrutura de credenciais complexa; não suporta VLAN change na maioria dos switches
4. **Não fazer** → operador continua acessando o controller UniFi manualmente (status quo)

## Referências

- ADR-048 §2 (integração somente-leitura — sobrepomos parcialmente)
- ADR-016 (RBAC — RoleAccess.Operate)
- UniFi Network Controller API (não-oficial) — `PUT /rest/device/{mac}`
