#!/usr/bin/env python3
"""Compose agent — API HTTP mínima para escala real de serviços Compose.

Decisão C do docs/65 (Fase 3): container privilegiado dedicado em vez de
agente systemd no host ou CLI dentro do processo da API.

Endpoints (rede interna do compose, sem porta publicada):
  GET  /health                      -> {"ok": true}          (sem auth)
  GET  /services                    -> serviços + réplicas  (auth)
  POST /services/{svc}/scale        -> {"replicas": N}       (auth)

Auth: se AGENT_TOKEN estiver definido, exige header `X-Agent-Token`
igual (compare_digest). Sem token configurado, a rede interna do
compose é a única barreira — o serviço não publica porta no host.

Comandos docker executados:
  - listagem: `docker ps -a --filter label=com.docker.compose.project=...`
    (não depende de profiles do compose — lê as labels dos containers);
  - escala:   `docker compose --project-name P -f FILE up -d
    --no-deps --scale svc=N svc` no diretório do projeto montado
    read-only. O serviço alvo explícito faz o compose auto-habilitar o
    profile dele; --no-deps impede que a operação suba dependências
    paradas (postgres, redis etc.) como efeito colateral.
"""

import hmac
import json
import os
import re
import subprocess
import sys
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

PORT = int(os.environ.get("AGENT_PORT", "8080"))
PROJECT_DIR = os.environ.get("PROJECT_DIR", "/project")
PROJECT_NAME = os.environ.get("COMPOSE_PROJECT_NAME", "infracontrol-")
COMPOSE_FILE = os.environ.get("COMPOSE_FILE", "docker-compose.yml")
TOKEN = os.environ.get("AGENT_TOKEN", "")

MAX_REPLICAS = 16
SERVICE_RE = re.compile(r"^[a-z0-9][a-z0-9_-]{0,62}$")
PS_TIMEOUT = 20
SCALE_TIMEOUT = 240

os.chdir(PROJECT_DIR)


def run(cmd, timeout):
    """Executa comando e devolve (rc, stdout+stderr). Nunca lança."""
    env = dict(os.environ)
    # COMPOSE_FILE aceita lista separada por ':' — a CLI resolve nativamente;
    # não dá para repassar via -f (que aceita um arquivo por flag).
    env["COMPOSE_FILE"] = COMPOSE_FILE
    env["COMPOSE_PROJECT_NAME"] = PROJECT_NAME
    try:
        proc = subprocess.run(
            cmd,
            capture_output=True,
            text=True,
            timeout=timeout,
            cwd=PROJECT_DIR,
            env=env,
        )
        return proc.returncode, (proc.stdout or "") + (proc.stderr or "")
    except subprocess.TimeoutExpired:
        return 124, f"timeout após {timeout}s: {' '.join(cmd)}"
    except Exception as exc:  # noqa: BLE001 — agente não pode derrubar
        return 1, f"falha ao executar {' '.join(cmd)}: {exc}"


def compose_base():
    # Sem -f: COMPOSE_FILE (env) já é resolvido pela CLI — suporta overlay
    # "a.yml:b.yml" que -f não conseguiria expressar.
    return [
        "docker", "compose",
        "--project-name", PROJECT_NAME,
    ]


def list_services():
    """Agrupa containers do projeto por serviço Compose (labels reais)."""
    rc, out = run(
        [
            "docker", "ps", "-a",
            "--filter", f"label=com.docker.compose.project={PROJECT_NAME}",
            "--format", "{{json .}}",
        ],
        PS_TIMEOUT,
    )
    if rc != 0:
        return None, out.strip() or "docker ps falhou"

    services = {}
    for line in out.splitlines():
        line = line.strip()
        if not line:
            continue
        try:
            container = json.loads(line)
        except json.JSONDecodeError:
            continue
        labels = container.get("Labels") or container.get("Label") or ""
        # `docker ps --format '{{json .}}'` devolve Labels como string
        # "k=v,k=v" — extrai o serviço Compose do label.
        service = None
        for part in labels.split(","):
            if part.startswith("com.docker.compose.service="):
                service = part.split("=", 1)[1]
                break
        if not service:
            continue
        entry = services.setdefault(service, {"containers": []})
        state = (container.get("State") or "").lower()
        entry["containers"].append({
            "name": (container.get("Names") or "").lstrip("/"),
            "state": state,
            "status": container.get("Status") or "",
        })

    result = []
    for name in sorted(services):
        containers = services[name]["containers"]
        running = sum(1 for c in containers if c["state"] == "running")
        result.append({
            "service": name,
            "total": len(containers),
            "running": running,
            "containers": sorted(containers, key=lambda c: c["name"]),
        })
    return result, None


def scale_service(service, replicas):
    """`docker compose up -d --no-deps --scale svc=N svc`."""
    cmd = compose_base() + [
        "up", "-d", "--no-deps",
        "--scale", f"{service}={replicas}",
        service,
    ]
    rc, out = run(cmd, SCALE_TIMEOUT)
    return rc == 0, out.strip()[-4000:]  # cauda da saída p/ a UI


class Handler(BaseHTTPRequestHandler):
    server_version = "InfraControlComposeAgent/1.0"

    def _send(self, status, payload):
        body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def _authorized(self):
        if not TOKEN:
            return True
        provided = self.headers.get("X-Agent-Token", "")
        return hmac.compare_digest(provided, TOKEN)

    def _require_auth(self):
        if self._authorized():
            return True
        self._send(401, {"error": "token do agente ausente ou inválido"})
        return False

    def do_GET(self):
        path = self.path.split("?", 1)[0].rstrip("/") or "/"
        if path == "/health":
            self._send(200, {"ok": True, "project": PROJECT_NAME})
            return
        if not self._require_auth():
            return
        if path == "/services":
            services, error = list_services()
            if services is None:
                self._send(502, {"error": error})
            else:
                self._send(200, {"project": PROJECT_NAME, "services": services})
            return
        self._send(404, {"error": "rota não encontrada"})

    def do_POST(self):
        path = self.path.split("?", 1)[0].rstrip("/")
        match = re.fullmatch(r"/services/([a-zA-Z0-9_-]+)/scale", path)
        if not match:
            self._send(404, {"error": "rota não encontrada"})
            return
        if not self._require_auth():
            return

        try:
            length = int(self.headers.get("Content-Length") or 0)
            body = json.loads(self.rfile.read(length) or b"{}")
        except (ValueError, json.JSONDecodeError):
            self._send(400, {"error": "corpo JSON inválido"})
            return

        service = match.group(1)
        replicas = body.get("replicas")
        if not SERVICE_RE.fullmatch(service):
            self._send(400, {"error": "nome de serviço inválido"})
            return
        if not isinstance(replicas, int) or isinstance(replicas, bool) \
                or not (0 <= replicas <= MAX_REPLICAS):
            self._send(400, {
                "error": f"réplicas deve ser inteiro entre 0 e {MAX_REPLICAS}",
            })
            return

        ok, output = scale_service(service, replicas)
        self._send(200 if ok else 502, {
            "service": service,
            "replicas": replicas,
            "success": ok,
            "output": output,
        })

    def log_message(self, fmt, *args):
        # Log enxuto no stdout do container (visível via `docker logs`).
        sys.stderr.write("%s - %s\n" % (self.address_string(), fmt % args))


def main():
    server = ThreadingHTTPServer(("0.0.0.0", PORT), Handler)
    sys.stderr.write(
        f"compose-agent ouvindo em :{PORT} projeto={PROJECT_NAME} "
        f"dir={PROJECT_DIR} file={COMPOSE_FILE} auth={'on' if TOKEN else 'off'}\n"
    )
    server.serve_forever()


if __name__ == "__main__":
    main()
