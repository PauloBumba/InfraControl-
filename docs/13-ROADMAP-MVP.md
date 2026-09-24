# Roadmap

## MVP

Priorizado pelo problema real do usuário (2026-09-23): inventário visível, saúde por ping, toner/drum de impressora, alerta e acesso remoto.

- Login
- RBAC
- Locations
- Racks
- Devices (tipos: Switch, Printer, AP, Camera, Server, Workstation)
- DeviceProfile (checks padrão por tipo ao cadastrar)
- Switches
- Ports
- IP
- MAC
- VLAN
- Ping (up/down estilo Zabbix + alerta)
- SNMP Printer (toner, drum/cilindro)
- SNMP Switch (sysUpTime opcional no MVP)
- Alertas (down, toner/drum baixo, recovery)
- Recovery
- Histórico
- Task híbrida
- Acesso remoto: SSH (switch/servidor), RDP (computador) — sem expor credenciais
- Topologia básica (depois do loop de inventário+monitoramento)

## V1.1

- LLDP
- CDP
- ARP
- DHCP
- descoberta automática
- novos dispositivos
- AP check de internet/WAN por site

## V2

- métricas avançadas
- tráfego
- integrações de fabricantes
- automação
- backup de configuração
- comparação
- API externa
- Windows Agent

Cada item do roadmap deve ser dividido em Tasks pequenas antes da implementação.
