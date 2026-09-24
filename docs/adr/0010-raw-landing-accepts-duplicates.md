# 0010. Raw landing accepts duplicates; deduplicate downstream

- **Status:** Accepted
- **Date:** 2026-09-24

## Context

The CLI may send the same event more than once, for example when its spool resends after a timeout whose request actually succeeded. Each event carries a client-generated UUIDv7 (`AgentEventId`) that stays the same across resends.

An earlier design used that ID as the primary key of the raw table. Ingestion would then have to detect duplicates at write time, including the race between two concurrent resends. That puts transformation logic at the front of the pipeline, which [ADR 0001](0001-raw-event-ledger-with-projections.md) is meant to avoid.

## Decision

Follow ELT: land everything exactly as received, and transform downstream.

- The raw table **`agent_event_receipts`** stores one row per **receipt**, i.e. every message the API accepts. Duplicates are stored, not rejected.
- Each receipt has two identities:
  - **`ReceiptId`**: server-generated UUIDv7, the primary key, unique per received message;
  - **`EventId`** (`AgentEventId`): the agent event's business key from the client. It is indexed but **not unique**, and is the same for every resend.
- Ingestion always appends. It never checks for duplicates and never fails because of one.
- **Deduplication happens in projections,** by `EventId`, using idempotent upserts. The future deduplicated table, **`agent_events`**, has one row per agent event, keyed by `AgentEventId`.

## Consequences

- Ingestion is a plain append, with no race conditions or special error handling.
- Resends stay visible as data, which is useful for monitoring CLI reliability.
- Anyone querying `agent_event_receipts` directly must deduplicate (e.g. `DISTINCT ON (event_id)`) or will over-count. A deduplicated view can be added if ad-hoc querying needs it.
- Duplicates may arrive in different watermark windows, so projections must upsert by `EventId`, not just insert.
