# 0015. Web UI: Blazor static rendering, no component library, raw data as evidence

- **Status:** Accepted
- **Date:** 2026-10-01

## Context

People and AI need to see what the ledger holds without direct database access, which stays private to the stack. The first UI is needed now, for validation and everyday visibility, while projections (sessions, turns, tool calls, cost) come later. Its role in the long term shapes what to build first.

## Decision

**Evidence principle.**
- Projections are *interpretation* (what an adapter believes happened); raw receipts are *evidence* (exactly what an agent sent).
- **Every projected item links to the raw events it was built from, and the raw events are always reachable.** So projections must store the IDs of their source events (lineage), starting with the first projection.
- Once projections exist they are the main pages (sessions, conversation timelines, tool calls, tags, cost). The raw explorer remains, as:
  - **evidence:** the drill-down target from every projection;
  - **diagnostics:** validating new agents, debugging adapters, checking rebuilds;
  - **fallback** for events no adapter understands.

**First version: the "Raw events" section.**
- `/raw/events` is the list:
  - filters: agent, event type, session, host, repo, time range;
  - a "one row per event" toggle, computed at query time;
  - keyset paging;
  - when filtered to a session, the event-type counts for it.
- `/raw/events/{eventId}` is the event detail: envelope, all receipts, and the payload pretty-printed or exact. Its URL is permanent, since projections will link to it.
- **No raw "sessions" page.** The sessions list is the first projection page.
- Previews in the list are raw text; meaningful summaries belong to projections ([ADR 0003](0003-agent-neutral-core-with-adapters.md)).
- The layout has a navigation shell from the start.

**Technology.**
- **Blazor**, rendered **server-side (static SSR)** by default, with interactivity only where a page needs it. The pages live in `AgentLedger.Web` and call the same query use cases as the API endpoints (through Mediator, never `DbContext`).
- **No component library.** Semantic HTML, our own CSS with design tokens as CSS variables (including dark mode), and a small set of our own components (data table, filter bar, timestamp, short ID, event-type badge, JSON viewer), defined in `docs/ui.md`.
  - Fluent UI Blazor was considered. Many of its components need interactive mode, which works against static rendering, and v5.0.0 was released two days before this decision.
  - A library can still be adopted later for rich interactive views (timelines, charts).
- The UI is released only together with password login ([roadmap](../roadmap.md), Phase 2), because the stable ledger is reachable from the network.

## Consequences

- One language and one deployable; the UI is another thin entry point over the use cases.
- We build and maintain a handful of UI components ourselves; the first pages look plain.
- The raw pages built now remain in the final product, so their URLs and the event detail page get care now.
- Phase 4 projections carry a lineage requirement.
