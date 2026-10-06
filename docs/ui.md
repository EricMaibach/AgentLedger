# Web UI Design

How AgentLedger's web UI looks and behaves: principles, structure, shared building blocks, design tokens and Blazor conventions. Read it before adding or changing a page. Where the UI sits in the architecture (a thin entry point over the use cases) is in [`architecture.md`](architecture.md); the decisions behind it are in [ADR 0015](adr/0015-web-ui-and-evidence.md).

This document grows with the UI. If a page needs something not covered here, add it here first, so the next page does it the same way.

## Principles

- **An engineering tool, not a marketing site.** Information-dense, calm, fast. Clarity over decoration.
- **Read-only.** The ledger is append-only; the UI never edits or deletes recorded data.
- **Evidence is one click away.** Anything interpreted (projections, summaries) links to the raw events it came from ([ADR 0015](adr/0015-web-ui-and-evidence.md)).
- **Raw means raw.** The "Raw events" section shows what agents sent, with no agent-specific interpretation. Meaningful summaries, event categories and colors come with projections.
- **Every view is a URL.** Filters and paging live in the query string, so any view can be bookmarked, shared and reached with the back button.
- **Works without JavaScript.** Pages are rendered on the server; script only enhances (e.g. copy buttons).
- **Light and dark themes**, following the operating system.

## Structure

### Navigation shell
A header with the product name and the ledger's version (from `/status`, e.g. `0.1.0+e62e9c2`), then a navigation bar with these sections:

| Section | Pages | Phase |
|---|---|---|
| *(future)* Sessions, Tool calls, Tags, Cost | projection pages; the main pages once they exist | 4 |
| **Raw events** | `/raw/events`, `/raw/events/{eventId}` | 2 (v1) |
| *(future)* Ingestion health | arrivals, duplicate rate, unknown event types | later |
| *(future)* Settings | API keys, users | 2 |

In v1 only "Raw events" exists. The home page `/` redirects to it until projection pages exist.

### URLs
- Lowercase, kebab-case paths; sections are top-level segments (`/raw/…`).
- **Permanent:** a page's URL never changes meaning once released. `/raw/events/{eventId}` in particular is the evidence link projections will use.
- Identifiers appear in full in URLs (never shortened).
- Filters, toggles and the paging cursor are query-string parameters, using the read API's names ([ADR 0016](adr/0016-read-api-and-keyset-paging.md)): `?agent=claude-code&eventType=Stop&session=…&distinct=false&after=…`. Parameters at their default value are left out.

## Building blocks

Use these shared components (in `Components/Shared/`) instead of re-creating them on a page.

| Component | Use | Rules |
|---|---|---|
| **DataTable** | every tabular list | A semantic `<table>` with `<th scope="col">`; header row stays visible when scrolling; one row = one item, the whole row links to its detail page (a real link in the first cell, not a click handler); numbers right-aligned. |
| **FilterBar** | filters above a list | A plain `<form method="get">`: submitting changes the URL. Each input has a visible `<label>`. A "Clear filters" link resets to the bare URL. Shows how many filters are active. |
| **Pager** | below a keyset-paged list | "Newer" / "Older" links only, no page numbers (keyset paging, ADR 0016). "Newer" returns to the first page. |
| **Timestamp** | every date/time | `2026-10-01 14:03:22 UTC` in lists; with milliseconds (`14:03:22.417`) on detail pages. Rendered in a `<time datetime="…">` element with the full ISO 8601 value. Always UTC, labelled as such (per-user time zones may come later). |
| **ShortId** | every identifier (event, receipt, session) | Monospace, first 8 characters, full value in the `title` tooltip, plus a copy button. Never shortened in URLs. |
| **AgentName** | showing an agent | Display names: `claude-code` → Claude Code, `codex` → Codex, `copilot-cli` → Copilot CLI, `vs-code-copilot` → VS Code Copilot, `cortex-code` → Cortex Code. Filters and URLs use the kebab-case wire names. |
| **EventTypeBadge** | an event's type | The agent's native name (`PreToolUse`) in a small, **neutral** badge. No per-type colors in the raw section: categorizing event types is agent-specific interpretation (ADR 0003, ADR 0015). |
| **JsonViewer** | payloads | Two views: **Formatted** (indented, the default) and **Exact** (the stored text, byte for byte), switched by a query parameter (`?view=exact`). Formatting keeps key order and duplicate keys. Monospace, wraps long lines, with a copy button that copies the exact text. |
| **EmptyState** | a list with no results | Says what was searched for ("No events match these filters") and offers the way out ("Clear filters"). |

**Errors:** an unexpected failure shows the standard error page with a short reference, never a stack trace. Not found (e.g. an unknown event ID) is a proper 404 page that links back to the list.

**Loading:** pages render on the server, so there are no spinners. If a query is slow enough to notice, use Blazor's streaming rendering with a short "Loading…" placeholder, rather than making the page interactive.

## Design tokens

Every color, font, size and spacing value comes from CSS variables defined once in `wwwroot/app.css`. **No hard-coded colors or sizes in components.** Dark mode only redefines the variables.

| Token | Light | Dark | Use |
|---|---|---|---|
| `--color-bg` | `#ffffff` | `#0f1115` | page background |
| `--color-surface` | `#f6f7f9` | `#171a21` | header, table header, panels |
| `--color-border` | `#d9dde3` | `#2a2f3a` | borders, separators |
| `--color-text` | `#1b1f24` | `#e6e8eb` | body text |
| `--color-text-muted` | `#5b6470` | `#9aa3ae` | secondary text, labels |
| `--color-accent` | `#2457c5` | `#7aa2ff` | links, focus outline, primary actions |
| `--color-danger` | `#b42318` | `#ff8a80` | errors |
| `--color-code-bg` | `#f3f4f6` | `#1d212a` | payloads, IDs |

- **Type:** `--font-sans` is the system UI font stack; `--font-mono` is the system monospace stack, used for IDs, payloads and event types. Sizes: `--text-sm` 0.8125rem (tables), `--text-base` 0.9375rem, `--text-lg` 1.125rem (page titles).
- **Spacing:** `--space-1` … `--space-6` = 4, 8, 12, 16, 24, 32 px. Tables are compact (`--space-1` vertical cell padding).
- **Shape:** `--radius` 6px; borders 1px.
- All text/background pairs meet WCAG AA contrast (4.5:1) in both themes. Check any new token pair before using it.

## Accessibility baseline

- Semantic HTML first: headings in order, `<nav>`, `<main>`, real `<table>`s, `<button>` for actions and `<a>` for navigation.
- Every form control has a visible label; every page has a unique `<title>`.
- Everything works with the keyboard; focus is always visible (`--color-accent` outline).
- Color is never the only signal: badges and states always carry text.

## Blazor conventions

- **Render mode:** static server-side rendering (the default). A component may use `@rendermode InteractiveServer` only when it needs live interaction a link or form can't provide; note why in a comment.
- **Folders** (in `src/AgentLedger.Web/Components/`):
  - `Layout/` for the shell (`MainLayout`, `NavMenu`);
  - `Pages/<Section>/` for routable pages, e.g. `Pages/Raw/RawEvents.razor` (`@page "/raw/events"`);
  - `Shared/` for the building blocks above.
- **Data:** pages send queries through `IMediator`, exactly like endpoints, and receive DTOs. Never inject `DbContext` or a repository into a component.
- **Query string:** bind filters and the cursor with `[SupplyParameterFromQuery]`.
- **Styles:** tokens and base styles in `wwwroot/app.css`; component-specific styles in the component's scoped `.razor.css`. No inline `style` attributes.
- **Script:** only small progressive enhancements in `wwwroot/app.js` (e.g. copy to clipboard). The page must still work if it doesn't load.
- **Logic out of markup:** formatting and mapping (timestamps, display names, JSON formatting) live in plain C# classes with unit tests; components only render.
- **Testing:** functional tests request pages over HTTP against the Testcontainers database and check the status and key content (e.g. a stored event's ID appears in `/raw/events`).
