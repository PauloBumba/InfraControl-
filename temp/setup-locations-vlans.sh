#!/usr/bin/env bash
set -u
BASE="https://localhost:8443"
CJ="temp/cookies.txt"
curl -sk -c "$CJ" -X POST "$BASE/api/v1/auth/login" -H "Content-Type: application/json" \
  -d '{"email":"admin@example.com","password":"Infra#Dev2026!Smoke"}' | grep -q '"isSuccess":true' || { echo "LOGIN FALHOU"; exit 1; }
curl -sk -b "$CJ" -c "$CJ" "$BASE/api/v1/auth/me" > /dev/null
XSRF=$(awk '$6=="XSRF-TOKEN"{print $NF}' "$CJ" | tail -1)

post() { # $1=path $2=json
  curl -sk -b "$CJ" -X POST "$BASE$1" -H "Content-Type: application/json" -H "X-XSRF-TOKEN: $XSRF" -d "$2"
}

echo "== LOCATIONS =="
for loc in \
  "Setor 2" "Setor 3" "Setor 4" "UTI 1" "UTI 2" \
  "Centro de Especialidades" "Contabilidade" "Comercial" "TI" "Compras" \
  "CME" "Financeiro" "Secretaria" "Farmácia" "Recepção Central" \
  "Manutenção" "Pastoral" "Marketing" "Departamento Pessoal" "Recursos Humanos"; do
  resp=$(post "/api/v1/inventory/locations" "{\"name\":\"$loc\"}")
  if echo "$resp" | grep -q '"isSuccess":true'; then echo "OK   $loc";
  else echo "FAIL $loc :: $(echo "$resp" | head -c 140)"; fi
done

echo "== VLANS =="
for v in \
  "Default|1|VLAN default/nativa da rede" \
  "Visitante|13|VLAN de visitantes" \
  "Telefone|21|Telefonia IP" \
  "Impressora|22|Impressoras de rede" \
  "Camera|26|Câmeras IP / videomonitoramento" \
  "AP|125|Access points UniFi" \
  "Switches|127|Gerenciamento dos switches" \
  "HSSM|50|Unidade HSSM"; do
  IFS='|' read -r name num desc <<< "$v"
  resp=$(post "/api/v1/network/vlans" "{\"name\":\"$name\",\"vlanNumber\":$num,\"description\":\"$desc\"}")
  if echo "$resp" | grep -q '"isSuccess":true'; then echo "OK   vlan $num $name";
  else echo "FAIL vlan $num $name :: $(echo "$resp" | head -c 140)"; fi
done
