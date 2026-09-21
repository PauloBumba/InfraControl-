# Checklist de Desenvolvimento

> Referência rápida para cada Task. Copiar e preencher durante a execução.

---

## Pré-Execução

- [ ] Ler `AGENTS.md`
- [ ] Ler `PROJECT_STATE.md`
- [ ] Ler `TASKS.md`
- [ ] Ler ADRs relevantes
- [ ] Ler documentação do módulo
- [ ] Ler requisitos da Task
- [ ] Verificar estado atual do repositório
- [ ] Verificar se já existe código relacionado
- [ ] Identificar dependências
- [ ] Identificar bloqueios
- [ ] Confirmar Task com o usuário

---

## Planejamento

- [ ] Objetivo claro
- [ ] Escopo definido
- [ ] Critérios de aceite listados
- [ ] Testes definidos (RED → GREEN → REFACTOR)
- [ ] SubTasks criadas (se necessário)
- [ ] Plano apresentado ao usuário

---

## Durante a Execução

- [ ] Criar/ajustar teste primeiro (RED)
- [ ] Implementar parte mínima
- [ ] Build passando
- [ ] Testes passando (GREEN)
- [ ] Refatorar se necessário
- [ ] Revisar diff
- [ ] Não esconder testes quebrados

---

## Pós-Execução

- [ ] Objetivo atendido
- [ ] Escopo respeitado
- [ ] Dependências resolvidas
- [ ] Testes necessários criados
- [ ] Testes passando
- [ ] Build passando
- [ ] Segurança verificada
- [ ] Observabilidade verificada
- [ ] Documentação atualizada
- [ ] Review concluído
- [ ] Evidências registradas
- [ ] PROJECT_STATE atualizado
- [ ] TASKS atualizado
- [ ] Commit realizado

---

## Validação Final

- [ ] `dotnet restore InfraControl.slnx` → SUCCESS
- [ ] `dotnet build InfraControl.slnx` → SUCCESS
- [ ] `dotnet test InfraControl.slnx` → SUCCESS
- [ ] Nenhum erro de compilação
- [ ] Nenhuma referência circular
- [ ] Nenhuma feature desnecessária implementada

---

## Regras de Ouro

```
Uma coisa por vez.
Uma Task por vez.
Uma mudança pequena por vez.
Um teste por vez.
Uma validação por vez.
```

---

## Proibições

- Não inventar requisitos.
- Não alterar arquitetura sem ADR.
- Não executar comando destrutivo sem confirmação.
- Não usar credenciais reais.
- Não esconder testes quebrados.
- Não fazer `git push` automaticamente.
- Não implementar várias Tasks sem autorização.
- Não apagar histórico sem política explícita.

---

## Se Bloqueou

```
IN_PROGRESS
→ BLOCKED
→ resolver dependência
→ voltar para IN_PROGRESS
```

---

## Se Grande Demais

PARAR.

Dividir em SubTasks com:
- objetivo
- escopo
- critérios
- testes
- evidências
