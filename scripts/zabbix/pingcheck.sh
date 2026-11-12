#!/bin/sh
# Ping sweep rapido - host a host, 2 pacotes, timeout 1s
for ip in "$@"; do
  if ping -c 2 -W 1 "$ip" >/dev/null 2>&1; then
    echo "$ip OK"
  else
    echo "$ip DOWN"
  fi
done
