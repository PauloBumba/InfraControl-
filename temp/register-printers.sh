#!/usr/bin/env bash
set -u
BASE="https://localhost:8443"
CJ="temp/cookies.txt"
LOGIN=$(curl -sk -c "$CJ" -X POST "$BASE/api/v1/auth/login" -H "Content-Type: application/json" \
  -d '{"email":"admin@example.com","password":"Infra#Dev2026!Smoke"}')
echo "$LOGIN" | grep -q '"isSuccess":true' || { echo "LOGIN FALHOU: $LOGIN"; exit 1; }
curl -sk -b "$CJ" -c "$CJ" "$BASE/api/v1/auth/me" > /dev/null
XSRF=$(awk '$6=="XSRF-TOKEN"{print $NF}' "$CJ" | tail -1)
echo "login OK (xsrf ${#XSRF})"

CPD=f85d95a4-5d91-4257-99bd-1cb02fd3e853
SETOR1=4b20ffdf-a89d-4691-af0c-84d5defbee4e
AUD=7797d76c-46b7-4693-8b29-c573e393f2f2
OPME=9548ace7-b13f-45d5-beb7-bd51e2c44cfc
NIR=139fef08-9e38-4a0f-91ea-006423e72dcc
SANIF=d7372619-c93d-4dc1-89d3-cae6cf4916c0
PS=8a327262-5557-4dc6-9fb9-cf8c42cca433
FARM=8cf72c7a-e6dd-4473-a3d3-14ebce5900e9
CC=7d3e9ec7-92f2-4139-b7ce-7380ea3f2b25

# name|ip|vendor|locationId
cat > temp/printers.tsv <<P
HSDS_CANON_CC|192.168.22.166|Canon|$CC
HSDS_CANON_CE|192.168.22.18|Canon|$CPD
HSDS_CANON_CONTABILIDADE|192.168.22.203|Canon|$CPD
HSDS_CANON_DP|192.168.22.198|Canon|$CPD
HSDS_CANON_ENDOSCOPIA|192.168.22.165|Canon|$CPD
HSDS_CANON_FARMACIA_CENTRAL|192.168.22.197|Canon|$FARM
HSDS_CANON_FATURAMENTO|192.168.22.163|Canon|$CPD
HSDS_CANON_PRONTO_SOCORRO|192.168.22.182|Canon|$PS
HSDS_CANON_RECEPCAO_IMG|192.168.22.181|Canon|$CPD
HSDS_CANON_RECEP_INTER|192.168.22.200|Canon|$CPD
HSDS_CANON_SEM_SETOR_22_210|192.168.22.210|Canon|$CPD
HSDS_CANON_TOMOGRAFIA_A3|192.168.22.164|Canon|$CPD
HSDS_EPSON_SEM_SETOR_22_65|192.168.22.65|Epson|$CPD
HSDS_HP_AUDITORIA|192.168.22.100|HP|$AUD
HSDS_HP_AUDITORIA_PS|192.168.22.105|HP|$AUD
HSDS_HP_MFP_SEM_SETOR_22_70|192.168.22.70|HP|$CPD
HSDS_HP_NIR|192.168.22.234|HP|$NIR
HSDS_HP_OPME|192.168.22.202|HP|$OPME
HSDS_HP_PS_CONSULTORIO_1|192.168.22.110|HP|$PS
HSDS_HP_PS_POSTO_ENFERMAGEM|192.168.22.225|HP|$PS
HSDS_HP_PS_TRIAGEM|192.168.22.230|HP|$PS
HSDS_HP_SANIFICACAO|192.168.22.69|HP|$SANIF
HSDS_HP_SEM_SETOR_22_75|192.168.22.75|HP|$CPD
HSDS_HP_SETOR_1_ENF|192.168.22.175|HP|$SETOR1
HSDS_HP_SETOR_1_MED|192.168.22.76|HP|$SETOR1
HSDS_HP_SETOR_2|192.168.22.190|HP|$CPD
HSDS_HP_SETOR_3|192.168.10.234|HP|$CPD
HSDS_HP_SETOR_4|192.168.22.183|HP|$CPD
HSDS_HP_TESOURARIA|192.168.22.193|HP|$CPD
HSDS_HP_UTI_1|192.168.22.16|HP|$CPD
HSDS_HP_UTI_2|192.168.22.184|HP|$CPD
HSDS_NUTRICAO|192.168.22.10|HP|$CPD
HSDS_SUPRIMENTOS|192.168.22.20|HP|$CPD
HSSM_CANON_ADMINISTRATIVO|192.168.50.124|Canon|$CPD
HSSM_CANON_CENTRO_CIRURGICO|192.168.50.104|Canon|$CC
HSSM_CANON_RECEPCAO|192.168.50.103|Canon|$CPD
P

ok=0; skip=0; fail=0
first=1
while IFS='|' read -r name ip vendor loc; do
  [ -z "$name" ] && continue
  [ $first -eq 1 ] && { first=0; continue; }
  body=$(printf '{"locationId":"%s","name":"%s","type":"Printer","ip":"%s","vendor":"%s","description":"Zabbix host %s - template IC Impressora SNMP"}' "$loc" "$name" "$ip" "$vendor" "$name")
  resp=$(curl -sk -b "$CJ" -X POST "$BASE/api/v1/inventory/devices" -H "Content-Type: application/json" -H "X-XSRF-TOKEN: $XSRF" -d "$body")
  if echo "$resp" | grep -q '"isSuccess":true'; then
    ok=$((ok+1)); echo "OK   $name"
  elif echo "$resp" | grep -qi "já existe\|already\|duplicate\|conflict"; then
    skip=$((skip+1)); echo "SKIP $name :: $(echo "$resp" | head -c 160)"
  else
    fail=$((fail+1)); echo "FAIL $name :: $(echo "$resp" | head -c 200)"
  fi
done < temp/printers.tsv
echo "== criados=$ok pulados=$skip falhas=$fail =="
