# INDICADORES E RELATÓRIOS — Plano

> **Origem:** especificação colada pelo usuário em sessão (2026-10-07).
> Texto integral preservado abaixo; designs derivados dele são sujeitos a
> revisão.

## 1. Objetivo

Criar uma camada de indicadores e relatórios para transformar os registros do InfraControl em informações úteis para acompanhamento operacional, gestão e tomada de decisão.

Os indicadores não devem existir apenas para mostrar números.

Devem responder:

> O que está acontecendo?
>
> Onde está acontecendo?
>
> Por que está acontecendo?
>
> O que está melhorando?
>
> Onde precisamos agir?

---

## 2. Indicadores de Tasks

### Volume

- Tasks criadas;
- Tasks concluídas;
- Tasks em andamento;
- Tasks atrasadas;
- Tasks bloqueadas;
- Tasks canceladas.

### Execução

- taxa de conclusão;
- prazo médio;
- tempo médio de execução;
- quantidade de replanejamentos;
- quantidade de impedimentos;
- tempo bloqueado;
- quantidade de atualizações.

### Distribuição

Por:

- responsável;
- equipe;
- setor;
- tipo;
- prioridade;
- período.

### Melhoria

- melhorias realizadas;
- implementações;
- preventivas;
- pesquisas;
- ações de inovação;
- problemas recorrentes tratados.

### Resultado

- Tasks com resultado validado;
- Tasks com impacto mensurado;
- Tasks que atingiram objetivo;
- Tasks parcialmente atingidas;
- Tasks que não atingiram objetivo.

---

## 3. Indicadores de PDI

### Situação

- PDIs ativos;
- concluídos;
- atrasados;
- replanejados;
- cancelados.

### Desenvolvimento

- objetivos atingidos;
- objetivos parcialmente atingidos;
- fases concluídas;
- check-ins realizados;
- evidências registradas;
- avaliações realizadas.

### Competências

Quantidade de PDIs por competência:

- técnico;
- comunicação;
- liderança;
- organização;
- trabalho em equipe;
- gestão do tempo;
- outras.

### Dificuldades

- falta de tempo;
- dificuldade técnica;
- recurso indisponível;
- treinamento indisponível;
- falta de acompanhamento;
- outros.

---

## 4. Indicadores de Knowledge

### Produção

- Knowledge criadas;
- Knowledge manuais;
- Knowledge automáticas;
- Knowledge originadas de Tasks;
- Knowledge originadas de PDIs.

### Governança

- publicadas;
- aguardando revisão;
- vencidas;
- arquivadas;
- obsoletas.

### Utilização

- visualizações;
- buscas;
- acessos;
- Knowledge mais utilizadas;
- Knowledge pouco utilizadas;
- Knowledge utilizadas em chamados;
- Knowledge utilizadas em Tasks.

### Evolução

- revisões realizadas;
- versões criadas;
- Knowledge atualizadas;
- tempo médio de aprovação.

---

## 5. Indicadores integrados

Aqui está uma das partes mais importantes.

O sistema deve conseguir cruzar os módulos.

### Chamados → Tasks

> Quantos problemas recorrentes geraram Tasks?

### Tasks → Knowledge

> Quantas Tasks concluídas geraram documentação?

### PDI → Knowledge

> Quantos PDIs geraram conhecimento reutilizável?

### Knowledge → Chamados

> Quantos chamados foram resolvidos utilizando Knowledge?

### Task → PDI

> Quais competências estão sendo exigidas pelas Tasks?

Esses cruzamentos mostram o ciclo de melhoria da organização.

---

## 6. Dashboard do colaborador

Mostrar:

### Minhas Tasks

- pendentes;
- em andamento;
- atrasadas;
- concluídas.

### Meu PDI

- progresso;
- fases;
- próximas atividades;
- check-ins;
- dificuldades.

### Meu conhecimento

- Knowledge criadas;
- Knowledge em revisão;
- Knowledge publicadas.

---

## 7. Dashboard do gestor

Mostrar:

```text
TASKS
├── Em andamento
├── Atrasadas
├── Bloqueadas
├── Concluídas
└── Melhorias

PDI
├── Ativos
├── Atenção
├── Concluídos
└── Replanejados

KNOWLEDGE
├── Publicadas
├── Em revisão
├── Pendentes
└── Desatualizadas
```

Além disso:

- próximos prazos;
- impedimentos;
- Tasks prioritárias;
- PDIs que precisam de acompanhamento;
- Knowledge aguardando aprovação.

---

## 8. Dashboard administrativo

Visão consolidada por:

- setor;
- equipe;
- período;
- responsável;
- tipo.

Deve permitir filtros.

Exemplo:

> TI → Setembro/2026

---

## 9. Relatório de Tasks

Relatório individual ou por período.

Deve apresentar:

### Resumo

- quantidade;
- concluídas;
- atrasadas;
- bloqueadas;
- taxa de conclusão.

### Execução

- principais Tasks;
- responsáveis;
- dificuldades;
- replanejamentos;
- resultados.

### Melhorias

- implementações;
- preventivas;
- automações;
- melhorias realizadas.

### Impacto

- resultados;
- indicadores antes/depois;
- problemas recorrentes reduzidos;
- ganhos identificados.

---

## 10. Relatório de PDI

Deve apresentar:

- colaborador;
- gestor;
- período;
- objetivos;
- estratégias;
- fases;
- evolução;
- check-ins;
- dificuldades;
- evidências;
- feedbacks;
- avaliação;
- resultado final.

Também poderá gerar relatório semestral.

---

## 11. Relatório de Knowledge

Deve apresentar:

- quantidade criada;
- quantidade publicada;
- origem;
- utilização;
- Knowledge mais acessadas;
- Knowledge desatualizadas;
- revisões;
- versões;
- Knowledge geradas automaticamente.

---

## 12. Relatório gerencial do período

Exemplo:

> Relatório de Desenvolvimento e Melhoria — 2º Semestre de 2026.

Estrutura:

```text
1. Resumo executivo

2. Tasks
   - realizadas
   - pendentes
   - melhorias
   - implementações

3. PDI
   - evolução
   - objetivos
   - competências

4. Knowledge
   - conhecimentos criados
   - conhecimentos utilizados

5. Dificuldades
   - principais impedimentos
   - recorrências

6. Resultados
   - indicadores
   - melhorias
   - impactos

7. Pontos de atenção

8. Próximos passos
```

---

## 13. Histórico dos indicadores

Os indicadores devem permitir comparação.

Exemplo:

```text
              JAN   FEV   MAR   ABR
Tasks         12    18    21    24
Concluídas     8    14    17    20
Knowledge      2     4     7     9
PDIs           5     7     8    10
```

Isso permite identificar tendência.

---

## 14. Exportação

Relatórios deverão poder ser exportados para:

- PDF;
- Excel;
- CSV.

Futuramente:

- dashboards;
- apresentações;
- relatórios automáticos.

---

## 15. Relatórios automáticos

O sistema poderá gerar automaticamente:

### Mensal

Resumo do setor.

### Trimestral

Evolução e principais resultados.

### Semestral

PDI + Tasks + Knowledge + indicadores.

### Anual

Histórico e evolução da organização.

---

## 16. IA nos relatórios

A IA poderá gerar uma interpretação dos indicadores.

Exemplo:

> "Durante o período foram concluídas 24 Tasks, sendo 8 relacionadas a melhorias e 5 a implementações. Foram identificados 7 impedimentos, sendo 3 relacionados à indisponibilidade de recursos. Nove novas Knowledge foram publicadas."

A IA deve utilizar os dados reais do sistema.

Não deve inventar resultados.

---

## 17. Princípio dos indicadores

O sistema deve evitar indicadores puramente quantitativos quando eles puderem induzir comportamento ruim.

Por exemplo:

> "Quantidade de Tasks concluídas por pessoa"

não deve ser utilizado isoladamente para avaliar desempenho.

Uma pessoa pode concluir poucas Tasks complexas enquanto outra concluir várias tarefas pequenas.

Por isso, os indicadores devem considerar:

- complexidade;
- tipo;
- prazo;
- resultado;
- impacto;
- qualidade;
- contexto.

---

## 18. Visão final

O objetivo da camada de indicadores e relatórios é transformar:

```text
REGISTROS
   ↓
DADOS
   ↓
INDICADORES
   ↓
ANÁLISE
   ↓
DECISÃO
   ↓
MELHORIA
```

O InfraControl deve permitir que a gestão não veja somente "quantas coisas foram feitas", mas consiga entender **o que foi feito, como foi feito, quais dificuldades apareceram, o que foi aprendido e qual impacto isso gerou**.
