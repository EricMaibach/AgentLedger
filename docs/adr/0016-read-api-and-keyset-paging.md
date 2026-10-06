# 0016. Read API: keyset paging, query service for lists, specification for lookups

- **Status:** Accepted
- **Date:** 2026-10-01

## Context

The web UI ([ADR 0015](0015-web-ui-and-evidence.md)), and later the CLI and the MCP server, read the ledger through use cases and a JSON API. The raw receipts table only grows, often in bursts with several receipts in the same millisecond, and receives new rows while someone is browsing.

## Decision

**Endpoints:**
```
GET /raw/events?agent=&eventType=&session=&host=&repo=&from=&to=&distinct=true&limit=50&after=<cursor>
    → { "items": [ … ], "nextCursor": "…" | null }
GET /raw/events/counts?session=…   → [ { "eventType": "…", "count": n }, … ]
GET /raw/events/{eventId}          → envelope, receipts[], exact payload
```
- `agent` uses the kebab-case wire names from ingest ([ADR 0011](0011-ingest-envelope-contract.md)).
- `distinct=true` (the default) returns one row per event (its first receipt) with a `receiptCount`. `distinct=false` returns every receipt.
- List items carry a short raw `preview`; the full payload is only on the detail endpoint.

**Keyset paging, not page numbers:**
- Order by `(received_at DESC, id DESC)`; the next page is the rows strictly older than the last one seen. The receipt ID breaks ties between receipts with equal timestamps.
- `nextCursor` is an opaque encoding of the last row's `(received_at, id)`. Clients pass it back unchanged and never construct it.
- An index on `(received_at, id)` supports it.
- Rationale: offset paging scans every skipped row (slower with depth) and shifts when new rows arrive (rows repeat or go missing); keyset paging costs the same at any depth and stays stable.

**Which pattern reads the data:**
- **Lookups of aggregates go through repositories and Specifications.** Event detail uses a `ReceiptsByEventIdSpec` on `IReadRepository<AgentEventReceipt>`.
- **Read-model lists go through a query service:** an interface in UseCases (`IRawEventQueryService`), implemented in Infrastructure with a hand-shaped EF query that returns DTOs directly, without loading entities. The list and the event-type counts are read models (flattened, deduplicated, aggregated).

## Consequences

- Paging performance doesn't degrade as the ledger grows, and browsing is stable while events arrive.
- Clients can't jump to an arbitrary page number; the UI offers newer/older navigation and filters instead.
- The "first receipt per event" rule is applied at query time, so raw data stays untouched; the Phase 4 projection will make it cheaper.
- Two reading patterns coexist, chosen by what's being read (aggregates or read models).
