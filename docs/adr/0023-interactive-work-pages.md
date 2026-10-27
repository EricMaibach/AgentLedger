# 0023. Work pages: Interactive Server, three resizable panes, selection in the URL

- **Status:** Accepted (amends [ADR 0015](0015-web-ui-and-evidence.md): the work pages are interactive)
- **Date:** 2026-10-26

## Context

The projections ([ADR 0022](0022-projections.md)) are explored on one page: pick a unit of work, open its turns, read their steps one by one. The pages so far are static server-side rendering ([ADR 0015](0015-web-ui-and-evidence.md)), where every click is a request that re-sends the whole page. That suits the raw list and detail pages. For this page it would mean rebuilding all three panes on every click and losing their scroll positions. The raw pages' "open a new page for the details" pattern was also judged a poor fit for following a conversation.

The audience is developers: designed for a 22" monitor and workable on a 14" laptop (about 1366 to 1440 px wide). Live updates of running sessions are wanted later, and other pages may need interactivity too.

## Decision

**Rendering**
- The **work section** uses Blazor **Interactive Server**:
  - clicks update only what changed over the page's live connection;
  - live updates can later be pushed over the same connection, with no extra channel or script.
- The first request is still rendered on the server (prerendering), so content appears immediately and functional tests can check it.
- Other pages stay static; any future page may opt in, noting why.
- Rejected:
  - **static pages:** a full round trip per click;
  - **Interactive WebAssembly:** a separate client project, a larger download, and its own API or hub for pushes;
  - **static pages with hand-written JavaScript:** two approaches mixed.
- **Tests:** interactive components are tested with **bUnit** (new test dependency). Prerendered output keeps its functional tests.
- Requirements: JavaScript and WebSockets (a reverse proxy must allow them), and a little server memory per open tab. Several API instances would need sticky sessions; one container doesn't.

**Layout**
- Three panes, each scrolling on its own:
  - **units of work** on the left;
  - **the unit's summary and its turns** in the middle (a turn expands into its steps);
  - **the selected step's details** on the right, including its evidence.
- **The side panes:**
  - can be **resized** by dragging their dividers, or with the arrow keys on a focused divider (the accessible "separator" pattern);
  - can be **collapsed** with a button, by double-clicking the divider, or by Enter on it, leaving a thin bar to expand them again;
  - have minimum widths (about 200 px left, 320 px right). The middle keeps at least about 480 px.
- **Dragging is done by a small JavaScript module, not Blazor.** Sending every mouse movement to the server would lag. Blazor learns the final widths.
- Widths and collapsed state are **remembered per browser** (local storage). They're a personal display preference, not part of a shared link.
- Below about 1200 px the details pane slides over the turns instead of squeezing them. That's the only narrow-screen behaviour; phones aren't a target.

**URLs**
- The section is `/work`.
- The selection lives in the query string and is updated as you click, without a reload: `?lens=…&unit=…&turn=…&step=…`. So the back button, bookmarks, reloads and shared links all work.
- Permanent short links, such as `/work/turns/{id}` and `/work/steps/{id}`, redirect to the full URL. Raw pages and the MCP link to these.
- The IDs are the projections' stable, content-derived IDs ([ADR 0022](0022-projections.md)), so links survive rebuilds.

**Not decided here:** how the server learns that new data has landed for live updates (an in-process signal or Postgres `LISTEN/NOTIFY`). That will be decided when live updates are built.

## Consequences

- Following a conversation is fast, and the panes keep their place.
- The work section needs JavaScript; the raw pages still work without it.
- Our first JavaScript module (the divider), and bUnit as a second kind of UI test.
- `docs/ui.md` gains a section for interactive pages, the pane layout and the divider. It's written when the work page is built.
