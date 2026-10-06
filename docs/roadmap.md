# Roadmap

What AgentLedger is for, where it stands, and what to do next. This is a living document: update the status and the "Next task" section as work lands. The engineering approach is in [`architecture.md`](architecture.md), and decisions are in [`adr/`](adr/).

## Vision

AI coding agents do more and more of the work in a codebase: they read files, run commands, edit code and spawn sub-agents. AgentLedger is an **auditable ledger of what they did**. It captures everything each agent exposes (prompts, tool calls, responses, reasoning, token usage) into one store, across agents, so that people and other AI can review, measure and correlate agent activity.

Supported agents (planned): **Claude Code, Codex, GitHub Copilot (CLI and VS Code), Snowflake Cortex Code.**

How it works (see ADRs [0001](adr/0001-raw-event-ledger-with-projections.md), [0002](adr/0002-hook-ingestion-via-cli.md)):

```
agent hook ──stdin──► agentledger CLI ──HTTP──► API ──► Postgres
                      (envelope, spool,          (ingest      agent_event_receipts
                       always exit 0)             use case)       │
                                                                  ▼
                                                        projections: sessions, turns,
                                                        tool calls, tokens, cost
                                                                  │
                                                     query API · read-only MCP server
```

## Phases

1. **Capture.** Every hook event from Claude Code lands in Postgres as a raw event. Done, apart from final verification; the stable ledger records this project's own development.
2. **Visibility and access (current).** People and AI can see what was logged without direct database access, and every entry point is authenticated. A web UI (raw event explorer) and read API, password login, scoped API keys (ingest required on `POST /events`), HTTPS, and optional GitHub login. Pulled ahead of transcripts: without it there's no way to view or validate the ledger, and ingest is currently open to the network.
3. **Transcripts. Critical, immediately after visibility.** The CLI reads agent transcripts incrementally, capturing assistant responses, reasoning and per-message token usage, which hooks don't expose. It's also the only way to recover what happened before hooks were installed.
4. **Projections.** Deduplicated agent events, then sessions, turns, tool calls and cost, derived from raw events. The UI gains session and cost views; the read-only MCP server ([ADR 0008](adr/0008-read-only-mcp-server.md)) exposes them to AI.
5. **Delivery.** Mostly done early: CI on every push, the container image and GitHub Releases (ADR 0014). Remaining: architecture tests, and CLI binaries attached to releases.
6. **More agents.** Capture real payloads, then an adapter per agent: Codex, Copilot CLI, VS Code Copilot, Cortex Code.

## Status

### Phase 1: Capture

- [x] Dev container with Postgres 18 (`.devcontainer/`)
- [x] Hook research and real Claude Code payload captures (`docs/research/agent-hook-capabilities.md`)
- [x] Solution skeleton from the Clean Architecture template ([ADR 0007](adr/0007-clean-architecture-template-without-aspire.md)); `GET /status`; Testcontainers-based functional tests
- [x] Raw event aggregate: `AgentEventId`, `AgentKind`, `CaptureContext`, with unit tests
- [x] EF Core mapping + first migration ([ADR 0009](adr/0009-ledger-storage-details.md)), with integration tests on Testcontainers
- [x] Raw table holds receipts: `AgentEventReceipt` with `ReceiptId` + `EventId`, duplicates landed ([ADR 0010](adr/0010-raw-landing-accepts-duplicates.md))
- [x] `IngestEvent` use case: command + handler, registered with Mediator
- [x] `POST /events` endpoint ([ADR 0011](adr/0011-ingest-envelope-contract.md) envelope contract)
- [x] CLI design ([ADR 0012](adr/0012-cli-design.md))
- [x] `agentledger` CLI: `hook`, `status`, `flush`, `install`/`uninstall`, Native AOT ([ADR 0012](adr/0012-cli-design.md), [ADR 0013](adr/0013-hook-installation-and-opt-out.md))
- [x] Dogfooding ([ADR 0014](adr/0014-releases-and-dogfooding.md)):
  - GitHub Release → CI → image on GHCR (v0.1.0);
  - the stable ledger runs as a Portainer stack (`deploy/compose.yml`, port 58090);
  - the dev container records every Claude Code session into it.
- [ ] **Verification:** after some real use, check that every event type from the probe captures arrives, that payloads are byte-for-byte (compare with `.probe/`), and that the spool delivers after the ledger is stopped and restarted. Easiest once the explorer UI exists.

**Phase 1 is done when:**
- every hook event from a real Claude Code session is stored in Postgres, with its payload intact;
- a resend produces an extra receipt, and each agent event is still identifiable once by `event_id` (deduplication itself comes with projections).

### Phase 2: Visibility and access

Each step is designed with the user first (one topic at a time), then built test-first.

1. [ ] **Web UI: raw event explorer** ← next task
   - query use cases, a read API, and pages served by the API itself;
   - built and tested in the dev container only: **not released without step 2**, because the stable ledger's port is reachable from the network.
2. [ ] **Password login:**
   - users, with the first admin bootstrapped from a stack variable;
   - **released together with step 1.**
3. [ ] **API keys:**
   - a management page for signed-in users;
   - keys are **scoped**: *ingest* keys only send events; *read* keys only read (MCP server, scripts, CLI viewing if ever added). A leaked ingest key must not expose the ledger, and no key can manage keys.
   - `POST /events` requires an ingest key;
   - the CLI gets a way to store its key in the **user** config (never the project file, ADR 0012), and the dev container a non-committed source for it;
   - enforcement is switched on after the CLI is configured, so the spool bridges the gap.
4. [ ] **HTTPS** for the stable ledger:
   - e.g. a reverse proxy in the stack;
   - passwords and keys must not cross the network in plain HTTP, and GitHub login requires HTTPS.
5. [ ] **GitHub login** (optional):
   - an **allowlist** of GitHub users or an organization, since GitHub login alone admits any GitHub account;
   - each installation registers its own GitHub OAuth app. Password login remains the default.

## Next task: web UI, the "Raw events" section

**Goal:** browse what's in the ledger without database access, for validation (Phase 1 verification) and everyday visibility. Decisions: [ADR 0015](adr/0015-web-ui-and-evidence.md) (Blazor static SSR, no component library, raw data as evidence).

**Scope of v1:**
- `/raw/events`: list (newest first).
  - Filters: agent, event type, session, host, repo, time range.
  - "One row per event" toggle (query-time `DISTINCT ON (event_id)`, shows ×N for resends).
  - Keyset paging.
  - When filtered to a session, that session's event-type counts.
- `/raw/events/{eventId}`: envelope fields, every receipt, and the payload pretty-printed or exact, with copy. A **permanent URL**: projections will link here.
- A navigation shell, with the ledger version in the header. No raw sessions page; previews are raw text.

**Steps:**
1. ✅ Design: UI technology and what v1 shows ([ADR 0015](adr/0015-web-ui-and-evidence.md)).
2. ✅ Design: read API shape ([ADR 0016](adr/0016-read-api-and-keyset-paging.md)): three endpoints, keyset paging, a query service for the list and counts, a Specification for event detail.
3. ✅ Design: no CLI viewing commands for now (see "Ideas").
4. ✅ `docs/ui.md` (the UI design system) and the `web-ui` skill (`.claude/skills/web-ui/`), which loads it only for UI work.
5. Build, test-first:
   - **written by the user:** `ReceiptsByEventIdSpec`, the `IRawEventQueryService` interface and its EF query (keyset paging, first receipt per event, counts; Claude guides), the query handlers and the three endpoints;
   - written by Claude: the `(received_at, id)` index migration, the Blazor pages and components, the CSS, and wiring.
6. Not released until password login (Phase 2, step 2) is done.

**Deferred from the EF mapping task:**
- An index on `context_git_repo` / `context_git_branch`. EF 10 can't declare an index on complex-type columns. Add it with `migrationBuilder.Sql` when a projection or query actually filters by repo or branch.
- `AgentEventReceiptBuilder` lives in the unit test project, while the integration tests use a small local factory. If a third test project needs one, move the builder into a shared test-support project.

## Later phases (outline)

- **Transcripts (Phase 3):**
  - The CLI tracks a read offset per `transcript_path` and sends new transcript lines as events.
  - Subagent transcripts too (`agent_transcript_path` from `SubagentStop`).
  - Token usage and model per assistant message come from here.
- **Projections (Phase 4):**
  - **Lineage (required):** every projected row stores the IDs of the raw events it was built from, so the UI can link to the evidence ([ADR 0015](adr/0015-web-ui-and-evidence.md)).
  - **Agent events** (`agent_events`): receipts deduplicated by `event_id` with idempotent upserts ([ADR 0010](adr/0010-raw-landing-accepts-duplicates.md)). Probably the first projection, since the others build on it.
  - **Sessions:** keyed by `(agent, native session ID)`, with activity periods and an inferred end ([ADR 0004](adr/0004-session-end-is-inferred.md)), plus lineage (clear, resume, fork) recorded as relationships.
  - **Turns:** grouped by Claude Code's `prompt_id`.
  - **Tool calls:** Pre/Post paired by `tool_use_id`, failures included.
  - **Token usage and cost.**
  - Projections are built by per-agent adapters ([ADR 0003](adr/0003-agent-neutral-core-with-adapters.md)) and can be rebuilt from raw events.
- **Known capture quirks the Claude Code adapter must handle** (details in the research doc):
  - internal helper subagents with an empty `agent_type` and no SubagentStart;
  - `UserPromptSubmit` events for injected messages that weren't typed by a human;
  - `SessionStart` with nothing after it;
  - `cwd` changing partway through a session;
  - `PostToolBatch` duplicating `tool_response`.

## Capture probe

`tools/probe/capture.sh` is a throwaway hook that dumps every payload and a redacted environment to `.probe/` (gitignored), for researching an agent's hooks before writing its adapter.

- **Claude Code:** captured. Not yet triggered: permission request/denied, fork, `UserPromptExpansion`.
- **Codex, Copilot, Cortex Code:** not yet captured.
- Captures contain prompts and conversation text. Review and sanitize them before committing any as test fixtures.

## Open questions

- Should AgentLedger log its own development (dogfooding)? If so, run a separate "stable" API instance for that, so a broken dev build doesn't lose data.
- OpenTelemetry (OTLP) as a second ingestion channel for cost and token metrics. Claude Code, Codex and Copilot all export it.

## Planned enhancements

- **Fewer duplicate receipts from parallel hooks:** when `hook` resends waiting events, skip spooled events younger than about 30 s. Seen when dogfooding: Claude Code fires several hooks at once (e.g. `InstructionsLoaded` at session start); each process resends the others' still-in-flight events, so some arrive 2–3 times. Harmless (deduplicated by `event_id`, ADR 0010) but it inflates the raw table. `flush` keeps sending everything.

- **Commit hash in the capture context** (`gitCommit`): read from the `.git` files alongside the branch, it ties agent activity to the exact code state. It's an additive change: an optional envelope field, a `CaptureContext` value and a column (see [ADR 0012](adr/0012-cli-design.md), "Future enhancement").
- **Unknown-agent error message:** the error currently echoes the converted name ("Unknown agent: Cursor") instead of what the caller sent ("cursor"). Cosmetic.

- **Reviewer agents** (after the explorer v1): an architecture reviewer that checks a change against `architecture.md` and the ADRs, and a UI reviewer that checks pages against `docs/ui.md`. Fresh context, reading only the written rules. The repo documents stay the source of truth; agents read them rather than keeping decisions in their own memory.

## Ideas (not planned)

- **CLI viewing commands** (`agentledger events …`): decided against for now (2026-10-01); revisit if a real need appears. They would call the read API with a read-scoped API key, so they only make sense after API keys exist.
- **Hash-chaining** the raw event table for tamper evidence (possible because payloads are stored byte-for-byte; [ADR 0009](adr/0009-ledger-storage-details.md)).
- **Redaction** of secrets and sensitive data, on the client side before sending, and/or on the server.
- **Cost rollups** per session and per tag.
- **Automated review of agent sessions** against `architecture.md`, as an external consumer of the ledger:
  - Deterministic rules (dependency direction) as architecture tests in CI (NetArchTest/ArchUnitNET).
  - A fast structured-output model to run a fixed checklist over every session.
  - An LLM, via the MCP server, to explain only the flagged sessions.
- **Deployment** to a cloud environment (container-based), once the Delivery phase is complete.
