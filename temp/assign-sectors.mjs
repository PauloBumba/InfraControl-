// Atribui location (gera SQL — API não permite mudar location)
// e vlanId (PUT /devices/{id}) conforme setor/vlan real do hospital.
import { execSync } from "child_process";
import fs from "fs";

const BASE = "https://localhost:8443";
process.env.NODE_TLS_REJECT_UNAUTHORIZED = "0";

const jar = new Map();
function storeCookies(res) {
  const set = res.headers.getSetCookie?.() ?? [];
  for (const c of set) {
    const [pair] = c.split(";");
    const idx = pair.indexOf("=");
    jar.set(pair.slice(0, idx).trim(), pair.slice(idx + 1).trim());
  }
}
const cookieHeader = () => [...jar.entries()].map(([k, v]) => `${k}=${v}`).join("; ");

const loginRes = await fetch(`${BASE}/api/v1/auth/login`, {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ email: "admin@example.com", password: "Infra#Dev2026!Smoke" }),
});
storeCookies(loginRes);
const login = await loginRes.json();
if (!login.isSuccess) { console.error("login falhou", login); process.exit(1); }

await fetch(`${BASE}/api/v1/auth/me`, { headers: { Cookie: cookieHeader() } }).then(storeCookies);
const xsrf = jar.get("XSRF-TOKEN");

async function api(method, path, body) {
  const res = await fetch(`${BASE}${path}`, {
    method,
    headers: {
      "Content-Type": "application/json",
      Cookie: cookieHeader(),
      "X-XSRF-TOKEN": xsrf ?? "",
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  storeCookies(res);
  const text = await res.text();
  if (res.status === 204) return { isSuccess: true };
  try { return JSON.parse(text); } catch { return { httpStatus: res.status, raw: text.slice(0, 200) }; }
}

// ---------- locations ----------
const LOC = {};
for (const line of fs.readFileSync("temp/locmap.txt", "utf8").trim().split("\n")) {
  const [name, id] = line.split("|");
  LOC[name] = id;
}

// ---------- vlans ----------
const VLAN = {};
const vlanRows = execSync(
  `docker exec infracontrol-postgres psql -U infracontrol -d infracontrol -t -A -c "SELECT name||'|'||id FROM vlans"`,
  { encoding: "utf8" }).trim().split("\n").filter(Boolean);
for (const line of vlanRows) { const [n, id] = line.split("|"); VLAN[n] = id; }

// ---------- mapeamento nome -> setor ----------
function sectorFor(name, type) {
  const n = name.toUpperCase();
  const hssm = n.startsWith("HSSM_");
  if (type === "Nvr") return "CPD";
  if (hssm) {
    if (n.includes("CENTRO_CIRURGICO")) return "Centro Cirurgico";
    if (n.includes("RECEP")) return "Recepção Central";
    if (n.includes("ADMINISTRATIVO")) return "Secretaria";
    return "CPD";
  }
  if (n.includes("SEM_SETOR")) return null; // setor não identificado no nome
  if (n.includes("SETOR_1") || n.includes("SETOR1") || n.includes("SETOR 1")) return "Setor 1";
  if (n.includes("SETOR_2") || n.includes("SETOR2") || n.includes("SETOR 2")) return "Setor 2";
  if (n.includes("SETOR_3") || n.includes("SETOR 3")) return "Setor 3";
  if (n.includes("SETOR_4") || n.includes("SETOR4") || n.includes("SETOR 4")) return "Setor 4";
  if (n.includes("UTI_1") || n.includes("UTI1") || n.includes("UTI 1")) return "UTI 1";
  if (n.includes("UTI_2") || n.includes("UTI2") || n.includes("UTI 2")) return "UTI 2";
  if (n.includes("AUDITORIA")) return "Auditoria";
  if (n.includes("OPME")) return "OPME";
  if (n.includes("NIR")) return "NIR CE";
  if (n.includes("SANIFIC")) return "Sanificacao";
  if (/_PS|PRONTO[-_ ]SOCORRO|CORREDOR_PA/.test(n)) return "Pronto Socorro";
  if (n.includes("FARMACIA") || n.includes("FARM_")) return "Farmácia";
  if (n.includes("_CC") || n.includes("CIRURG")) return "Centro Cirurgico";
  if (/_CE($|_)|ESPECIALIDADES/.test(n)) return "Centro de Especialidades";
  if (n.includes("CONTABIL")) return "Contabilidade";
  if (n.includes("FATURAMENTO") || n.includes("TESOURARIA")) return "Financeiro";
  if (/_DP($|_)/.test(n)) return "Departamento Pessoal";
  if (n.includes("ENDOSCOP") || n.includes("CME")) return "CME";
  if (n.includes("TOMOGRAF")) return "Centro de Especialidades";
  if (n.includes("RECEP") || n.includes("LAUDOS")) return "Recepção Central";
  if (n.includes("SUPRIMENT")) return "Compras";
  if (n.includes("MANUTEN")) return "Manutenção";
  if (n.includes("ALMOX")) return "Almoxarifado";
  if (n.includes("_ADM")) return "Secretaria";
  return null;
}

function vlanFor(type, name) {
  if (type === "Printer") return name.startsWith("HSSM_") ? "HSSM" : "Impressora";
  if (type === "Camera" || type === "Nvr") return "Camera";
  if (type === "Phone") return "Telefone";
  if (type === "Switch") return "Switches";
  if (type === "AccessPoint") return "AP";
  return "Default";
}

// ---------- busca devices ----------
const devices = [];
for (const type of ["Printer", "Camera", "Nvr", "Phone", "Switch", "AccessPoint", "Workstation"]) {
  const r = await api("GET", `/api/v1/inventory/devices?type=${type}&pageSize=500`);
  const items = r.data?.items ?? [];
  for (const d of items) devices.push(d);
}
console.log(`devices carregados: ${devices.length}`);

// ---------- SQL de location ----------
const updates = [];
for (const d of devices) {
  // Phone/Workstation: localização por andar foi decisão do F27-T014 — não mexe.
  if (d.type === "Phone" || d.type === "Workstation") continue;
  const sector = sectorFor(d.name, d.type);
  if (!sector || !LOC[sector]) continue;
  if (d.locationId === LOC[sector]) continue;
  updates.push(`UPDATE devices SET "LocationId"='${LOC[sector]}', "UpdateDateTime"=now() WHERE id='${d.id}'; -- ${d.name} -> ${sector}`);
}
fs.writeFileSync("temp/location-updates.sql", updates.join("\n") + "\n");
console.log(`updates de location gerados: ${updates.length}`);

// ---------- PUT vlanId ----------
let okV = 0, failV = 0, sameV = 0;
for (const d of devices) {
  const vname = vlanFor(d.type, d.name);
  const vid = VLAN[vname];
  if (!vid) { console.log(`vlan não achada: ${vname}`); failV++; continue; }
  if (d.vlanId === vid) { sameV++; continue; }
  const body = {
    name: d.name, type: d.type, rackId: d.rackId ?? null, ip: d.ip ?? null,
    mac: d.mac ?? null, serial: d.serial ?? null, model: d.model ?? null,
    vendor: d.vendor ?? null, vlanId: vid, description: d.description ?? null,
    extension: d.extension ?? null, sipServer: d.sipServer ?? null,
    operatingSystem: d.operatingSystem ?? null, imagePath: d.imagePath ?? null,
  };
  const r = await api("PUT", `/api/v1/inventory/devices/${d.id}`, body);
  if (r.isSuccess === true) { okV++; }
  else { failV++; console.log(`FAIL vlan ${d.name}: ${JSON.stringify(r).slice(0, 160)}`); }
}
console.log(`vlanId: atualizados=${okV} já-ok=${sameV} falhas=${failV}`);
