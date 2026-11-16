#!/bin/bash
B=https://localhost:8443
J=temp/cookies.txt; rm -f $J
H='-sk -H Content-Type:application/json'
# login
R=$(curl $H -c $J -d '{"email":"admin@example.com","password":"Infra#Dev2026!Smoke"}' $B/api/v1/auth/login)
echo "login: $(echo $R | head -c 120)"
TOK=$(echo $R | grep -o '"accessToken":"[^"]*"' | cut -d'"' -f4)
[ -z "$TOK" ] && TOK=$(echo $R | grep -o '"token":"[^"]*"' | cut -d'"' -f4)
echo "token: ${TOK:0:20}..."
A="Authorization: Bearer $TOK"
# xsrf
curl $H -b $J -c $J -H "$A" $B/api/v1/auth/me -o /dev/null
XSRF=$(grep XSRF-TOKEN $J | awk '{print $NF}')
echo "xsrf: ${XSRF:0:10}..."
for ep in \
 "auth/me" \
 "inventory/devices" \
 "inventory/locations" \
 "inventory/vlans" \
 "network/switches" \
 "network/switch-ports" \
 "monitoring/alerts" \
 "monitoring/checks" \
 "integrations/zabbix/config" \
 "integrations/zabbix/summary" \
 "integrations/zabbix/hosts" \
 "integrations/zabbix/problems" \
 "integrations/unifi/dashboard" \
 "integrations/unifi/devices" \
 "status/containers" \
 "status/health" \
 "reports" \
 "tasks" \
 "incidents" \
 "projects" \
 ; do
 code=$(curl $H -b $J -H "$A" -o temp/resp.json -w "%{http_code}" "$B/api/v1/$ep")
 size=$(wc -c < temp/resp.json)
 echo "$code  $ep  (${size}B)"
done
