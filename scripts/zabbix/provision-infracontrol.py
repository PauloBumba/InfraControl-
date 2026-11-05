#!/usr/bin/env python3
"""F46-T007 — provisionamento idempotente do Zabbix para a stack InfraControl.

Uso (dev local, credenciais documentadas em docs/27):
    python3 scripts/zabbix/provision-infracontrol.py

Sobrescritas por env: ZBX_API_URL, ZBX_USER, ZBX_PASSWORD, POSTGRES_USER,
POSTGRES_PASSWORD, POSTGRES_DB, REDIS_PASSWORD, RABBITMQ_USER, RABBITMQ_PASSWORD.
"""

import base64
import json
import os
import sys
import urllib.error
import urllib.request

API_URL = os.environ.get("ZBX_API_URL", "http://localhost:8086/api_jsonrpc.php")
ZBX_USER = os.environ.get("ZBX_USER", "Admin")
ZBX_PASSWORD = os.environ.get("ZBX_PASSWORD", "zabbix")

AGENT_HOST = os.environ.get("ZBX_AGENT_HOST", "infracontrol-zabbix-agent")
AGENT_PORT = os.environ.get("ZBX_AGENT_PORT", "10050")

PG_HOST = os.environ.get("PG_HOST", "infracontrol-postgres")
PG_USER = os.environ.get("POSTGRES_USER", "infracontrol")
PG_PASSWORD = os.environ.get("POSTGRES_PASSWORD", "infracontrol_dev_2026")
PG_DB = os.environ.get("POSTGRES_DB", "infracontrol")

REDIS_HOST = os.environ.get("REDIS_HOST_CONTAINER", "infracontrol-redis-master")
REDIS_PASSWORD = os.environ.get("REDIS_PASSWORD", "redis_dev_password_2026")

RABBIT_HOST = os.environ.get("RABBIT_HOST", "infracontrol-rabbitmq")
RABBIT_USER = os.environ.get("RABBITMQ_USER", "infracontrol")
RABBIT_PASSWORD = os.environ.get("RABBITMQ_PASSWORD", "infracontrol_dev_2026")

GROUP = "InfraControl"
WEB_SCENARIO_HOST = "infracontrol-services"
WEB_SCENARIO_NAME = "InfraControl endpoints"

_session = {"token": None}


def rpc(method, params, authenticated=True):
    body = {"jsonrpc": "2.0", "method": method, "params": params, "id": 1}
    headers = {"Content-Type": "application/json"}
    if authenticated:
        headers["Authorization"] = "Bearer " + _session["token"]
    req = urllib.request.Request(API_URL, data=json.dumps(body).encode(), headers=headers)
    try:
        resp = json.loads(urllib.request.urlopen(req, timeout=30).read())
    except urllib.error.HTTPError as exc:
        raise SystemExit(f"HTTP {exc.code} em {method}: {exc.read().decode()[:400]}")
    if "error" in resp:
        raise SystemExit(f"erro {method}: {resp['error']}")
    return resp["result"]


def login():
    _session["token"] = rpc(
        "user.login", {"username": ZBX_USER, "password": ZBX_PASSWORD}, authenticated=False
    )


def ensure_group():
    found = rpc("hostgroup.get", {"filter": {"name": GROUP}, "output": ["groupid"]})
    if found:
        print(f"  group '{GROUP}' já existe (id {found[0]['groupid']})")
        return found[0]["groupid"]
    created = rpc("hostgroup.create", {"name": GROUP})
    print(f"  group '{GROUP}' criado (id {created['groupids'][0]})")
    return created["groupids"][0]


def get_host(host):
    found = rpc(
        "host.get",
        {"filter": {"host": host}, "output": ["hostid"],
         "selectParentTemplates": ["host"], "selectInterfaces": ["interfaceid", "ip", "dns", "port", "useip"]},
    )
    return found[0] if found else None


def ensure_macros(hostid, macros):
    existing = {
        m["macro"]: m
        for m in rpc("usermacro.get", {"hostids": hostid, "output": ["hostmacroid", "macro", "value"]})
    }
    for macro, value in macros.items():
        current = existing.get(macro)
        if current is None:
            rpc("usermacro.create", {"hostid": hostid, "macro": macro, "value": value})
            print(f"    macro {macro} criada")
        elif current["value"] != value:
            rpc("usermacro.update", {"hostmacroid": current["hostmacroid"], "value": value})
            print(f"    macro {macro} atualizada")


def templateid(template_host):
    found = rpc("template.get", {"filter": {"host": template_host},
                                 "output": ["templateid"]})
    if not found:
        raise SystemExit(f"template '{template_host}' não existe no Zabbix")
    return found[0]["templateid"]


def ensure_templates(hostid, templates):
    record = rpc("host.get", {"hostids": hostid, "output": ["hostid"],
                              "selectParentTemplates": ["host"]})[0]
    linked = {t["host"] for t in record.get("parentTemplates", [])}
    desired = set(templates)
    if linked == desired:
        return
    rpc("host.update", {"hostid": hostid,
                        "templates": [{"templateid": templateid(t)} for t in sorted(desired)]})
    print(f"    templates → {sorted(desired)}")


def ensure_interface(hostid, ip, port):
    host = get_host_by_id(hostid)
    interfaces = host.get("interfaces", [])
    if not interfaces:
        rpc("hostinterface.create", {"hostid": hostid, "type": 1, "main": 1,
                                     "useip": 0, "ip": "", "dns": ip, "port": port})
        print(f"    interface agent {ip}:{port} criada")
        return
    iface = interfaces[0]
    current = iface["ip"] if iface.get("useip") == "1" else iface["dns"]
    if current != ip or iface["port"] != port:
        rpc("hostinterface.update", {"interfaceid": iface["interfaceid"],
                                     "useip": 0, "ip": "", "dns": ip, "port": port})
        print(f"    interface agent ajustada → {ip}:{port}")


def get_host_by_id(hostid):
    return rpc("host.get", {"hostids": hostid, "output": ["hostid"],
                            "selectInterfaces": ["interfaceid", "ip", "dns", "port", "useip"]})[0]


def ensure_host(host, templates, macros):
    groupid = ensure_group_cached()
    record = get_host(host)
    if record is None:
        created = rpc("host.create", {
            "host": host, "name": host, "status": 0,
            "groups": [{"groupid": groupid}],
            "interfaces": [{"type": 1, "main": 1, "useip": 0, "ip": "",
                            "dns": AGENT_HOST, "port": AGENT_PORT}],
            "templates": [{"templateid": templateid(t)} for t in templates],
            "macros": [{"macro": k, "value": v} for k, v in macros.items()],
        })
        print(f"  host '{host}' criado (id {created['hostids'][0]}) templates={templates}")
        return created["hostids"][0]
    print(f"  host '{host}' já existe (id {record['hostid']}) — garantindo templates/macros")
    ensure_interface(record["hostid"], AGENT_HOST, AGENT_PORT)
    ensure_templates(record["hostid"], templates)
    ensure_macros(record["hostid"], macros)
    return record["hostid"]


_group_cache = {"id": None}


def ensure_group_cached():
    if _group_cache["id"] is None:
        _group_cache["id"] = ensure_group()
    return _group_cache["id"]


def fix_zabbix_server_interface():
    record = get_host("Zabbix server")
    if record is None:
        print("  host 'Zabbix server' não encontrado — pulando correção de interface")
        return
    iface = record.get("interfaces", [{}])[0]
    current = iface.get("ip") if iface.get("useip") == "1" else iface.get("dns")
    if current == AGENT_HOST and iface.get("port") == AGENT_PORT:
        print("  host 'Zabbix server' já aponta para o agent da stack")
        return
    rpc("hostinterface.update", {"interfaceid": iface["interfaceid"],
                                 "useip": 0, "ip": "", "dns": AGENT_HOST,
                                 "port": AGENT_PORT})
    print(f"  host 'Zabbix server': interface {current}:{iface.get('port')}"
          f" → {AGENT_HOST}:{AGENT_PORT} (agent real da stack)")


def rabbit_node_info():
    token = base64.b64encode(f"{RABBIT_USER}:{RABBIT_PASSWORD}".encode()).decode()
    urls = [
        f"http://localhost:{os.environ.get('RABBIT_MGMT_HOST_PORT', '15673')}/api/nodes",
        f"http://{RABBIT_HOST}:15672/api/nodes",
    ]
    for url in urls:
        req = urllib.request.Request(url, headers={"Authorization": "Basic " + token})
        try:
            nodes = json.loads(urllib.request.urlopen(req, timeout=8).read())
            cluster = nodes[0].get("cluster_name", "rabbit")
            node = nodes[0].get("name", "").split("@")[-1]
            return cluster, node
        except Exception:
            continue
    return "", ""


def ensure_web_scenario(hostid):
    existing = rpc("httptest.get", {"hostids": hostid, "output": ["httptestid", "name"]})
    match = next((s for s in existing if s["name"] == WEB_SCENARIO_NAME), None)
    steps = [
        {"no": 1, "name": "frontend", "url": "http://frontend/",
         "status_codes": "200,301,302", "follow_redirects": 1, "timeout": "15s"},
        {"no": 2, "name": "api health", "url": "http://api:8080/health",
         "status_codes": "200", "follow_redirects": 1, "timeout": "15s"},
        {"no": 3, "name": "rabbitmq mgmt", "url": "http://rabbitmq:15672/",
         "status_codes": "200,302", "follow_redirects": 1, "timeout": "15s"},
        {"no": 4, "name": "zabbix web ping", "url": "http://zabbix-web:8080/ping",
         "status_codes": "200", "follow_redirects": 1, "timeout": "15s"},
        {"no": 5, "name": "evolution api", "url": "http://evolution-api:8080/",
         "status_codes": "200,404", "follow_redirects": 1, "timeout": "15s"},
        {"no": 6, "name": "guacamole", "url": "http://guacamole:8080/guacamole/",
         "status_codes": "200,302", "follow_redirects": 1, "timeout": "15s"},
        {"no": 7, "name": "adminer", "url": "http://adminer:8080/",
         "status_codes": "200", "follow_redirects": 1, "timeout": "15s"},
    ]
    if match is None:
        created = rpc("httptest.create", {"hostid": hostid, "name": WEB_SCENARIO_NAME,
                                             "delay": "1m", "steps": steps})
        print(f"  web scenario '{WEB_SCENARIO_NAME}' criado "
              f"(id {created['httptestids'][0]}, {len(steps)} steps)")
        scenario_id = created["httptestids"][0]
    else:
        scenario_id = match["httptestid"]
        rpc("httptest.update", {"httptestid": scenario_id,
                                "name": WEB_SCENARIO_NAME, "delay": "1m", "steps": steps})
        print(f"  web scenario '{WEB_SCENARIO_NAME}' já existe (id {scenario_id}) — steps garantidos")
    ensure_endpoint_health(hostid)


ENDPOINTS = [
    ("frontend", "http", "frontend", "80"),
    ("api", "http", "api", "8080"),
    ("rabbitmq", "http", "rabbitmq", "15672"),
    ("zabbix-web", "http", "zabbix-web", "8080"),
    ("evolution", "http", "evolution-api", "8080"),
    ("guacamole", "http", "guacamole", "8080"),
    ("adminer", "http", "adminer", "8080"),
]


def ensure_endpoint_health(hostid):
    stale = rpc("trigger.get", {"filter": {"description":
        f"{WEB_SCENARIO_HOST}: endpoints indisponíveis"}})
    if stale:
        rpc("trigger.delete", [t["triggerid"] for t in stale])
        print("  trigger antiga web.test.fail removida (bug do trunk 8.0rc2: "
              "itens web.test.* não são criados)")
    existing_items = {i["key_"] for i in rpc(
        "item.get", {"hostids": hostid, "output": ["key_"]})}
    existing_triggers = {t["description"] for t in rpc(
        "trigger.get", {"search": {"description": WEB_SCENARIO_HOST},
                        "searchWildcardsEnabled": True, "output": ["description"]})}
    interfaceid = rpc("hostinterface.get", {"hostids": hostid,
                                            "output": ["interfaceid"]})[0]["interfaceid"]
    to_create = []
    for name, scheme, host, port in ENDPOINTS:
        service_key = f"net.tcp.service[{scheme},{host},{port}]"
        perf_key = f"net.tcp.service.perf[{scheme},{host},{port}]"
        if service_key not in existing_items:
            to_create.append({"hostid": hostid, "interfaceid": interfaceid,
                              "name": f"{name} disponível (TCP)",
                              "key_": service_key, "type": 0, "value_type": 3,
                              "delay": "1m", "history": "7d", "trends": "30d"})
        if perf_key not in existing_items:
            to_create.append({"hostid": hostid, "interfaceid": interfaceid,
                              "name": f"{name} latência TCP (ms)",
                              "key_": perf_key, "type": 0, "value_type": 0,
                              "delay": "1m", "history": "7d", "trends": "30d"})
    if to_create:
        created = rpc("item.create", to_create)
        print(f"  {len(created['itemids'])} itens de endpoint TCP criados "
              f"(disponibilidade + latência por serviço)")
    for name, scheme, host, port in ENDPOINTS:
        service_key = f"net.tcp.service[{scheme},{host},{port}]"
        description = f"{WEB_SCENARIO_HOST}: {name} offline"
        if description not in existing_triggers:
            rpc("trigger.create", {"description": description,
                                   "expression": f"last(/{WEB_SCENARIO_HOST}/{service_key})=0",
                                   "priority": 4})
    missing = [t for t in ENDPOINTS
               if f"{WEB_SCENARIO_HOST}: {t[0]} offline" not in existing_triggers]
    if missing:
        print(f"  {len(missing)} triggers de endpoint criadas")


def main():
    print(f"Zabbix: {API_URL} (user {ZBX_USER})")
    login()
    print(f"versão: {rpc('apiinfo.version', {}, authenticated=False)}")

    print("1) host do agent (autodiscovery Docker por container)")
    ensure_host("infracontrol-docker", ["Docker by Zabbix agent 2"], {})

    print("2) host do Zabbix server → interface do agent da stack")
    fix_zabbix_server_interface()

    print("3) PostgreSQL (infracontrol-postgres)")
    ensure_host("infracontrol-postgres", ["PostgreSQL by Zabbix agent 2"], {
        "{$PG.CONNSTRING.AGENT2}": f"tcp://{PG_HOST}:5432",
        "{$PG.USER}": PG_USER,
        "{$PG.PASSWORD}": PG_PASSWORD,
        "{$PG.DATABASE}": PG_DB,
    })

    print("4) Redis (infracontrol-redis-master)")
    ensure_host("infracontrol-redis", ["Redis by Zabbix agent 2"], {
        "{$REDIS.CONN.URI}": f"tcp://{REDIS_HOST}:6379",
        "{$REDIS.PASSWORD}": REDIS_PASSWORD,
        "{$REDIS.USERNAME}": "",
    })

    print("5) RabbitMQ (management API)")
    cluster, node = rabbit_node_info()
    print(f"    cluster={cluster!r} node={node!r}")
    ensure_host("infracontrol-rabbitmq", ["RabbitMQ node by Zabbix agent"], {
        "{$RABBITMQ.API.SCHEME}": "http",
        "{$RABBITMQ.API.HOST}": RABBIT_HOST,
        "{$RABBITMQ.API.PORT}": "15672",
        "{$RABBITMQ.API.USER}": RABBIT_USER,
        "{$RABBITMQ.API.PASSWORD}": RABBIT_PASSWORD,
        "{$RABBITMQ.CLUSTER.NAME}": cluster,
        "{$RABBITMQ.NODE.NAME}": node,
        "{$RABBITMQ.PROCESS.NAME.PARAMETER}": "beam.smp",
    })

    print("6) endpoints HTTP (latência/disponibilidade por serviço)")
    groupid = ensure_group_cached()
    hostid = ensure_host(WEB_SCENARIO_HOST, [], {})
    ensure_web_scenario(hostid)

    print("pronto.")


if __name__ == "__main__":
    main()
    sys.exit(0)
