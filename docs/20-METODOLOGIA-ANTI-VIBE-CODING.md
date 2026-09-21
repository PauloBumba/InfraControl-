# Metodologia Anti-Vibe Coding

## Objetivo

Garantir que o desenvolvimento assistido por IA tenha arquitetura, testes, segurança, governança e documentação antes de produzir código de forma ampla.

## Princípios

1. IA é aceleradora.
2. Teste antes do código para regras de negócio.
3. Isolamento primeiro.
4. Documentação viva.
5. Sem código sem governança.
6. Dividir para conquistar.
7. Banco/modelo de dados primeiro.

## Ciclo de 7 dias

### Dia 1 — AI Jail

- isolamento;
- sandbox;
- permissões;
- secrets;
- governance.

### Dia 2 — Arquitetura

- dados;
- entidades;
- relacionamentos;
- bounded contexts;
- aggregates;
- migrations;
- stack;
- dependências;
- ADR.

### Dia 3 — TDD

- testes unitários;
- integração;
- testes inicialmente falhando;
- cobertura definida.

### Dia 4 — Implementação

Implementar somente o necessário para os testes.

### Dia 5 — Performance

- N+1;
- blocking;
- jobs;
- async;
- medição.

### Dia 6 — Interface

- componentes;
- UX;
- E2E;
- Playwright quando aplicável.

### Dia 7 — CI/CD

- build;
- lint;
- testes;
- security;
- staging;
- deploy controlado.

## Regras adicionais

- testar concorrência;
- registrar timeline de incidentes;
- usar staging;
- LGPD;
- rate limiting;
- sanitização;
- SQL injection;
- prompt injection;
- documentar débito técnico;
- manter memória do projeto em arquivos.
