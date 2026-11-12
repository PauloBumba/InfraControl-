#!/usr/bin/env bash
# Sweep de rotas GET da API InfraControl — nao autenticado.
# Esperado: 200/204/301/302/401/403/405 = rota existe; 404/000/500 = problema.
BASE="https://localhost:8443"
CTRL_DIR="src/InfraControl.Api/Controllers"
GUID="00000000-0000-0000-0000-000000000000"

> /tmp/routes.txt
for f in $(find "$CTRL_DIR" -name "*.cs" | sort); do
  base=$(grep -oP '^\s*\[Route\("\K[^"]+' "$f" | head -1)
  [ -z "$base" ] && continue
  grep -oP '^\s*\[Http(Get|Head)\("\K[^"]+' "$f" | while read -r tmpl; do
    url="/$base/$tmpl"
    url=$(echo "$url" | sed -E 's/\{[^}]*\}/'"$GUID"'/g; s|//+|/|g; s|/$||')
    echo "GET $url" >> /tmp/routes.txt
  done
  # HttpGet sem template -> rota base (somente se nao houver HttpGet("...") cobrindo)
  if grep -qP '^\s*\[Http(Get|Head)\]' "$f"; then
    echo "GET /$base" >> /tmp/routes.txt
  fi
done
sort -u /tmp/routes.txt -o /tmp/routes.txt

total=$(wc -l < /tmp/routes.txt)
echo "== $total rotas GET encontradas =="
> /tmp/sweep_results.txt
while read -r method url; do
  code=$(curl -sk -o /dev/null -w "%{http_code}" --max-time 15 "$BASE$url")
  echo "$code $url" >> /tmp/sweep_results.txt
done < /tmp/routes.txt

echo "== RESULTADO =="
awk '{print $1}' /tmp/sweep_results.txt | sort | uniq -c
echo "-- codigos inesperados (fora 200/204/301/302/401/403/405) --"
grep -vE '^(200|204|301|302|401|403|405) ' /tmp/sweep_results.txt || true
echo "-- detalhe completo --"
cat /tmp/sweep_results.txt
