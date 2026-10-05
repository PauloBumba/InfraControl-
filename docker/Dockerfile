# syntax=docker/dockerfile:1
# ADR-022 — um único Dockerfile, três targets: api | workers | frontend.
# Contexto de build = raiz do repositório (o .dockerignore limpa o resto).
#
#   docker build -t infracontrol-api --target api .
#   docker build -t infracontrol-workers --target workers .
#   docker build -t infracontrol-frontend --target frontend .

# ------------------------------------------------ base de build (.NET) -------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-build
WORKDIR /src

# Restore antes do COPY de tudo: só os .csproj primeiro, para o layer de
# dependências não invalidar quando o código muda (os targets compartilham isso).
COPY global.json ./
COPY src/InfraControl.BuildingBlocks/InfraControl.BuildingBlocks.csproj src/InfraControl.BuildingBlocks/
COPY src/InfraControl.Contracts/InfraControl.Contracts.csproj src/InfraControl.Contracts/
COPY src/InfraControl.Domain/InfraControl.Domain.csproj src/InfraControl.Domain/
COPY src/InfraControl.Application/InfraControl.Application.csproj src/InfraControl.Application/
COPY src/InfraControl.Infrastructure/InfraControl.Infrastructure.csproj src/InfraControl.Infrastructure/
COPY src/InfraControl.Api/InfraControl.Api.csproj src/InfraControl.Api/
COPY src/InfraControl.Workers/InfraControl.Workers.csproj src/InfraControl.Workers/
RUN dotnet restore src/InfraControl.Api/InfraControl.Api.csproj \
 && dotnet restore src/InfraControl.Workers/InfraControl.Workers.csproj

COPY . .

FROM dotnet-build AS api-build
RUN dotnet publish src/InfraControl.Api/InfraControl.Api.csproj \
    -c Release -o /out/api --no-restore

FROM dotnet-build AS workers-build
RUN dotnet publish src/InfraControl.Workers/InfraControl.Workers.csproj \
    -c Release -o /out/workers --no-restore

# ------------------------------------------------ runtime da API ------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
COPY --from=api-build /out/api ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "InfraControl.Api.dll"]

# ------------------------------------------------ runtime dos workers --------
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS workers
WORKDIR /app
# Instala iputils-ping para suporte a ping ICMP (base Debian/Ubuntu)
RUN apt-get update && apt-get install -y iputils-ping && rm -rf /var/lib/apt/lists/*
COPY --from=workers-build /out/workers ./
USER $APP_UID
ENTRYPOINT ["dotnet", "InfraControl.Workers.dll"]

# ------------------------------------------------ build do frontend (Vite) ---
FROM node:22-alpine AS frontend-build
WORKDIR /app
COPY frontend/InfraControl.Web/package*.json ./
RUN npm ci
COPY frontend/InfraControl.Web/ ./

# VITE_* é embutido no bundle no build (o Vite só expõe o que começa com
# VITE_). Sem isto o login Google não aparece em produção.
ARG VITE_GOOGLE_CLIENT_ID=
ARG VITE_API_BASE_URL=
# URL da plataforma de logs (Seq) para o botão da LogsPage — em produção é o
# host+porta publicados pelo nginx (SEQ_UI_PORT). Default = porta do dev.
ARG VITE_LOGS_URL=http://localhost:5341
ENV VITE_GOOGLE_CLIENT_ID=$VITE_GOOGLE_CLIENT_ID \
    VITE_API_BASE_URL=$VITE_API_BASE_URL \
    VITE_LOGS_URL=$VITE_LOGS_URL

RUN npm run build

# ------------------------------------------------ nginx (frontend + proxy) --
FROM nginx:1.27-alpine AS frontend
# Certificado autoassinado de dev (ADR-022: TLS no nginx para o cookie Secure):
# o navegador vai reclamar uma vez; aceite e siga. Troque por um certificado
# real em produção.
RUN apk add --no-cache openssl apache2-utils \
 && mkdir -p /etc/nginx/ssl \
 && openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
    -keyout /etc/nginx/ssl/server.key \
    -out /etc/nginx/ssl/server.crt \
    -subj "/CN=infracontrol.local"

COPY --from=frontend-build /app/dist /usr/share/nginx/html
# nginx.conf é TEMPLATE (não config final): o entrypoint do nginx oficial roda
# envsubst em /etc/nginx/templates/*.template e grava em NGINX_ENVSUBST_OUTPUT_DIR,
# injetando NGINX_HTTPS_PORT no redirect HTTP→HTTPS (a porta do host não é
# deduzível dentro do container). Default 443 para o caso de subir sem compose.
ENV NGINX_ENVSUBST_OUTPUT_DIR=/etc/nginx \
    NGINX_HTTPS_PORT=443
COPY docker/nginx.conf /etc/nginx/templates/nginx.conf.template
# Cria o .htpasswd do Seq a partir das envs SEQ_AUTH_USER/SEQ_AUTH_PASSWORD
# antes do nginx subir (ADR-022: Seq atrás de basic auth, porta sem exposição
# direta).
COPY docker/entrypoint-seq-auth.sh /docker-entrypoint.d/30-seq-auth.sh
RUN chmod +x /docker-entrypoint.d/30-seq-auth.sh

EXPOSE 80 443 5341