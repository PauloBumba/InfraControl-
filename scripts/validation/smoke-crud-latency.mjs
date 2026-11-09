#!/usr/bin/env node
// ==============================================================================
// smoke-crud-latency.mjs — varre endpoints CRUD ao vivo (banco real), mede
// latência p50/p95 e respeita o rate limit (Default 300 req/60s).
// Uso: NODE_TLS_REJECT_UNAUTHORIZED=0 node smoke-crud-latency.mjs [baseURL]
// ==============================================================================
import { writeFileSync } from "node:fs";

const API = process.argv[2] || "https://localhost:8445/api/v1";
const ROOT = API.replace(/\/api\/v1\/?$/, "");
const EMAIL = process.env.IC_EMAIL || "admin@example.com";
const PASSWORD = process.env.IC_PASSWORD || "InfraControl_Dev_2026";

const jar = new Map();
let xsrf = "";
let budget = { used: 0, windowStart: Date.now(), limited: 0 };

function cookieHeader() {
  return [...jar.entries()].map(([k, v]) => `${k}=${v}`).join("; ");
}

// janela de rate limit: 300/60s — usa no máx. 240 para folga
function gate() {
  const now = Date.now();
  if (now - budget.windowStart >= 60_000) {
    budget = { used: 0, windowStart: now, limited: budget.limited };
  }
  if (budget.used >= 240) {
    const wait = 60_000 - (now - budget.windowStart) + 500;
    console.log(`[GATE] janela de rate limit cheia — aguardando ${wait}ms`);
    return new Promise(r => setTimeout(r, wait)).then(() => { budget.used = 0; budget.windowStart = Date.now(); });
  }
}

async function req(method, path, body) {
  await gate();
  budget.used++;
  const headers = { Accept: "application/json", Cookie: cookieHeader() };
  if (body !== undefined) headers["Content-Type"] = "application/json";
  if (xsrf) headers["X-XSRF-TOKEN"] = xsrf;
  const res = await fetch(`${API}${path}`, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
    redirect: "manual",
  });
  for (const c of res.headers.getSetCookie?.() ?? []) {
    const [pair] = c.split(";");
    const i = pair.indexOf("=");
    jar.set(pair.slice(0, i).trim(), pair.slice(i + 1).trim());
  }
  const text = await res.text();
  let json = null;
  try { json = text ? JSON.parse(text) : null; } catch { /* non-json */ }
  if (res.status === 429) {
    const retryAfter = Number(res.headers.get("retry-after") || 60);
    budget.limited++;
    console.log(`[429] ${method} ${path} — servidor pediu espera de ${retryAfter}s`);
    await new Promise(r => setTimeout(r, (retryAfter + 1) * 1000));
    budget.used = 0;
    budget.windowStart = Date.now();
    return req(method, path, body); // uma única repetição
  }
  return { status: res.status, json, text };
}

async function timed(method, path, body) {
  await gate(); // espera fora da medição
  const used0 = budget.used;
  const t0 = performance.now();
  const r = await req(method, path, body);
  const ms = performance.now() - t0;
  return { ...r, ms };
}

function expect(cond, msg) { if (!cond) throw new Error(msg); }

const results = [];
function record(domain, name, method, path, status, ms, ok, detail = "") {
  results.push({ domain, name, method, path, status, ms: Math.round(ms * 100) / 100, ok, detail });
  console.log(`[${ok ? "PASS" : "FAIL"}] ${domain} ${name} ${method} ${path} → ${status} ${Math.round(ms)}ms ${detail}`);
}

async function login() {
  const r = await timed("POST", "/auth/login", { email: EMAIL, password: PASSWORD });
  expect(r.status === 200 && r.json?.isSuccess, `login failed: ${r.status} ${r.text?.slice(0, 200)}`);
  await timed("GET", "/auth/me");
  xsrf = jar.get("XSRF-TOKEN") || "";
  expect(xsrf.length > 0, "no XSRF token");
  console.log(`login ok as ${EMAIL} (rate-limit window iniciada)\n`);
}

async function crud(domain, name, base, { create, update, keep = false } = {}) {
  try {
    let r = await timed("GET", `${base}?take=3`);
    expect(r.status === 200, `list status ${r.status}`);
    record(domain, `${name} list`, "GET", `${base}`, r.status, r.ms, true);

    let id = null;
    if (create) {
      r = await timed("POST", base, create);
      const ok = r.status >= 200 && r.status < 300 && r.json?.isSuccess !== false;
      expect(ok, `create ${r.status}: ${r.text?.slice(0, 220)}`);
      id = r.json?.data?.id ?? r.json?.id;
      record(domain, `${name} create`, "POST", base, r.status, r.ms, true);
    }
    const t = id ? `${base}/${id}` : null;

    if (t) {
      r = await timed("GET", t);
      expect(r.status === 200, `get status ${r.status}`);
      record(domain, `${name} get`, "GET", t, r.status, r.ms, true);
    }
    if (t && update) {
      r = await timed("PUT", t, typeof update === "function" ? update(id) : update);
      const ok = r.status >= 200 && r.status < 300 && r.json?.isSuccess !== false;
      expect(ok, `update ${r.status}: ${r.text?.slice(0, 220)}`);
      record(domain, `${name} update`, "PUT", t, r.status, r.ms, true);
    }
    if (t && create && !keep) {
      r = await timed("DELETE", t);
      const ok = (r.status >= 200 && r.status < 300) || r.status === 404;
      expect(ok, `delete status ${r.status}`);
      record(domain, `${name} delete`, "DELETE", t, r.status, r.ms, true, "cleanup");
    }
    return id;
  } catch (e) {
    record(domain, name, "—", base, 0, 0, false, e.message);
    return null;
  }
}

function percentile(sorted, p) {
  if (!sorted.length) return 0;
  const idx = Math.min(sorted.length - 1, Math.ceil((p / 100) * sorted.length) - 1);
  return sorted[Math.max(0, idx)];
}

async function latency(name, method, path, body, iterations = 20) {
  const times = [];
  let lastStatus = 0;
  const created = [];
  for (let i = 0; i < iterations; i++) {
    const r = await timed(method, path, typeof body === "function" ? body() : body);
    if (r.json?.data?.id) created.push(r.json.data.id);
    lastStatus = r.status;
    times.push(r.ms);
    if (r.status >= 500) break;
  }
  times.sort((a, b) => a - b);
  const s = {
    name, method, path, status: lastStatus, n: times.length,
    min: Math.round(times[0] ?? 0),
    p50: Math.round(percentile(times, 50)),
    p95: Math.round(percentile(times, 95)),
    max: Math.round(times[times.length - 1] ?? 0),
  };
  for (const id of created) await timed("DELETE", `/inventory/locations/${id}`).catch(() => {});
  console.log(`[LAT] ${name} ${method} ${path} → ${s.status} n=${s.n} p50=${s.p50}ms p95=${s.p95}ms max=${s.max}ms`);
  return s;
}

async function main() {
  await login();

  console.log("=== CRUD ===");
  // ── location compartilhada (não deletada até o fim) ──
  const locId = await crud("inventory", "locations", "/inventory/locations", {
    create: { name: "SMOKE-LOC", description: "criado pelo smoke-crud-latency" },
    update: { name: "SMOKE-LOC-UPD", description: "atualizado" },
    keep: true,
  });

  await crud("inventory", "racks", "/inventory/racks", {
    create: { locationId: locId, name: "SMOKE-RACK", description: "x" },
    update: { locationId: locId, name: "SMOKE-RACK-UPD", description: "y" },
  });
  await crud("inventory", "devices", "/inventory/devices", {
    create: { locationId: locId, name: "SMOKE-DEV", type: "Server", ip: "192.0.2.55", operatingSystem: "Linux" },
    update: { locationId: locId, name: "SMOKE-DEV-UPD", type: "Server", ip: "192.0.2.55", operatingSystem: "Linux" },
  });

  // ip-addresses precisa de InterfaceId existente
  {
    const ifaces = await timed("GET", "/inventory/network-interfaces?take=1");
    const ifaceId = ifaces.json?.data?.items?.[0]?.id;
    if (ifaceId) {
      await crud("inventory", "ip-addresses", "/inventory/ip-addresses", {
        create: { interfaceId: ifaceId, ip: "198.51.100.99" },
        update: { interfaceId: ifaceId, ip: "198.51.100.98" },
      });
    } else {
      record("inventory", "ip-addresses (sem interface)", "GET", "/inventory/network-interfaces", ifaces.status, ifaces.ms, true, "skip: 0 interfaces");
    }
  }

  await crud("tasks", "tasks", "/tasks", {
    create: { title: "SMOKE-TASK", kind: "Manual", description: "teste crud", priority: "Medium" },
    update: { title: "SMOKE-TASK-UPD", kind: "Manual", description: "atualizado", priority: "Medium" },
  });
  {
    const users = await timed("GET", "/admin/users?take=1");
    const arr = Array.isArray(users.json?.data) ? users.json.data : (users.json?.data?.items ?? []);
    const leaderId = arr[0]?.userId ?? arr[0]?.id;
    if (leaderId) {
      await crud("projects", "projects", "/projects", {
        create: { code: `SP-${Date.now() % 100000000}`, title: "SMOKE-PROJ", description: "teste", priority: "Medium", leaderId },
        update: (id) => ({ code: `SP-${Date.now() % 100000000}`, title: "SMOKE-PROJ-UPD", description: "upd", priority: "Medium", leaderId }),
      });
    } else {
      record("projects", "projects (sem leader)", "GET", "/admin/users", users.status, users.ms, false, "leaderId não encontrado");
    }
  }
  await crud("knowledge", "categories", "/knowledge/categories", {
    create: { name: "SMOKE-CAT" },
    update: { name: "SMOKE-CAT-UPD" },
  });
  await crud("knowledge", "articles", "/knowledge/articles", {
    create: { title: "SMOKE-ARTICLE", content: "# smoke" },
    update: { title: "SMOKE-ARTICLE-UPD", content: "# smoke upd", reason: "smoke test update" },
  });
  await crud("sla", "services", "/sla/services", {
    create: { name: "SMOKE-SVC", description: "x" },
    update: { name: "SMOKE-SVC-UPD", description: "y" },
  });

  // routing (F39-T003) — GET/PUT real
  {
    let r = await timed("GET", "/notifications/routing");
    record("notifications", "routing get", "GET", "/notifications/routing", r.status, r.ms, r.status === 200, r.status === 200 ? "" : r.text?.slice(0, 150));
    r = await timed("PUT", "/notifications/routing", { enabled: true, defaults: ["Email", "SignalR"], rules: [] });
    record("notifications", "routing save", "PUT", "/notifications/routing", r.status, r.ms, r.status === 200, r.status === 200 ? "" : r.text?.slice(0, 150));
    r = await timed("GET", "/notifications/routing");
    const vers = r.json?.data?.version;
    record("notifications", "routing reload", "GET", "/notifications/routing", r.status, r.ms, r.status === 200, `version=${vers}`);
  }

  console.log("\n=== READS ===");
  for (const [name, path] of [
    ["users list", "/admin/users"],
    ["audit list", "/admin/audit"],
    ["ai-providers list", "/admin/ai-providers"],
    ["invitations list", "/admin/invitations"],
    ["sessions list", "/auth/sessions"],
    ["notification rules", "/notifications/rules"],
    ["notification feed", "/notifications"],
    ["reports indicators", "/reports/indicators"],
    ["dashboard admin", "/reports/dashboard/administrativo"],
    ["incidents list", "/incidents?take=3"],
    ["alerts list", "/monitoring/alerts"],
    ["search term", "/inventory/search?term=smoke"],
    ["switches list", "/network/switches"],
    ["topology", "/network/topology"],
    ["sla policies", "/sla/policies"],
    ["sla slos", "/sla/slos"],
    ["mfa status", "/auth/mfa/status"],
    ["audit export check", "/admin/audit?take=1"],
    ["integrations zabbix cfg", "/integrations/zabbix/config"],
    ["integrations unifi health", "/integrations/unifi/health"],
    ["pdi", "/pdi"],
  ]) {
    const r = await timed("GET", path);
    const ok = r.status === 200;
    record("read", name, "GET", path, r.status, r.ms, ok, ok ? "" : r.text?.slice(0, 130));
  }

  // health na raiz (não sob /api/v1)
  {
    const t0 = performance.now();
    const res = await fetch(`${ROOT}/health`);
    const ms = performance.now() - t0;
    const j = await res.json().catch(() => null);
    const bad = (j?.checks ?? []).filter(c => c.status !== "Healthy").map(c => `${c.name}:${c.status}`);
    record("infra", "health", "GET", "/health", res.status, ms, res.status === 200, bad.length ? `503 esperado p/ stack local — não-healthy: ${bad.join(", ")}` : "all healthy");
  }

  console.log("\n=== LATÊNCIA (20 iterações, com gate) ===");
  const lat = [];
  lat.push(await latency("locations list", "GET", "/inventory/locations?take=20"));
  lat.push(await latency("devices list", "GET", "/inventory/devices?take=20"));
  lat.push(await latency("tasks list", "GET", "/tasks?take=20"));
  lat.push(await latency("projects list", "GET", "/projects?take=20"));
  lat.push(await latency("incidents list", "GET", "/incidents?take=20"));
  lat.push(await latency("knowledge cats", "GET", "/knowledge/categories"));
  lat.push(await latency("notifications rules", "GET", "/notifications/rules"));
  lat.push(await latency("routing get", "GET", "/notifications/routing"));
  lat.push(await latency("admin audit", "GET", "/admin/audit?take=20"));
  lat.push(await latency("admin users", "GET", "/admin/users"));
  lat.push(await latency("auth me", "GET", "/auth/me"));
  lat.push(await latency("reports ind", "GET", "/reports/indicators"));
  lat.push(await latency("location create", "POST", "/inventory/locations", () => ({ name: `LAT-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`, description: "lat" })));
  lat.push(await latency("search", "GET", "/inventory/search?term=a"));

  // limpeza da location
  if (locId) await timed("DELETE", `/inventory/locations/${locId}`);

  const pass = results.filter(r => r.ok).length;
  const fail = results.filter(r => !r.ok).length;
  const summary = { api: API, at: new Date().toISOString(), rateLimitHits: budget.limited, pass, fail, results, latency: lat };
  writeFileSync("/tmp/opencode/crud-latency-results.json", JSON.stringify(summary, null, 2));
  console.log(`\n=== RESUMO: ${pass} PASS, ${fail} FAIL ===`);
  for (const f of results.filter(r => !r.ok)) console.log(` - ${f.domain} ${f.name}: ${f.detail}`);
  process.exit(fail ? 1 : 0);
}

main().catch(e => { console.error("ERRO FATAL:", e); process.exit(2); });
