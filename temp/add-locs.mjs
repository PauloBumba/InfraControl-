process.env.NODE_TLS_REJECT_UNAUTHORIZED = "0";
const BASE = "https://localhost:8443";
const jar = new Map();
const store = (res) => { for (const c of res.headers.getSetCookie?.() ?? []) { const [p] = c.split(";"); const i = p.indexOf("="); jar.set(p.slice(0,i).trim(), p.slice(i+1).trim()); } };
const cookie = () => [...jar.entries()].map(([k,v])=>`${k}=${v}`).join("; ");
const login = await fetch(`${BASE}/api/v1/auth/login`,{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({email:"admin@example.com",password:"Infra#Dev2026!Smoke"})});
store(login); await login.json();
await fetch(`${BASE}/api/v1/auth/me`,{headers:{Cookie:cookie()}}).then(store);
const xsrf = jar.get("XSRF-TOKEN");
for (const name of ["Nutrição","Laudos","Raio-X","Laboratório","Tomografia","Ultrassom"]) {
  const r = await fetch(`${BASE}/api/v1/inventory/locations`,{method:"POST",headers:{"Content-Type":"application/json",Cookie:cookie(),"X-XSRF-TOKEN":xsrf},body:JSON.stringify({name})});
  store(r); const j = await r.json().catch(()=>null);
  console.log(name, "->", j?.isSuccess ? "OK "+j.data.id : JSON.stringify(j).slice(0,140));
}
