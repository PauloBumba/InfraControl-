# Estrutura MVC da API

## Objetivo

A API do InfraControl segue a organização web do Dashboard do Sentinel Stack sem copiar suas regras de negócio. A camada HTTP é organizada como MVC modular: controllers por área, ViewModels para contratos de transporte, Services para adaptadores específicos da API, HealthChecks, Middleware e Hubs para recursos transversais.

## Estrutura

```text
src/InfraControl.Api/
├── Controllers/
│   ├── BaseController.cs
│   ├── Identity/
│   ├── Admin/ (users, roles)
│   ├── Dashboard/
│   ├── Agents/
│   ├── Incidents/
│   ├── Integrations/
│   ├── Webhooks/
│   └── Health/
├── ViewModels/
│   ├── Identity/
│   └── Admin/
├── Services/
├── HealthChecks/
├── Middleware/
├── Hubs/
└── Program.cs
```

## Regra de fronteira

Controllers recebem e devolvem HTTP, fazem binding de ViewModels e delegam para contratos da Application. ViewModels não são entidades de domínio nem comandos internos. Services da API são adaptadores de composição HTTP, enquanto casos de uso, validação e invariantes permanecem em `InfraControl.Application` e `InfraControl.Domain`.

A API continua sendo RESTful e pode servir o frontend React. A organização MVC é estrutural e não implica adoção de Razor Views; a pasta `Views` só deve ser criada se o projeto passar a renderizar páginas server-side.

## Identity atual

Os controllers `Identity/AuthenticationController` e `Admin/UsersController` expõem registro, login, refresh, logout, recuperação de senha, alteração de senha, usuário atual e administração de usuários/roles (atribuição e remoção de papéis em `POST/DELETE {id}/roles/{role}`; o antigo `Admin/RolesController`, em `api/identity/users`, foi removido no F18-T004 por duplicar o DELETE e ficar fora do padrão v1). Os contratos HTTP vivem em `ViewModels/Identity` e `ViewModels/Admin`, mantendo os records de Application protegidos da camada de transporte. O antigo diretório `Endpoints` foi removido para evitar dois estilos HTTP concorrentes.
