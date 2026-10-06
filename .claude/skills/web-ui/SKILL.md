---
name: web-ui
description: Use when creating or changing AgentLedger's web UI — Blazor pages, layout, shared components, CSS or design tokens under src/AgentLedger.Web/Components or wwwroot. Loads the UI design rules (docs/ui.md) and the checklist for adding a page.
---

# Working on the AgentLedger web UI

1. **Read `docs/ui.md` first.** It is the design system: principles, URL rules, the shared building blocks, design tokens, accessibility baseline and Blazor conventions. Follow it; if a page needs something it doesn't cover, add the pattern to `docs/ui.md` before using it.
2. Background decisions: [ADR 0015](../../../docs/adr/0015-web-ui-and-evidence.md) (Blazor static SSR, no component library, raw data as evidence) and [ADR 0016](../../../docs/adr/0016-read-api-and-keyset-paging.md) (read API, keyset paging). Architecture rules still apply: `docs/architecture.md`.

## Checklist for a new or changed page

- [ ] Route follows the URL rules (lowercase, section prefix, permanent, filters in the query string with the read API's parameter names).
- [ ] Data comes from a query sent through `IMediator`; no `DbContext` or repository in the component.
- [ ] Static SSR; `@rendermode InteractiveServer` only with a comment saying why.
- [ ] Uses the shared components (DataTable, FilterBar, Pager, Timestamp, ShortId, AgentName, EventTypeBadge, JsonViewer, EmptyState) rather than one-off markup.
- [ ] Colors, fonts and spacing only through the CSS variables in `wwwroot/app.css`; component styles in a scoped `.razor.css`; no inline styles.
- [ ] Raw pages show raw data: no agent-specific interpretation (that belongs to projections).
- [ ] Accessible: semantic HTML, labelled inputs, keyboard-usable, visible focus, a unique `<title>`, color never the only signal.
- [ ] Formatting/mapping logic in plain C# classes with unit tests; a functional test requests the page and checks status and key content.
- [ ] Works in light and dark themes, and without JavaScript.
