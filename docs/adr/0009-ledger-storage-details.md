# 0009. Ledger storage: exact payloads, snake_case, audit timestamps

- **Status:** Accepted
- **Date:** 2026-09-23

## Context

While designing the EF Core mapping for `AgentEvent`, three storage questions came up:

1. **Payload type.** Postgres `jsonb` normalizes JSON: it strips whitespace, reorders keys and keeps only the last of any duplicate keys. The domain promises the payload is kept exactly as received ([ADR 0001](0001-raw-event-ledger-with-projections.md)), and future tamper evidence (hash-chaining) needs the exact bytes.
2. **Naming.** Npgsql's EF provider uses C# names as-is, which forces quoted identifiers (`"AgentEvents"."EventType"`) in every hand-written query.
3. **Incremental processing.** Projection builders need a fast, reliable way to find rows added or changed since their last run.

## Decision

1. **The raw payload is stored as `json`,** not `jsonb`. `json` keeps the text exactly, and still supports the `->` / `->>` operators and expression indexes on specific fields. Heavy querying goes to projections. If whole-document indexing is ever needed, a generated `jsonb` column can be added without losing anything.
   - Tags (built by us, not raw input) are stored as `jsonb` with a GIN index.
2. **Tables and columns use snake_case** (`agent_events.event_type`), via `EFCore.NamingConventions`.
3. **Every table has `created_at` and `updated_at`,** set by an EF interceptor. Incremental processes watermark on `updated_at` with bounded windows, a short safety lag and idempotent processing (see "Incremental processing" in `docs/architecture.md`).
   - A separate integer sequence column was considered for the append-only ledger (exact position, no ties). It was rejected in favor of one uniform convention: bounded windows and idempotency make ties and overlap harmless.

## Consequences

- Payloads are stored byte-for-byte, so audit and hash verification are possible.
- There's no GIN index on the whole payload. Ad-hoc queries over it can cast (`payload::jsonb`) and will be slower.
- All tables share one timestamp convention, and every incremental process follows the same algorithm.
- `created_at`/`updated_at` duplicate `ReceivedAt` on the ledger table. That's accepted: one is a domain fact, the other storage bookkeeping.
