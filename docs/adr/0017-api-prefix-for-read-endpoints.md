# 0017. JSON read API under /api; pages own the clean URLs

- **Status:** Accepted
- **Date:** 2026-10-22

## Context

[ADR 0016](0016-read-api-and-keyset-paging.md) put the read API at `/raw/events…`, and [ADR 0015](0015-web-ui-and-evidence.md) / `docs/ui.md` put the web pages at the same paths. One route can't serve both a page and JSON without content negotiation, and the page URLs are the permanent, human-facing ones (bookmarks, shared links, evidence links from projections).

## Decision

- **JSON read endpoints live under `/api`:** `GET /api/raw/events`, `GET /api/raw/events/counts`, `GET /api/raw/events/{eventId}`. Parameters and responses are unchanged from ADR 0016.
- **Pages own the unprefixed URLs:** `/raw/events`, `/raw/events/{eventId}`.
- **Ingest stays at `POST /events`.** The released CLI depends on that contract ([ADR 0011](0011-ingest-envelope-contract.md)), and moving it would break installed hooks.
- New JSON endpoints go under `/api`.

## Consequences

- No route clashes between pages and JSON, and the page URLs stay short and stable.
- `/api` is a natural boundary for authentication later: browser sessions for pages, API keys for `/api` (and ingest).
- Ingest is the one JSON endpoint outside `/api`, for compatibility.
