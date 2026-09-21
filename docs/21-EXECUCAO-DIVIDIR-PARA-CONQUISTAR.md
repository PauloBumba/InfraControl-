# Execução — Dividir para Conquistar

## Regra

Toda funcionalidade deve ser quebrada até chegar a uma unidade pequena e verificável.

## Exemplo

Errado:

```text
Implementar monitoramento.
```

Correto:

```text
MON-001 Entity MonitoringCheck
MON-002 Entity MonitoringResult
MON-003 Migration
MON-004 PingProvider
MON-005 PingProviderTests
MON-006 RunCheckCommand
MON-007 RunCheckHandler
MON-008 API
MON-009 IntegrationTest
MON-010 Worker
MON-011 Alert
MON-012 Recovery
MON-013 UI
```

## Ciclo

```text
BACKLOG
→ READY
→ PLANNING
→ IN_PROGRESS
→ TESTING
→ REVIEW
→ VALIDATION
→ DONE
```

## Bloqueio

```text
IN_PROGRESS
→ BLOCKED
→ resolver dependência
→ voltar
```

## Task grande

Se uma Task exigir vários dias, dezenas de arquivos ou múltiplas responsabilidades, dividir.

## Evidência

Uma Task encerrada deve informar:

- arquivos alterados;
- testes;
- build;
- decisões;
- evidências;
- commit;
- documentação atualizada.

## Regra de parada

Se um teste falhar, parar o avanço da cadeia até entender a causa.

Se uma dependência estiver bloqueada, não inventar solução fora do escopo.

## Definition of Done

```text
[ ] objetivo atendido
[ ] testes
[ ] build
[ ] segurança
[ ] observabilidade
[ ] documentação
[ ] review
[ ] evidência
[ ] estado atualizado
[ ] commit
```

## Regra de ouro

> Uma coisa por vez.
>
> Uma Task por vez.
>
> Uma mudança pequena por vez.
>
> Um teste por vez.
>
> Uma validação por vez.
