# Web UI Design

How AgentLedger's web UI looks and behaves: principles, structure, shared building blocks, design tokens and Blazor conventions. Read it before adding or changing a page. Where the UI sits in the architecture (a thin entry point over the use cases) is in [`architecture.md`](architecture.md); the decisions behind it are in [ADR 0015](adr/0015-web-ui-and-evidence.md).

This document grows with the UI. If a page needs something not covered here, add it here first, so the next page does it the same way.

## Principles

- **An engineering tool, not a marketing site.** Information-dense, calm, fast. Clarity over decoration.
- **Read-only.** The ledger is append-only; the UI never edits or deletes recorded data.
- **Evidence is one click away.** Anything interpreted (projections, summaries) links to the raw events it came from ([ADR 0015](adr/0015-web-ui-and-evidence.md)).
- **Raw means raw.** The "Raw events" section shows what agents sent, with no agent-specific interpretation. Meaningful summaries, event categories and colors come with projections.
- **Every view is a URL.** Filters and paging live in the query string, so any view can be bookmarked, shared and reached with the back button.
- **Works without JavaScript.** Pages are rendered on the server; script only enhances (e.g. copy buttons). The work pages are interactive (ADR 0023), but every selection there is still a real link, so they work as plain page loads too.
- **Light and dark themes**, following the operating system.

## Structure

### Navigation shell
A header with the product name and the ledger's version (from `/status`, e.g. `0.1.0+e62e9c2`), then a navigation bar with these sections:

| Section | Pages | Phase |
|---|---|---|
| **Work** | `/work` (units of work, turns, steps; interactive), short links `/work/turns/{id}` and `/work/steps/{id}` | 4 |
| **Raw events** | `/raw/events`, `/raw/events/{eventId}` | 2 (v1) |
| **Raw transcripts** | `/raw/transcripts`, `/raw/transcripts/{lineId}` | 3 |
| *(future)* Ingestion health | arrivals, duplicate rate, unknown event types | later |
| **Settings** | `/settings/api-keys`; *(future)* users | 2 |

"Work", "Raw events", "Raw transcripts" and "Settings" exist today. The home page `/` and the product name go to "Work".

The header also holds the account menu when signed in: the username, "Change password" and a "Sign out" button (a POST form, [ADR 0018](adr/0018-password-login.md)). Signed out, the header shows only the product name and version, and there is no section navigation.

### Work pages
The main pages once projections exist ([ADR 0022](adr/0022-projections.md), [ADR 0023](adr/0023-interactive-work-pages.md)): one interactive page, `/work`, with three panes.
- **Left: units of work** for the chosen lens (only *Session* so far). Each row: title, project, branch, turn count, last activity, "● active" within 10 minutes. 50 at a time, "Show older sessions" for more.
- **Middle: the selected unit**, a four-line summary, then its turns oldest first under a column header that stays visible (`#`, `Time (UTC)`, `Prompt`, `Status`, `Duration`, `Tools`) and a divider for each day (`Tue 27 Oct 2026`; a session can span days), each with number, time of day (the full timestamp on hover), prompt summary, status, duration and tool count; injected and queued prompts labelled. One turn expanded at a time; its steps one line each (time since the turn started, kind or tool name, summary, outcome and duration for tools; thinking without text says "summary not available", dimmed).
- **Right: the selected step** (only while one is selected): header, content for its kind (Markdown text with a Rendered/Plain switch; a tool's input as name and value pairs or JSON, its output with "Show all" beyond 200 lines, an error shown as an alert), the response's tokens ("this response"), and the evidence, collapsed, each source expanding to its raw JSON in place and linking to its raw page in a new tab.
- **URL:** `/work?unit=…&turn=…&step=…` (the session lens is the default and left out). Choosing a session or turn adds a history entry; choosing a step replaces it. The close link and Esc remove the step.
- **Panes:** the dividers are `role="separator"` elements: drag, or arrow keys when focused, to resize; double-click, Enter, Home or End, or the collapse button to collapse. Widths and collapsed panes are remembered per browser. Below 1,200 px the details slide over the turns.
- **Markdown** goes through `MarkdownDisplay` only (see ADR 0023 for its rules): raw HTML shown as text, links only for http, https and mailto (new tab, `noopener noreferrer nofollow`), images never loaded.

### Raw transcripts pages
Built **exactly like Raw events** ([ADR 0020](adr/0020-transcripts.md)): the same layout, building blocks and URL rules, so the two raw sections read the same way.
- **List** `/raw/transcripts`: every transcript line, newest first (received time, then byte offset, then line ID; so a session's lines appear in exact file order, reversed, like events). A **DataTable** with: received (links to the line), agent, line type (the line's own top-level `type` field, in a neutral **EventTypeBadge**), session (**ShortId**, links to the session-filtered list), file (`main` or "subagent a1b2c3"), byte offset, preview. A **FilterBar** with agent, line type, session, file and the received time range; a **Pager** (Newer / Older); an **EmptyState**.
- **Detail** `/raw/transcripts/{lineId}`: the line's envelope as a `<dl>` (line ID, agent, session, file, byte offset, line type, received, sent by: owner and key), a subagent's raw meta in a **JsonViewer**, then the line itself in a **JsonViewer** (Formatted / Exact).
- **Links between the sections:** an event's Session row and the session-filtered event list link to that session's transcript lines (`/raw/transcripts?session=…`), and a line's Session row links to the session's events. The released `/raw/sessions/{session}/transcript` redirects to the session-filtered list.
- Raw means raw: the line type is shown as written; reading meaning into lines (conversations, cost) is Phase 4.

### Settings pages
`/settings/api-keys` ([ADR 0019](adr/0019-api-keys.md)) lists keys in a **DataTable** (members their own, admins everyone's), with a create form above it. A new key is shown **once**, in a status panel with a **CopyButton**; it never appears again. Each active key's row has a "Revoke" button: a POST form per row, with its own form name (`revoke-<keyId>`). Statuses come from `ApiKeyStatus` (Active, "Expires in N days" within 14 days, Expired, "Revoked by …").

### Account pages
`/login`, `/setup` (first run only) and `/account/password` are each a single **FormPanel**. Every page requires sign-in unless it is explicitly marked `@attribute [AllowAnonymous]` (deny by default); only the login, setup and not-found pages are.

### URLs
- Lowercase, kebab-case paths; sections are top-level segments (`/raw/…`).
- **Permanent:** a page's URL never changes meaning once released. `/raw/events/{eventId}` in particular is the evidence link projections will use.
- Identifiers appear in full in URLs (never shortened).
- Pages own the plain paths; the JSON read API with the same data lives under `/api` (`/api/raw/events`, [ADR 0017](adr/0017-api-prefix-for-read-endpoints.md)).
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
| **CopyButton** | copying a value (used by ShortId and JsonViewer) | A `<button>` with an accessible label (`Copy event ID`) and `data-copy`; `app.js` does the copying. Hidden unless `app.js` runs (`html.js`), so there is never a button that can't work. |
| **FormPanel** | a small form on its own (sign in, setup, change password) | A centered panel: an `<h1>` title, an optional intro, errors in an `alert`, a success message in a `status`, then the form. Fields are stacked, each with a visible `<label>`; one primary submit button. Use `EditForm` with a `FormName` (static SSR form handling and antiforgery) and the right `autocomplete` values (`username`, `current-password`, `new-password`). |
| **EmptyState** | a list with no results | Says what was searched for ("No events match these filters") and offers the way out ("Clear filters"). |

**Errors:** an unexpected failure shows the standard error page with a short reference, never a stack trace. Not found (e.g. an unknown event ID) is a proper 404 page that links back to the list: a page calls `NavigationManager.NotFound()`, which renders `Pages/NotFound.razor` (the Router's `NotFoundPage`) with status 404. Don't set `Response.StatusCode` yourself: the renderer then sends an empty body. **A page that calls `NotFound()` or `NavigateTo()` renders nothing else**, forms above all (a `Leaving` flag around its markup): the not-found page or redirect replaces it, and a form discarded mid-render intermittently fails with "The renderer does not have a component with ID …" (a 500; seen on `/setup` in CI). Unusable filters (an unknown agent, a bad date or cursor) are not errors: the list page explains them in an alert and stays 200.

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

- **Render mode:** static server-side rendering (the default). A component may use `@rendermode InteractiveServer` only when it needs live interaction a link or form can't provide; note why in a comment. So far that's the work page.
- **Interactive pages** ([ADR 0023](adr/0023-interactive-work-pages.md)):
  - Prerendered, so the first response is complete HTML. Data loaded while prerendering is handed over with `[PersistentState]` rather than loaded again. The browser sends that data back when it connects, so the connection accepts messages up to 4 MB (`AddHubOptions` in `Program.cs`): at SignalR's default 32 KB, long sessions never became interactive and every click was a full page load.
  - Selections are real links (`data-work-nav`); the page's script turns a plain click into a history update plus a call to the page, so the page never re-renders on the server for a click. Modified clicks open new tabs as usual.
  - The connection rechecks the security stamp (`CircuitSessionRevalidation`); when it fails, the page goes to the login page.
  - `blazor.web.js` starts with `ssr: { disableDomPreservation: true }`: enhanced navigation and form handling stay off, so static pages behave exactly as plain HTML pages.
  - Lost connections show the shell's small "Reconnecting…" notice.
- **Script modules:** a page's own behavior lives in a collocated module (`Page.razor.js`), imported by the page. A module may set CSS variables on the element it manages (e.g. pane widths); markup still never has inline styles.
- **Folders** (in `src/AgentLedger.Web/Components/`):
  - `Layout/` for the shell (`MainLayout`, `NavMenu`);
  - `Pages/<Section>/` for routable pages, e.g. `Pages/Raw/RawEvents.razor` (`@page "/raw/events"`);
  - `Shared/` for the building blocks above.
- **Data:** pages send queries through `IMediator`, exactly like endpoints, and receive DTOs. Never inject `DbContext` or a repository into a component.
- **Query string:** bind filters and the cursor with `[SupplyParameterFromQuery]`.
- **Styles:** tokens and base styles in `wwwroot/app.css`; component-specific styles in the component's scoped `.razor.css`. No inline `style` attributes.
- **Script:** only small progressive enhancements in `wwwroot/app.js` (e.g. copy to clipboard). The page must still work if it doesn't load.
- **Logic out of markup:** formatting and mapping (timestamps, display names, JSON formatting) live in plain C# classes with unit tests; components only render.
- **Testing:** functional tests request pages over HTTP against the Testcontainers database and check the status and key content (e.g. a stored event's ID appears in `/raw/events`). Interactive behavior (clicks, toggles, selection changes) is tested with **bUnit** against a substituted `IMediator`.
