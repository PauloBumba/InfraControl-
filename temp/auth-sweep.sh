#!/usr/bin/env bash
# Sweep autenticado com latencia — gera /tmp/auth_sweep.tsv (code<TAB>ms<TAB>url)
BASE="https://localhost:8443"
JAR=/tmp/ic_cookies.txt
rm -f "$JAR"

# login admin
code=$(curl -sk -o /tmp/login.json -w "%{http_code}" -c "$JAR" -X POST "$BASE/api/v1/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@example.com","password":"Infra#Dev2026!Smoke"}')
echo "login: $code $(head -c 200 /tmp/login.json)"
[ "$code" != "200" ] && exit 1

# /auth/me emite XSRF-TOKEN
curl -sk -b "$JAR" -c "$JAR" -o /dev/null "$BASE/api/v1/auth/me"
XSRF=$(grep XSRF-TOKEN "$JAR" | awk '{print $NF}' | tail -1)
echo "xsrf: ${XSRF:0:12}..."

> /tmp/auth_sweep.tsv
while read -r method url; do
  read -r http ms < <(curl -sk -b "$JAR" -o /dev/null -w "%{http_code} %{time_total}" --max-time 60 "$BASE$url")
  ms=$(awk "BEGIN{printf \"%d\", $ms*1000}")
  echo -e "$http\t$ms\t$url" >> /tmp/auth_sweep.tsv
done < /tmp/routes.txt

echo "== distribuicao =="
awk -F'\t' '{print $1}' /tmp/auth_sweep.tsv | sort | uniq -c
echo "== mais lentos (top 20) =="
sort -t$'\t' -k2 -rn /tmp/auth_sweep.tsv | head -20
echo "== nao-200 =="
awk -F'\t' '$1!=200' /tmp/auth_sweep.tsv
