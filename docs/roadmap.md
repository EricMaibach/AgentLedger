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
2. **Visibility and access (released in v0.2.0 and v0.3.0; user management, HTTPS and GitHub login when needed).** People and AI can see what was logged without direct database access, and every entry point is authenticated. A web UI (raw event explorer) and read API, password login, scoped API keys (ingest required on `POST /events`), HTTPS, and optional GitHub login. Pulled ahead of transcripts: without it there's no way to view or validate the ledger, and ingest is currently open to the network.
3. **Transcripts (next).** The CLI reads agent transcripts incrementally on every hook, capturing assistant responses, reasoning (where present) and per-response token usage, which hooks don't expose ([ADR 0020](adr/0020-transcripts.md)).
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
- [x] **Verification** (2026-10-23), against the probe hook registered for the same 27 Claude Code events:
  - every probe capture since dogfooding began (2026-09-25) is in the ledger with a byte-identical payload, and the ledger holds no payload the probe didn't capture;
  - 19 event types confirmed; PermissionRequest hasn't occurred since dogfooding began, and 11 registered events have never fired in this usage;
  - spool: with the API stopped, events waited in the spool and were delivered when it came back (19 of 19 captured during the outage arrived, byte-for-byte).

**Phase 1 is done when:**
- every hook event from a real Claude Code session is stored in Postgres, with its payload intact;
- a resend produces an extra receipt, and each agent event is still identifiable once by `event_id` (deduplication itself comes with projections).

### Phase 2: Visibility and access

Each step is designed with the user first (one topic at a time), then built test-first.

1. [x] **Web UI: raw event explorer** (released in v0.2.0)
   - `/raw/events` and `/raw/events/{eventId}` pages over a read API under `/api` ([ADR 0015](adr/0015-web-ui-and-evidence.md), [ADR 0016](adr/0016-read-api-and-keyset-paging.md), [ADR 0017](adr/0017-api-prefix-for-read-endpoints.md)), design system in `docs/ui.md`.
2. [ ] **Password login** ([ADR 0018](adr/0018-password-login.md)):
   - [x] **2a** (released in v0.2.0): first-run setup page, login, sign-out, change own password, `reset-password` command, deny-by-default authorization, local-only port binding. **Released together with step 1.**
   - [ ] **2b:** user management (`/settings/users`), member and admin roles, temporary passwords changed at first sign-in. Disabling a user also stops their API keys (ADR 0019).
3. [x] **API keys** ([ADR 0019](adr/0019-api-keys.md), released in v0.3.0): personal keys with one scope each (ingest or read), `/settings/api-keys`, ingest always requires a key, receipts record the sending key, CLI `login` and spool-on-401/403.
4. [ ] **HTTPS**, for reaching a ledger from other machines:
   - the app honors a reverse proxy's forwarded headers (`Secure` cookies, `https` redirects);
   - documented recipes for personal setups: Tailscale (no domain needed), Caddy with your own domain, an existing proxy;
   - until then the stack publishes its port only locally (ADR 0018).
5. [ ] **GitHub login** (optional):
   - an **allowlist** of GitHub users or an organization, since GitHub login alone admits any GitHub account;
   - each installation registers its own GitHub OAuth app. Password login remains the default.

## Next task: transcripts (Phase 3)

**Goal:** the transcript's content (responses, models, token usage) in the ledger, raw and byte-for-byte, seconds after it's written. Decisions: [ADR 0020](adr/0020-transcripts.md). Claude Code only.

**Steps (test-first):**
1. ✅ Core (`TranscriptAggregate`): `TranscriptFileName` (`main`, `subagents/agent-<id>`, with `SubagentId`), content-derived `TranscriptLineId` and `TranscriptFileId` (SHA-256 of length-prefixed fields, UUID version 8; pinned by values computed independently), `TranscriptLine` and `TranscriptFile`.
2. ✅ Infrastructure: `transcript_lines` (`text`, line ID as primary key, `byte_offset`, indexed for reading in order) and `transcript_files` (raw subagent meta, filled in once); `ITranscriptStore` write store: one transaction, `unnest` insert with `ON CONFLICT DO NOTHING`, audit timestamps set explicitly.
3. ✅ Use cases: `IngestTranscriptBatch` (envelope validation, 1–1,000 lines with distinct offsets, meta only for subagents; stored and already-stored counts), `ListTranscriptFiles` (main first, line counts) and `ListTranscriptLines` (keyset on byte offset and line ID) over `ITranscriptQueryService`.
4. ✅ Web: `POST /transcripts/lines` (ingest key, gzip request decompression, 8 MB limit after decompression answered with 413). Reading back, revised to work like raw events: `GET /api/raw/transcripts` (filters, keyset paging newest first) and `GET /api/raw/transcripts/{lineId}`; a "Raw transcripts" section with list and line pages; `line_type` column (backfilled by migration); session links between events and transcript lines; the v0.4.0 per-session page redirects.
5. ✅ CLI: `ITranscriptSource` (Claude Code: `transcript_path` and every subagent file in the session's folder, revised after the rollout check found a subagent's last line written after its final hook); `TranscriptStateStore` (offset, fingerprint, lock file); `TranscriptReader` (complete lines, byte offsets, 1 MB per run, lines over 4 MB skipped and logged, restart on shrink or replacement); batches of 1,000 lines or 4 MB in a separate spool folder, sent gzipped, up to 3 per hook; `flush` and `status` cover them. Also fixed: the CLI wrote its spool into the current directory when `~/.local/share` didn't exist yet (`SpecialFolderOption.DoNotVerify`).
6. Rollout: ✅ v0.4.0 deployed 2026-10-25; this session's main transcript (6,793 lines) verified byte-for-byte against the file. Next: release the revised read side (v0.5.0), and check the migration's `line_type` backfill against the stored text.

## Done before: API keys (Phase 2, step 3)

API keys released in v0.3.0 ([ADR 0019](adr/0019-api-keys.md)); the dev container sends with its own ingest key, and new receipts record it. Remaining Phase 2 work, when needed: 2b (user management), HTTPS (step 4), GitHub login (step 5).

**Known flaky test:** `SetupTests.CreatesTheFirstAdminOnceAndThenDisappears` failed once in a full `dotnet test` run (2026-10-25) and passed in 5 full runs and 3 single runs afterwards; the failure message wasn't kept. When it fails again, keep the full output and find the cause rather than retrying.

**Open question:** a URL that matches no page (e.g. `/raw/events/not-a-guid`) returns 404 with an empty body, because it never reaches Blazor. `UseStatusCodePagesWithReExecute("/not-found")` would fix it but also changes `/api` 404s; decide when convenient.

**Deferred from the EF mapping task:**
- An index on `context_git_repo` / `context_git_branch`. EF 10 can't declare an index on complex-type columns. Add it with `migrationBuilder.Sql` when a projection or query actually filters by repo or branch.
- `AgentEventReceiptBuilder` lives in the unit test project, while the integration tests use a small local factory. If a third test project needs one, move the builder into a shared test-support project.

## Later phases (outline)

- **Transcripts (Phase 3):** designed in [ADR 0020](adr/0020-transcripts.md); see "Next task".
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
