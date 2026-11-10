#!/usr/bin/env node
// Mede latência pós-criação: GET list "frio" logo após POST (invalidação de cache)
// vs GET list em regime. NODE_TLS_REJECT_UNAUTHORIZED=0 node post-create-latency.mjs
const API = "https://localhost:8445/api/v1";
const jar = new Map();
let xsrf = "";
let used = 0, winStart = Date.now();

function ck() { return [...jar.entries()].map(([k, v]) => `${k}=${v}`).join("; "); }
async function req(method, path, body) {
  if (Date.now() - winStart > 60000) { used = 0; winStart = Date.now(); }
  if (used >= 200) { await new Promise(r => setTimeout(r, 60000 - (Date.now() - winStart) + 300)); used = 0; winStart = Date.now(); }
  used++;
  const headers = { Accept: "application/json", Cookie: ck() };
  if (body !== undefined) headers["Content-Type"] = "application/json";
  if (xsrf) headers["X-XSRF-TOKEN"] = xsrf;
  const t0 = performance.now();
  const res = await fetch(API + path, { method, headers, body: body === undefined ? undefined : JSON.stringify(body), redirect: "manual" });
  for (const c of res.headers.getSetCookie?.() ?? []) { const [p] = c.split(";"); const i = p.indexOf("="); jar.set(p.slice(0, i).trim(), p.slice(i + 1).trim()); }
  const text = await res.text();
  let json = null; try { json = text ? JSON.parse(text) : null; } catch {}
  return { status: res.status, json, ms: performance.now() - t0 };
}
const pct = (a, p) => a.length ? a[Math.min(a.length - 1, Math.ceil(p / 100 * a.length) - 1)] : 0;
const stats = t => { const s = [...t].sort((a, b) => a - b); return { p50: Math.round(pct(s, 50)), p95: Math.round(pct(s, 95)), max: Math.round(s.at(-1) ?? 0), n: s.length }; };

async function main() {
  let r = await req("POST", "/auth/login", { email: "admin@example.com", password: "InfraControl_Dev_2026" });
  if (r.status !== 200) { console.log("login", r.status, r.json); process.exit(1); }
  await req("GET", "/auth/me");
  xsrf = jar.get("XSRF-TOKEN");

  // aquecimento
  for (let i = 0; i < 5; i++) await req("GET", "/inventory/locations?take=20");

  const baseline = [], afterCreate = [], createTimes = [];
  for (let i = 0; i < 15; i++) {
    const b = await req("GET", "/inventory/locations?take=20");
    baseline.push(b.ms);
    const c = await req("POST", "/inventory/locations", { name: `PC-${Date.now()}-${i}`, description: "post-create" });
    createTimes.push(c.ms);
    const a = await req("GET", "/inventory/locations?take=20"); // logo após mutation
    afterCreate.push(a.ms);
    // cleanup
    if (c.json?.data?.id) await req("DELETE", `/inventory/locations/${c.json.data.id}`);
    await new Promise(r => setTimeout(r, 200)); // respeitar janela
  }
  console.log("baseline GET list:   ", stats(baseline));
  console.log("POST create:        ", stats(createTimes));
  console.log("GET list pós-create:", stats(afterCreate));
  const overhead = afterCreate.map((v, i) => v - baseline[i]).filter(v => Number.isFinite(v));
  console.log("overhead (ms):      ", stats(overhead));
}
main().catch(e => { console.error(e); process.exit(2); });
