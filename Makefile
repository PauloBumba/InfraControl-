.PHONY: help up down build rebuild restart logs ps health dev prod test front-test back-test clean

help:
	@echo "InfraControl Makefile"
	@echo "  make up        - Sobe containers (app profile)"
	@echo "  make down      - Para containers"
	@echo "  make build     - Build dos containers"
	@echo "  make rebuild   - Build sem cache e sobe"
	@echo "  make restart   - Restart containers"
	@echo "  make logs      - Logs em tempo real"
	@echo "  make ps        - Status dos containers"
	@echo "  make health    - Health checks"
	@echo "  make dev       - Sobe dev (infra + app)"
	@echo "  make prod      - Sobe produção"
	@echo "  make test      - Roda todos os testes"
	@echo "  make front-test - Testes frontend"
	@echo "  make back-test - Testes backend"
	@echo "  make update    - Rebuild sem cache frontend/api/workers e restart"

up:
	docker compose --profile app up -d

down:
	docker compose down

build:
	docker compose --profile app build

rebuild:
	docker compose --profile app build --no-cache && docker compose --profile app up -d

restart:
	docker compose restart

logs:
	docker compose logs -f

ps:
	docker compose ps

health:
	curl -k https://localhost:8443/health || true

dev:
	docker compose up -d && docker compose --profile app up -d

prod:
	docker compose -f docker-compose.yml -f docker-compose.production.yml up -d

test:
	dotnet test InfraControl.slnx

front-test:
	cd frontend/InfraControl.Web && npm run test

back-test:
	dotnet test InfraControl.slnx --filter "FullyQualifiedName~Unit|FullyQualifiedName~Integration"

update:
	docker compose build --no-cache api workers frontend && docker compose up -d api workers frontend

clean:
	docker compose down -v
