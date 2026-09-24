# ADR-014 — DeviceProfile

## Status
Accepted

## Decisão

DeviceProfile define os checks padrão por `DeviceType`, aplicados no cadastro do Device, com override opcional por device.

### Comportamento

1. **Defaults por tipo** ficam no domínio (`DeviceProfileDefaults`), fonte única de verdade versionada em código:

| DeviceType | Checks padrão |
| --- | --- |
| Printer | Ping, SnmpToner, SnmpDrum |
| AccessPoint | Ping, HttpWan |
| Switch | Ping, SnmpSystem |
| Server | Ping, SnmpSystem |
| Camera | Ping |
| Router | Ping, SnmpSystem |
| Firewall | Ping, SnmpSystem |
| Workstation | Ping |
| Phone | Ping |
| UPS | Ping, SnmpSystem |
| Other | Ping |

2. **Ao criar o Device**, o handler materializa a lista de checks aplicados no próprio agregado (`Device.MonitoringChecks` + `Device.ProfileIsCustom = false`).

3. **Override opcional**: `UpdateDeviceProfileCommand` troca a lista no device (`ProfileIsCustom = true`). Voltar aos defaults é permitido (`useDefault: true`).

4. **Execução fica fora deste ADR**: F5-T001 cria `MonitoringCheck`/resultados e resolve `IMonitoringProvider` via `IProviderRegistry`. DeviceProfile só declara *o que* monitorar, não *como*.

### Não fazer agora

- Tabela global editável de perfis por tipo (defaults em código bastam no MVP);
- Thresholds numéricos (toner/drum “baixo” entram com F6 alertas);
- WAN como check SNMP dedicado (HttpWan cobre “AP com internet” no MVP);
- Auto-provision de checks em devices já criados (backfill manual/seed depois).

## Motivo

O usuário precisa ver saúde por tipo (ping, toner/drum de impressora, AP on/off) sem configurar check por check. Defaults por tipo + override cobre o hospital heterogêneo sem virar configuração infinita.

## Consequências

- Criar device já nasce “monitorável” para o worker de ping/SNMP (F5);
- Mudança de default de tipo exige release (explícito e auditável);
- Devices criados antes deste slice ficam sem checks até backfill/override.
