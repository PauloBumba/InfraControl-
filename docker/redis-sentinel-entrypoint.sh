#!/bin/sh
set -eu

: "${REDIS_PASSWORD:?REDIS_PASSWORD is required}"
: "${REDIS_MASTER_IP:?REDIS_MASTER_IP is required}"

case "$REDIS_PASSWORD" in
    *[!A-Za-z0-9_-]*)
        echo "REDIS_PASSWORD must contain only letters, digits, '_' or '-' for Sentinel config." >&2
        exit 1
        ;;
esac

if [ ! -f /data/sentinel.conf ]; then
    umask 077
    printf '%s\n' \
        "port 26379" \
        "dir /data" \
        "requirepass $REDIS_PASSWORD" \
        "sentinel monitor mymaster $REDIS_MASTER_IP 6379 2" \
        "sentinel down-after-milliseconds mymaster 5000" \
        "sentinel parallel-syncs mymaster 1" \
        "sentinel failover-timeout mymaster 10000" \
        "sentinel auth-pass mymaster $REDIS_PASSWORD" \
        "sentinel resolve-hostnames yes" \
        "sentinel announce-hostnames yes" \
        > /data/sentinel.conf
elif grep -q '^sentinel monitor mymaster redis-master 6379 2$' /data/sentinel.conf; then
    sed -i \
        "s|^sentinel monitor mymaster redis-master 6379 2$|sentinel monitor mymaster $REDIS_MASTER_IP 6379 2|" \
        /data/sentinel.conf
fi

exec /usr/local/bin/docker-entrypoint.sh redis-server /data/sentinel.conf --sentinel
