# Domínio e Dados

## Entidades principais

```text
Location
Rack
Device
NetworkInterface
MacAddress
IpAddress
Vlan
SwitchPort
SwitchPortVlan
Connection
DiscoveryRun
DiscoveryResult
MonitoringCheck
MonitoringResult
Alert
Incident
Service
Task
SlaPolicy
TaskSla
TaskSlaPause
Slo
SloMeasurement
SloBudget
KnowledgeCategory
KnowledgeArticle
Notification
NotificationRecipient
NotificationDelivery
AuditLog
OutboxMessage
ProcessedMessage
RetentionPolicy
Invitation
InvitationRole
ExternalIdentity
PasswordResetToken
UserSession
```

## Device

Um Device representa equipamentos heterogêneos.

Tipos:

- Server
- Workstation
- Switch
- Router
- Firewall
- AccessPoint
- Printer
- Camera
- Phone
- UPS
- Other

## Relacionamentos

```text
Location 1:N Rack
Rack 1:N Device
Device 1:N NetworkInterface
Switch(Device) 1:N SwitchPort
SwitchPort N:N Vlan
NetworkInterface 1:N IpAddress
Interface N:N Interface via Connection
```

PostgreSQL é a fonte da verdade.
