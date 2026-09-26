# Performance (Dia 5 — docs/20)

Auditoria da F12-T005 (2026-09-25) nos eixos do Dia 5:
**N+1, blocking, jobs, async, medição**.

## Achados e correções

- **N+1**: `RunAllChecksCommand` fazia `SaveChangesAsync` por device
  dentro de `ExecuteAsync` — N idas ao banco por execução. Corrigido:
  persistência em **lote** (um `SaveChanges` ao fim do loop; o
  `TransactionBehavior` do pipeline já garante atomicidade na API).
  Coberto por teste (`RunAllChecks_ShouldPersistAllResultsInSingleSaveChanges`).
- **Blocking**: `RetentionPolicyRepository.GetAllAsync` usava
  `ContinueWith(task => task.Result)` (sync-over-async). Corrigido para
  `await` puro.
- **Medição**: `PerformanceBehavior` registrava só Information.
  Agora loga **Warning** acima de `Performance:SlowRequestMs`
  (default 1000, appsettings `Performance`), mantém Information abaixo e
  continua emitindo o histograma `infracontrol.request.duration`
  (tags `request`/`outcome`). Coberto por 3 testes (rápido, lento,
  falha) incluindo captura via `MeterListener`.

## Sem problema (decisões registradas)

- **Async**: todo o pipeline é `async` de ponta a ponta; o único
  `Task.Run` é o `SshRemoteProvider.Connect()` — SSH.NET expõe apenas
  API síncrona (documentado em F11-T002).
- **Jobs**: `CleanupWorker`/`BackupWorker`/`OutboxDispatcherWorker`
  rodam em background com intervalo configurável — sem trabalho
  síncrono bloqueante no thread do host.
- **Paralelismo dos checks**: mantido **serial** de propósito —
  `AppDbContext` não é thread-safe e execução paralela dispararia
  probes ICMP/SNMP em rajada (decisão registrada, não é N+1).

## Como validar

```bash
dotnet test tests/InfraControl.UnitTests --filter "PerformanceBehaviorTests|RunAllChecksCommandTests"
```
