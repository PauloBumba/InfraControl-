# InfraControl

Plataforma on-premises para gerenciamento de infraestrutura de tecnologia da informação. O InfraControl reúne inventário, rede, descoberta, monitoramento, alertas, incidentes, tarefas, níveis de serviço, conhecimento, notificações, identidade, auditoria e acesso remoto em uma solução modular.

> **Status atual:** a fundação técnica está implementada. O frontend possui um dashboard inicial, o backend possui infraestrutura com PostgreSQL, Redis, RabbitMQ e observabilidade, e a fundação do ASP.NET Core Identity foi concluída. A validação remota do CI está temporariamente bloqueada porque a conta do GitHub atingiu o limite mensal de minutos do GitHub Actions.

## Objetivo

O projeto busca centralizar a operação de ambientes de TI sem impor um fornecedor específico de equipamentos ou serviços. A primeira fase prioriza uma base modular, observável, testável e adequada para execução on-premises.

A solução não pretende começar com microservices. O sistema inicia como um **monólito modular**, permitindo separar os domínios sem introduzir complexidade operacional prematura.

## Estado de implementação

| Área | Estado | Evidência |
| --- | --- | --- |
| Solução .NET | Implementada | `InfraControl.slnx` |
| PostgreSQL | Configurado | Docker Compose e Entity Framework Core |
| Redis | Configurado | Docker Compose e camada de cache |
| RabbitMQ | Configurado | MassTransit e infraestrutura de mensageria |
| Outbox | Estruturado | Repositório e eventos de domínio |
| Observabilidade | Base implementada | Serilog, health checks e OpenTelemetry |
| Frontend | Dashboard inicial implementado | React, TypeScript, Vite e PrimeReact |
| ASP.NET Core Identity | F1-T001 concluída | Usuário, roles store e migration inicial |
| CI | Workflow criado, validação bloqueada | `.github/workflows/ci.yml` |

O estado detalhado e as pendências rastreáveis estão em [`PROJECT_STATE.md`](PROJECT_STATE.md) e [`TASKS.md`](TASKS.md).

## Arquitetura

O backend segue uma organização modular com separação entre domínio, aplicação, infraestrutura, API, workers e contratos.

```text
src/
├── InfraControl.Domain/          # Entidades e regras de domínio
├── InfraControl.Application/    # Casos de uso e interfaces
├── InfraControl.Infrastructure/ # Persistência, Identity, cache e mensageria
├── InfraControl.Api/            # API HTTP e health checks
├── InfraControl.Workers/        # Processamentos assíncronos
└── InfraControl.Contracts/      # Contratos de integração

tests/
├── InfraControl.UnitTests/
├── InfraControl.IntegrationTests/
└── InfraControl.ArchitectureTests/

frontend/InfraControl.Web/
└── src/
    ├── api/
    ├── assets/
    ├── components/
    ├── context/
    ├── data/
    ├── hooks/
    ├── pages/
    ├── redux/
    ├── services/
    ├── types/
    └── utils/
```

O frontend utiliza organização por camadas e componentes reutilizáveis. A página atual é o dashboard operacional, com indicadores de disponibilidade, saúde dos sistemas, alertas recentes e atividades da plataforma.

## Tecnologias

### Backend

- .NET 10;
- ASP.NET Core Identity [1];
- Entity Framework Core [2];
- PostgreSQL;
- ASP.NET Core Identity;
- Redis;
- RabbitMQ com MassTransit;
- MediatR;
- Serilog;
- OpenTelemetry;
- xUnit, FluentAssertions e NetArchTest.

### Frontend

- React 19 [4];
- TypeScript;
- Vite;
- PrimeReact;
- PrimeFlex;
- Redux Toolkit;
- TanStack Query;
- SignalR client.

## Pré-requisitos

Para executar o projeto localmente, instale:

- .NET SDK `10.0.401`, conforme o arquivo [`global.json`](global.json);
- Node.js 22 ou versão compatível com o projeto;
- npm;
- Docker e Docker Compose, para subir PostgreSQL, Redis e RabbitMQ.

## Configuração local

Copie o arquivo de exemplo de variáveis de ambiente quando necessário:

```bash
cp .env.example .env
```

Os serviços de infraestrutura podem ser iniciados com:

```bash
docker compose -f docker-compose.infrastructure.yml up -d
```

Para conferir os containers:

```bash
docker compose -f docker-compose.infrastructure.yml ps
```

Não use credenciais reais em arquivos versionados. As credenciais do Compose são destinadas somente ao ambiente de desenvolvimento local.

## Executar o backend

Restaure as dependências e compile a solução:

```bash
dotnet restore InfraControl.slnx
dotnet build InfraControl.slnx --configuration Release --no-restore
```

Execute a API:

```bash
dotnet run --project src/InfraControl.Api
```

A API expõe health checks em:

```text
/health
/health/ready
```

A documentação OpenAPI é disponibilizada no ambiente de desenvolvimento.

## Executar o frontend

Entre no diretório do frontend e instale as dependências:

```bash
cd frontend/InfraControl.Web
npm ci
```

Inicie o servidor de desenvolvimento:

```bash
npm run dev
```

Para gerar a versão de produção:

```bash
npm run build
```

## Banco de dados e migrations

As migrations ficam em [`src/InfraControl.Infrastructure/Migrations`](src/InfraControl.Infrastructure/Migrations). A migration `AddIdentityFoundation` cria as tabelas padrão do ASP.NET Core Identity, incluindo usuários, roles, claims, logins e tokens.

Para aplicar migrations com o Entity Framework Core:

```bash
dotnet ef database update \
  --project src/InfraControl.Infrastructure \
  --startup-project src/InfraControl.Api
```

A aplicação das migrations exige que o PostgreSQL esteja disponível e que a connection string esteja configurada corretamente.

## Testes

Execute todos os testes da solução com:

```bash
dotnet test InfraControl.slnx \
  --configuration Release \
  --no-build \
  --no-restore
```

As suítes atuais são organizadas por finalidade:

- **UnitTests:** testes isolados de componentes e regras;
- **IntegrationTests:** testes que serão conectados aos serviços de infraestrutura;
- **ArchitectureTests:** regras de dependência e limites entre projetos.

A fundação de Identity possui um teste que verifica o mapeamento de `InfraControlUser` para a tabela `AspNetUsers`.

## Integração contínua

O workflow [`ci.yml`](.github/workflows/ci.yml) executa em pull requests, em alterações no branch `main` e manualmente. Ele foi preparado conforme o modelo de workflows do GitHub Actions [3] para:

1. Restaurar, compilar e testar a solução .NET;
2. Subir PostgreSQL, Redis e RabbitMQ como serviços do job;
3. Instalar as dependências do frontend;
4. Compilar o frontend;
5. Publicar os resultados dos testes como artefatos.

A pipeline está correta, mas sua execução remota está bloqueada no momento. A conta `PauloBumba` consumiu os `2.000/2.000` minutos incluídos do GitHub Actions. O uso está previsto para ser renovado em **01/10/2026**, salvo alteração da configuração de orçamento ou uso de outro runner.

Esse bloqueio está documentado em [`docs/11-CLEANUP-RETENTION-TESTES-CICD.md`](docs/11-CLEANUP-RETENTION-TESTES-CICD.md) e não representa uma falha do código do workflow.

## Roadmap imediato

As próximas etapas da fundação de identidade são:

- Bootstrap do primeiro administrador;
- Roles;
- Permissions;
- Convites;
- Aceite e rejeição de convites;
- Alteração e recuperação de senha;
- Auditoria.

O roadmap completo está em [`TASKS.md`](TASKS.md). Cada Task deve ser executada isoladamente, com escopo, teste, evidência e atualização de estado.

## Processo de desenvolvimento

O projeto segue este ciclo:

```text
Requisito
→ Domínio
→ Dados
→ Arquitetura
→ ADR
→ Task pequena
→ Teste
→ Código mínimo
→ Validação
→ Documentação
→ Commit
```

Antes de alterar o código, leia [`AGENTS.md`](AGENTS.md), confirme a Task em [`TASKS.md`](TASKS.md) e consulte os ADRs relevantes. Não marque uma decisão como concluída somente porque ela foi documentada; a conclusão exige evidência de código ou validação.

## Documentação

- [Índice mestre](docs/00-MASTER-INDEX.md)
- [Arquitetura](docs/01-ARQUITETURA.md)
- [Identity e acesso](docs/05-IDENTITY-ACESSO.md)
- [Observabilidade e segurança](docs/10-REMOTE-ACCESS-OBSERVABILITY-SECURITY.md)
- [Testes e CI/CD](docs/11-CLEANUP-RETENTION-TESTES-CICD.md)
- [UI/UX](docs/14-UI-UX.md)
- [Contratos da API](docs/15-API-CONTRATOS.md)
- [Estrutura da solução](docs/16-ESTRUTURA-SOLUTION.md)
- [Decisões arquiteturais](docs/12-DECISOES.md)

## Licença

Consulte [`LICENSE`](LICENSE).

## Referências

[1]: https://learn.microsoft.com/aspnet/core/security/authentication/identity "ASP.NET Core Identity"
[2]: https://learn.microsoft.com/ef/core/ "Entity Framework Core"
[3]: https://docs.github.com/actions "GitHub Actions"
[4]: https://react.dev/ "React Documentation"
