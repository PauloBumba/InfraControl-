# KNOWLEDGE — Plano de Base de Conhecimento

> **Origem:** especificação colada pelo usuário em sessão (2026-10-07).
> Texto integral preservado abaixo; designs derivados dele são sujeitos a
> revisão. Gap analysis × código em `docs/32-KNOWLEDGE-GAP`? — ver
> `docs/31-TASKS-PLANO-EXECUCAO.md` para o plano de execução.

## 1. Objetivo

O módulo Knowledge será responsável por preservar, organizar, versionar e disponibilizar o conhecimento da organização.

A Knowledge deve transformar experiências, procedimentos, pesquisas e resultados em conhecimento reutilizável.

> Knowledge preserva o conhecimento da organização.

---

## 2. Formas de criação

Existirão duas formas principais.

### Manual

Usuário cria:

> Knowledge → Nova Knowledge

Preenche o conteúdo e envia para revisão.

### Automática

Uma Task ou PDI concluído pode gerar uma Knowledge sugerida.

```text
TASK / PDI
    ↓
Histórico
    ↓
Consolidação
    ↓
Knowledge sugerida
    ↓
Revisão
    ↓
Aprovação
    ↓
Publicação
```

---

## 3. Geração automática

Ao concluir uma Task, o sistema poderá disponibilizar:

> Gerar Knowledge

A IA poderá analisar:

- objetivo;
- contexto;
- timeline;
- pesquisas;
- dificuldades;
- decisões;
- evidências;
- implementação;
- validação;
- resultado.

E gerar um primeiro rascunho.

A IA não deve inventar informações.

O conteúdo deve estar baseado nos registros existentes.

---

## 4. Knowledge originada de PDI

Ao concluir um PDI, o sistema poderá identificar conteúdo potencialmente útil para a organização.

Exemplo:

> PDI: Conhecer funcionamento do sistema de Tomografia.

Pode gerar:

> Knowledge: Funcionamento operacional do sistema de Tomografia.

A geração deve ser opcional e passar por revisão.

---

## 5. Tipos de Knowledge

- Procedimento
- Manual
- Guia
- FAQ
- Solução conhecida
- Configuração
- Documentação técnica
- Boas práticas
- Política
- Norma interna
- Troubleshooting
- Conhecimento de sistema
- Conhecimento de infraestrutura
- Conhecimento operacional

---

## 6. Estrutura

Cada Knowledge deverá possuir:

### Identificação

- ID
- título
- descrição
- categoria
- tags
- autor
- responsável

### Conteúdo

- objetivo;
- contexto;
- pré-requisitos;
- procedimento;
- observações;
- problemas conhecidos;
- soluções;
- limitações;
- referências.

### Governança

- status;
- versão;
- data de criação;
- última atualização;
- próxima revisão;
- responsável pela revisão;
- aprovador.

---

## 7. Status

```text
RASCUNHO
   ↓
EM REVISÃO
   ↓
APROVADA
   ↓
PUBLICADA
   ↓
EM REVISÃO
   ↓
ATUALIZADA
```

Também poderá existir:

- Arquivada
- Obsoleta
- Rejeitada

---

## 8. Versionamento

A Knowledge nunca deve perder versões anteriores.

Exemplo:

```text
v1.0
 ↓
v1.1
 ↓
v2.0
```

Cada versão deverá registrar:

- alteração;
- autor;
- data;
- motivo;
- aprovador.

---

## 9. Rastreabilidade

A Knowledge deverá manter sua origem.

Exemplo:

```text
Knowledge #0015
     │
     ├── Task #0042
     ├── Autor: João
     ├── Revisão: Paulo
     └── Versão: 1.2
```

Também poderá ter múltiplas origens:

- Tasks;
- PDIs;
- Chamados;
- Projetos;
- documentos;
- treinamentos.

---

## 10. Relação com Tasks

A Knowledge poderá:

- ser criada a partir de uma Task;
- ser utilizada por uma Task;
- ser atualizada por uma Task;
- estar relacionada a várias Tasks.

Exemplo:

```text
Knowledge
"Configuração SNMP de impressoras"

       ↑
       │
Task #42
Task #57
Task #81
```

---

## 11. Relação com chamados

Quando um chamado utilizar uma Knowledge, registrar:

> Knowledge utilizada na resolução.

Isso permite descobrir quais conhecimentos realmente ajudam a operação.

Também poderá existir:

```text
Chamados recorrentes
       ↓
Knowledge criada
       ↓
Novos chamados
       ↓
Knowledge utilizada
```

---

## 12. Revisão

O sistema deve permitir revisão periódica.

Exemplo:

> Revisar Knowledge a cada 180 dias.

Quando chegar a data:

> Knowledge necessita de revisão.

O responsável poderá:

- manter;
- atualizar;
- substituir;
- arquivar.

---

## 13. Busca

A busca deverá considerar:

- título;
- conteúdo;
- categoria;
- tags;
- autor;
- sistema;
- setor;
- tipo;
- palavras-chave.

Futuramente poderá utilizar busca semântica/embeddings para encontrar conhecimento por significado.

---

## 14. IA

A IA poderá:

- gerar rascunho;
- resumir;
- reorganizar conteúdo;
- identificar duplicidade;
- sugerir tags;
- identificar possível desatualização;
- sugerir revisão;
- relacionar Knowledge semelhantes;
- gerar documentação a partir de Tasks;
- gerar documentação a partir de PDIs.

A IA não aprova nem publica conhecimento sozinha.

---

## 15. Indicadores de Knowledge

### Produção

- Knowledge criadas;
- Knowledge manuais;
- Knowledge geradas automaticamente;
- Knowledge originadas de Tasks;
- Knowledge originadas de PDIs.

### Governança

- rascunhos;
- aguardando revisão;
- publicadas;
- rejeitadas;
- arquivadas;
- obsoletas;
- pendentes de revisão.

### Utilização

- visualizações;
- pesquisas;
- acessos;
- Knowledge mais utilizadas;
- Knowledge sem utilização;
- Knowledge utilizadas em chamados;
- Knowledge utilizadas em Tasks.

### Qualidade

- quantidade de revisões;
- tempo médio até aprovação;
- Knowledge atualizadas;
- Knowledge vencidas;
- Knowledge duplicadas.

---

## 16. Knowledge como patrimônio organizacional

O objetivo final é reduzir a dependência do conhecimento individual.

O conhecimento deve sair de:

> "Fulano sabe como fazer."

Para:

> "A organização sabe como fazer."

---

## 17. Princípio

> Toda experiência relevante pode gerar conhecimento, mas todo conhecimento oficial deve passar por validação.
