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
| Observabilidade | Base implementada | Serilog, health checks por estado e OpenTelemetry |
| Frontend | Dashboard inicial implementado | React, TypeScript, Vite e PrimeReact |
| ASP.NET Core Identity | F1-T001–F1-T004 concluídas | Agregados DDD, bootstrap, RBAC, permissões, policies, claims, sessões e migration |
| Plataforma transversal | Implementada | Behaviors, auditoria, idempotência, Outbox/Inbox, resiliência, cache, feature flags e providers |
| CI | Workflow criado, validação bloqueada | `.github/workflows/ci.yml` |

O estado detalhado e as pendências rastreáveis estão em [`PROJECT_STATE.md`](PROJECT_STATE.md) e [`TASKS.md`](TASKS.md). As decisões de adoção de capacidades transversais do Sentinel estão em [`docs/23-SENTINEL-ADAPTACAO.md`](docs/23-SENTINEL-ADAPTACAO.md).

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
docker compose up -d
```

A aplicação completa (API + workers + frontend) com o Docker:

```bash
docker compose --profile app up -d --build
```

Frontend em `http://localhost:8080` (redireciona para `https://localhost:8443`,
com certificado autoassinado — aceite no navegador). A API e os workers falam
com o PostgreSQL, Redis e RabbitMQ pela rede interna do compose; nenhuma porta
da API é publicada no host além das do frontend.

A plataforma de logs (ADR-021/ADR-022) com observabilidade:

```bash
docker compose --profile app --profile obs up -d --build
```

- Seq (logs centralizados) em `http://localhost:5341` — acesso com o usuário e
  senha de `SEQ_AUTH_USER`/`SEQ_AUTH_PASSWORD` no `.env` (dev: `admin`/`admin`).
  O serviço Seq em si não publica porta; quem publica é o nginx com basic auth.
- Coletor OpenTelemetry em `localhost:4317` (sem backend de tracing em
  homologação — despeja em console).

IA (Ollama) com escalabilidade horizontal (ADR-041):

```bash
docker compose --profile ai up -d
```

- 3 réplicas do Ollama + nginx load balancer em `http://localhost:11434`
- Modelo padrão: `qwen2.5-coder:1.5b` (baixar em cada réplica: `docker exec infracontrol-ollama-1 ollama pull qwen2.5-coder:1.5b`)
- Load balancer: `ollama-lb` (DNS interno — a API usa `http://ollama-lb`)
- IP estáveis configuráveis: `OLLAMA_1_IP`, `OLLAMA_2_IP`, `OLLAMA_3_IP`

Para conferir os containers:

```bash
docker compose ps
```

Não use credenciais reais em arquivos versionados. As credenciais do Compose são destinadas somente ao ambiente de desenvolvimento local.

Para escala manual, overlay de produção, volumes compartilhados, health checks,
backup/restore, carga k6 e testes controlados de falha, consulte
[docs/40-SCALE-HORIZONTAL-OPERATIONS.md](docs/40-SCALE-HORIZONTAL-OPERATIONS.md).
O overlay de produção exige segredos, tags de imagem, certificados TLS e
limites de recursos configurados no `.env`; não o use com os valores de
desenvolvimento.

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

> **Login por cookie exige HTTPS.** O cookie de sessão é `Secure` (além de
> `HttpOnly` e `SameSite=Strict`), então em desenvolvimento rode a API no perfil
> `https` com um certificado de dev:
>
> ```bash
> dotnet dev-certs https --trust          # uma vez por máquina
> dotnet run --project src/InfraControl.Api --launch-profile https
> ```
>
> O proxy do Vite já aponta para `https://localhost:7237` (perfil https) e aceita
> o certificado self-signed; para outro alvo, defina `VITE_DEV_API_TARGET` em
> `frontend/InfraControl.Web/.env.local`.

A API expõe health checks em:

```text
/health
/health/ready
/health/live
/health/startup
```

O nginx repassa esses caminhos na mesma origem do app (o mesmo vale para o
proxy do Vite em dev, junto com `/api` e `/hubs`). Por isso a página de saúde
da SPA ficou em `/status` — se ela voltar para `/health`, disputa a rota com a
API.

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

- Permissions;
- Convites;
- Aceite e rejeição de convites;
- Alteração e recuperação de senha;
- Auditoria.

O domínio de Identity já contém os agregados `User` e `UserSession`, as roles `Admin`, `User` e `Viewer` e eventos para criação, alteração de role e ciclo de sessão. O ASP.NET Core Identity permanece como adaptador técnico para credenciais e persistência de autenticação.

O Bootstrap Admin está implementado como caso de uso em `Application`, com persistência delegada por uma porta ao `Infrastructure`. Ele é opt-in e pode ser ativado com `BootstrapAdmin__Enabled=true`; email, nome e senha devem vir de variáveis de ambiente ou secret manager. A operação é idempotente e não registra a senha.

O RBAC inicial segue Vertical Slice/CQRS: comandos e handlers ficam em `Application`, o domínio mantém as invariantes do usuário, e `Infrastructure` sincroniza as roles e claims de permissão com o ASP.NET Core Identity. As policies HTTP legadas são `AdminOnly`, `UserAccess` e `ViewerAccess`; as policies granulares usam os nomes de `Permission` (`DashboardView`, `InventoryRead`, `IdentityRolesManage` etc.). A matriz padrão está em `Domain/Authorization/RolePermissions`; auditoria continua planejada.

O roadmap completo está em [`TASKS.md`](TASKS.md). Cada Task deve ser executada isoladamente, com escopo, teste, evidência e atualização de estado. A adaptação transversal do Sentinel segue o plano em [`docs/23-SENTINEL-ADAPTACAO.md`](docs/23-SENTINEL-ADAPTACAO.md); retenção e cleanup operacional continuam pendentes.

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
