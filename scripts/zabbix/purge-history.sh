#!/usr/bin/env bash
# Purge em batches de history_uint e history com mais de 7 dias.
# Uso: ./purge-history.sh [batch_size] [sleep_seg]
set -u
BATCH="${1:-300000}"
SLEEP="${2:-3}"
CUTOFF_SQL="extract(epoch from now()) - 7*86400"
TOTAL=0

for tbl in history_uint history; do
  echo "[$(date +%H:%M:%S)] Iniciando purge de $tbl (batch=$BATCH, sleep=${SLEEP}s)..."
  iter=0
  while true; do
    iter=$((iter+1))
    out=$(docker exec infracontrol-zabbix-db psql -U zabbix -d zabbix -tA -c \
      "WITH d AS (DELETE FROM $tbl WHERE ctid IN (SELECT ctid FROM $tbl WHERE clock < $CUTOFF_SQL LIMIT $BATCH) RETURNING 1) SELECT count(*) FROM d")
    n=${out:-0}
    TOTAL=$((TOTAL+n))
    if [ "$iter" -le 3 ] || [ $((iter % 20)) -eq 0 ]; then
      echo "[$(date +%H:%M:%S)] $tbl iter=$iter deleted=$n total_acumulado=$TOTAL"
    fi
    if [ "$n" -lt "$BATCH" ]; then
      echo "[$(date +%H:%M:%S)] $tbl concluido apos $iter iteracoes (ultimo lote=$n)"
      break
    fi
    sleep "$SLEEP"
  done
done
echo "[$(date +%H:%M:%S)] PURGE COMPLETO. Total deletado: $TOTAL"
