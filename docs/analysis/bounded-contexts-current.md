# Análise de Bounded Contexts Atuais

## Estrutura Atual do Domain

### Pastas Existentes

```text
Domain/
├── Common/                    # Building blocks
│   ├── Entities/
│   ├── Events/
│   └── ValueObjects/
├── Entities/                  # ❌ Flat (todas entidades juntas)
│   ├── Location.cs
│   ├── Rack.cs
│   ├── Device.cs
│   ├── NetworkInterface.cs
│   ├── MacAddress.cs
│   ├── IpAddress.cs
│   ├── SwitchPort.cs
│   ├── SwitchPortVlan.cs
│   ├── Vlan.cs
│   ├── Connection.cs
│   ├── User.cs
│   ├── Invitation.cs
│   ├── UserSession.cs
│   ├── UserMfa.cs
│   ├── UserRoleAssignment.cs
│   ├── InvitationRole.cs
│   ├── Alert.cs
│   ├── Incident.cs
│   ├── IncidentAlert.cs
│   ├── Notification.cs
│   ├── NotificationDelivery.cs
│   ├── NotificationRecipient.cs
│   ├── NotificationPreference.cs
│   ├── TaskItem.cs
│   ├── TaskComment.cs
│   ├── TaskDevice.cs
│   ├── TaskIncident.cs
│   ├── TaskHistory.cs
│   ├── TaskAssignee.cs
│   ├── TaskSla.cs
│   ├── TaskSlaPause.cs
│   ├── KnowledgeArticle.cs
│   ├── KnowledgeArticleDevice.cs
│   ├── KnowledgeArticleIncident.cs
│   ├── KnowledgeArticleService.cs
│   ├── KnowledgeCategory.cs
│   ├── SlaPolicy.cs
│   ├── Slo.cs
│   ├── SloBudget.cs
│   ├── SloMeasurement.cs
│   ├── RemoteSession.cs
│   ├── DeviceCredential.cs
│   ├── DiscoveryRun.cs
│   ├── DiscoveryResult.cs
│   ├── ExternalDeviceReference.cs
│   ├── MonitoringCheck.cs
│   ├── MonitoringEvent.cs
│   ├── MonitoringResult.cs
│   ├── AuditEntry.cs
│   ├── OutboxMessage.cs
│   ├── ProcessedMessage.cs
│   ├── RetentionPolicy.cs
│   ├── IndividualDevelopmentPlan.cs
│   ├── PdiHistoryEntry.cs
│   ├── PdiTransitions.cs
│   └── Service.cs
├── Enums/                     # ❌ Flat (todos enums juntos)
├── Events/                    # ❌ Flat (todos eventos juntos)
├── ValueObjects/             # ❌ Flat (todos value objects juntos)
├── Devices/                   # ⚠️ Subpastas inconsistentes
├── Alerts/
├── Discovery/
├── Incidents/
├── Knowledge/
├── Monitoring/
├── Network/
├── Notifications/
├── Profiles/
├── RemoteAccess/
├── Sla/
├── Tasks/
└── Pdi/
```

## Bounded Contexts Identificados

### 1. Inventory
**Entidades:**
- Location
- Rack
- Device
- NetworkInterface
- MacAddress
- IpAddress
- ExternalDeviceReference (cross-context)

**Eventos:**
- (não identificados em primeira análise)

### 2. Network
**Entidades:**
- SwitchPort
- SwitchPortVlan
- Vlan
- Connection

**Eventos:**
- (não identificados em primeira análise)

### 3. Identity
**Entidades:**
- User
- Invitation
- UserSession
- UserMfa
- UserRoleAssignment
- InvitationRole

**Eventos:**
- UserCreated
- UserUpdated
- InvitationAccepted
- InvitationRejected
- SessionCreated
- SessionRevoked

### 4. Monitoring
**Entidades:**
- MonitoringCheck
- MonitoringEvent
- MonitoringResult

**Eventos:**
- MonitoringCheckCreated
- MonitoringEventOccurred

### 5. Alerts
**Entidades:**
- Alert

**Eventos:**
- AlertCreated
- AlertResolved

### 6. Incidents
**Entidades:**
- Incident
- IncidentAlert

**Eventos:**
- IncidentCreated
- IncidentUpdated
- IncidentResolved

### 7. Notifications
**Entidades:**
- Notification
- NotificationDelivery
- NotificationRecipient
- NotificationPreference

**Eventos:**
- (não identificados em primeira análise)

### 8. Tasks
**Entidades:**
- TaskItem
- TaskComment
- TaskDevice
- TaskIncident
- TaskHistory
- TaskAssignee
- TaskSla
- TaskSlaPause

**Eventos:**
- (não identificados em primeira análise)

### 9. Knowledge
**Entidades:**
- KnowledgeArticle
- KnowledgeArticleDevice
- KnowledgeArticleIncident
- KnowledgeArticleService
- KnowledgeCategory

**Eventos:**
- (não identificados em primeira análise)

### 10. SLA
**Entidades:**
- SlaPolicy
- Slo
- SloBudget
- SloMeasurement

**Eventos:**
- (não identificados em primeira análise)

### 11. Remote Access
**Entidades:**
- RemoteSession
- DeviceCredential

**Eventos:**
- (não identificados em primeira análise)

### 12. Discovery
**Entidades:**
- DiscoveryRun
- DiscoveryResult

**Eventos:**
- (não identificados em primeira análise)

### 13. PDI (Individual Development Plan)
**Entidades:**
- IndividualDevelopmentPlan
- PdiHistoryEntry
- PdiTransitions

**Eventos:**
- PdiCreated
- PdiUpdated
- PdiStatusChanged

### 14. Infrastructure/Cross-Cutting
**Entidades:**
- AuditEntry
- OutboxMessage
- ProcessedMessage
- RetentionPolicy
- Service

**Eventos:**
- (não identificados em primeira análise)

## Dependências Entre Contexts

**Cross-Context References:**
- ExternalDeviceReference: Usado por Inventory, aponta para integrations externas (UniFi, Zabbix)
- TaskDevice: Task → Device (Tasks depende de Inventory)
- TaskIncident: Task → Incident (Tasks depende de Incidents)
- KnowledgeArticleDevice: Knowledge → Device (Knowledge depende de Inventory)
- KnowledgeArticleIncident: Knowledge → Incident (Knowledge depende de Incidents)
- KnowledgeArticleService: Knowledge → Service (Knowledge depende de SLA)
- IncidentAlert: Incident → Alert (Incidents depende de Alerts)
- MonitoringEvent: Pode referenciar Device (Monitoring depende de Inventory)

## Observações

1. **Entidades flat vs subpastas**: A maioria das entidades está em `Entities/` flat, mas existem subpastas vazias (`Devices/`, `Alerts/`, etc.) que sugerem intenção de bounded contexts que não foi completada.

2. **Eventos misturados**: Eventos de domínio estão em `Events/` flat, não dentro dos bounded contexts.

3. **Dependências claras**: Há dependências definidas entre contexts (Tasks → Inventory/Incidents, Knowledge → Inventory/Incidents/SLA, etc.), o que é normal em DDD.

4. **Cross-cutting**: Algumas entidades (AuditEntry, OutboxMessage, etc.) são cross-cutting e podem ficar em um contexto separado ou em `Common/`.
