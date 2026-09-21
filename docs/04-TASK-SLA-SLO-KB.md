# Task, SLA, SLO e Knowledge Base

## Task

Task é uma unidade genérica de trabalho.

Tipos:

- Manual
- Incident
- Alert
- Monitoring
- Discovery
- Maintenance
- Change
- Preventive
- Corrective
- Project
- System

Pode relacionar-se a múltiplos dispositivos, usuários, incidentes e serviços.

## Estados

```text
Open
InProgress
Waiting
Blocked
Resolved
Cancelled
Closed
```

## SLA

SLA controla compromisso de resposta e resolução.

Entidades:

- SlaPolicy
- TaskSla
- TaskSlaPause

O SLA aplicado deve preservar os prazos históricos.

## SLO

SLO mede objetivo de serviço.

Exemplo:

```text
Availability >= 99.95%
Window = 30 days
```

Entidades:

- Service
- Slo
- SloMeasurement
- SloBudget

## Knowledge Base

Artigos:

- Draft
- Published
- Archived

Relacionamentos possíveis com Device, Incident e Service.
