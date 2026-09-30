#!/bin/sh
# ADR-022: gera o .htpasswd do Seq a partir das envs do compose. Segredo fica
# no .env, fora do git; padrão de dev documentado no .env.example.
set -e

SEQ_AUTH_USER="${SEQ_AUTH_USER:-admin}"
SEQ_AUTH_PASSWORD="${SEQ_AUTH_PASSWORD:-admin}"

htpasswd -bc /etc/nginx/seq.htpasswd "$SEQ_AUTH_USER" "$SEQ_AUTH_PASSWORD" >/dev/null 2>&1

exit 0