# ADR-010 — Hybrid Cache

## Status
Accepted

## Decisão
Redis será o cache distribuído primário, com provider de fallback e PostgreSQL como fonte da verdade.
