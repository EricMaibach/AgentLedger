# 0006. PostgreSQL for storage

- **Status:** Accepted
- **Date:** 2026-09-23

## Context

The ledger stores semi-structured JSON payloads that must be queryable, plus relational projections built from them.

## Decision

- **PostgreSQL** (version 18), accessed through EF Core with the Npgsql provider.
- Raw payloads are stored as `jsonb`, with GIN indexes where queries need them.
- Development uses the dev container's Postgres service. Tests use Testcontainers with the same major version. The EF in-memory provider is not used.

## Consequences

- JSON querying, relational projections and transactions are all in one database.
- Postgres-specific features (`jsonb`, GIN) are confined to Infrastructure. Core stays storage-agnostic.
