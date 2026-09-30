# ADR-022 — Empacotamento Docker e compose unificado

- **Status**: aceito
- **Data**: 2026-09-29
- **Contexto**: preparação para integrar o dashboard (React/Vite) ao backend

## Problema

O repositório não tem **um único Dockerfile**. Os dois arquivos compose
(`docker-compose.yml` e `docker-compose.infrastructure.yml`) sobem apenas
postgres, redis, rabbitmq e adminer. API, workers e frontend rodam na máquina do
desenvolvedor, cada um à sua maneira.

Isso bloqueia a integração por um motivo concreto, não por estética: o cookie de
sessão é `SameSite=Strict` e `Secure` (ADR-008/ADR-017). Com o frontend em
`localhost:3000` e a API em `https://localhost:7237`, são **origens diferentes**,
e o cookie não viaja. Só funciona porque o Vite faz proxy em desenvolvimento.
Não há caminho de produção, e a tela `/logs` não tem onde consultar nada porque o
Seq não sobe.

## Decisão

### 1. Um Dockerfile, três targets

`docker/Dockerfile` com estágios e *targets* `api`, `workers` e `frontend`.
Um arquivo só porque os três precisam do mesmo SDK e do mesmo `.slnx`; a
separação em três arquivos duplicaria o restore e o `COPY` de todo o contexto.

- API e workers: `dotnet publish` em runtime não-root.
- Frontend: build Vite em imagem Node, servido por nginx na imagem final.
- Sem `USER root` no estágio final, sem SDK na imagem de execução.

### 2. nginx serve o frontend e é o proxy da API

Uma origem só. nginx atende o React e encaminha `/api/v1` e `/hubs` para a API.

Isso é o que **elimina** a necessidade de CORS: mesma origem, o cookie
`SameSite=Strict` volta a valer, e `Secure` funciona porque há TLS no nginx
(certificado do próprio serviço, como já é o caso em `launchSettings.json`).

Não vamos abrir a API em CORS para o dashboard. CORS com `AllowCredentials` para
origem de desenvolvimento exigiria `Set-Cookie` com domínio específico e
`SameSite=None`, que é mais frouxo que a proteção que se quer manter.

### 3. Compose único, com profiles

`docker-compose.yml` passa a ser a única entrada:

| Profile | Serviços |
|---|---|
| *(sem profile)* | postgres, redis, rabbitmq, adminer |
| `app` | api, workers, frontend |
| `obs` | seq, otel-collector |

`services` sem `profiles` sobem por padrão, então **`docker compose up -d`
continua funcionando** como o README já documenta — o comando atual não quebra.
`--profile app` sobe a aplicação, `--profile obs` a observabilidade.

`docker-compose.infrastructure.yml` sai: o arquivo único já cobre o caso e
manter os dois é a forma garantida de os dois divergirem.

### 4. Seq não publica porta

Seq fica **sem `ports:`**, só na rede interna. Publicar `5341` na máquina do
host deixaria a plataforma de logs da aplicação — que inclui caminho de requisição
e erro — alcançável por qualquer processo local e por qualquer coisa na rede,
sem autenticação do Seq configurada.

O acesso é pelo nginx, com **basic auth** de arquivo `.htpasswd` (segredo em
`.env`, arquivo fora do git). É a proteção mínima aceitável para um ambiente de
homologação; integrar ao login da aplicação é uma decisão de segurança à parte e
não foi feita aqui — o Seq não valida token do ASP.NET.

### 5. Credenciais em `.env`

Compose novo não repete senha em texto no YAML. `.env.example` versionado
documenta as chaves; `.env` real fica no `.gitignore` (já está lá). As senhas
de desenvolvimento que já estavam em texto no compose (postgres, rabbitmq) são
mantidas como valor padrão, para não quebrar ambiente existente, mas agora
passam a poder ser sobrescritas.

## Consequências

**Bom**

- `docker compose --profile app --profile obs up -d` sobe o sistema inteiro,
  frontend incluído, com login funcionando como em produção.
- CORS deixa de ser problema: some do código e da superfície de ataque.
- O admin tem o Seq para consultar e o rastro de requisição existe.

**Ruim**

- TLS: em desenvolvimento será necessário um certificado local. O compose
  provisiona um autoassinado por padrão; o navegador vai reclamar até ser
  aceito. Documentado, não escondido.
- _basic auth_ do Seq é mais fraco que o login da aplicação. Aceito para
  homologação; anotado como dívida.
- A imagem da API precisa do `.slnx` inteiro no contexto de build, o que
  aumenta o contexto de Docker. `.dockerignore` reduz isso.

## Alternativas rejeitadas

- **CORS liberado para o dashboard**: `SameSite=None` + `AllowCredentials` é
  exatamente a configuração que enfraquece a proteção de cookie descrita acima.
- **Publicar a API direto na host e servir o frontend pelo Vite**: só funciona em
  desenvolvimento e não entrega o caminho de produção que falta.
- **Manter `docker-compose.infrastructure.yml`**: nenhum ganho; o risco é os
  dois arquivos divergirem sobre rede, volume e healthcheck.
- **Traefik/Caddy como proxy**: automático, mas introduz um conceito novo e um
  container a mais para resolver um problema de um serviço. nginx resolve em
  dez linhas.
