#!/bin/sh
set -eu

: "${REDIS_PASSWORD:?REDIS_PASSWORD is required}"
: "${REDIS_SLAVE_IP:?REDIS_SLAVE_IP is required}"

sentinel_hosts="redis-sentinel-1 redis-sentinel-2 redis-sentinel-3"
master_ip=""
attempt=0

while [ "$attempt" -lt 60 ] && [ -z "$master_ip" ]; do
    for sentinel_host in $sentinel_hosts; do
        master_ip=$(redis-cli --no-auth-warning -a "$REDIS_PASSWORD" --raw \
            -h "$sentinel_host" -p 26379 \
            SENTINEL get-master-addr-by-name mymaster 2>/dev/null | sed -n '1p' || true)
        if [ -n "$master_ip" ]; then
            break
        fi
    done

    if [ -z "$master_ip" ]; then
        attempt=$((attempt + 1))
        sleep 2
    fi
done

if [ -z "$master_ip" ]; then
    echo "Could not discover the Redis primary from Sentinel after 120 seconds." >&2
    exit 1
fi

if [ "$master_ip" = "$REDIS_SLAVE_IP" ]; then
    exec redis-server --appendonly yes --requirepass "$REDIS_PASSWORD"
fi

exec redis-server --replicaof "$master_ip" 6379 \
    --masterauth "$REDIS_PASSWORD" \
    --appendonly yes \
    --requirepass "$REDIS_PASSWORD"
