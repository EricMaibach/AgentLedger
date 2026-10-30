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
3. **Transcripts (released in v0.4.0 and v0.4.1).** The CLI reads agent transcripts incrementally on every hook, capturing assistant responses, reasoning (where present) and per-response token usage, which hooks don't expose ([ADR 0020](adr/0020-transcripts.md)).
4. **Projections (designed in [ADR 0022](adr/0022-projections.md) and [ADR 0023](adr/0023-interactive-work-pages.md)).** Sessions, turns and their steps, merged from hook events and transcripts, grouped into units of work (session, day, branch, commit, release, workflow tag, and an Auto default), with token usage per model and lineage to the raw evidence. A work page with three panes; the read-only MCP server ([ADR 0008](adr/0008-read-only-mcp-server.md)) right after the first projections.
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

## Next task: Phase 4, projections

**Goal:** turn the raw evidence (hook events, transcript lines, git commits and tags) into what the agents did, grouped into units of work, each projected row linked to its evidence. Decisions: [ADR 0022](adr/0022-projections.md) (model, merging, lenses, commit attribution, tokens, lineage, engine) and [ADR 0023](adr/0023-interactive-work-pages.md) (the interactive work pages).

**Scope (after researching existing tools, 2026-10-26):** units of work tied to git are the centre; trace viewing, evaluations and prompt management are well covered elsewhere and stay out. The MCP server comes right after the first projections, because agents querying their own history is the other gap the research found.

**Steps (test-first, each released and checked against the stable ledger):**
1. ✅ (released 2026-10-27) Engine and core projections, no UI: the neutral model in Core; the Claude Code adapter for prompts, thinking, responses and tool calls (hooks and transcript merged), with sanitized fixtures; projection tables with stable content-derived IDs and lineage arrays; the background worker (watermarks over `updated_at`, a whole session rebuilt at a time, advisory lock, projection version). Check: turns against prompts, tool calls against Pre/Post pairs, tokens against unique `message.id`s.
   - Built: Core `Projections/` (content-derived `SessionId`, `TurnId`, `StepId`, `ModelResponseId`; `StepKind`, `TurnStatus`, `PromptSource`; `Lineage`, `TokenUsage`, `Step`, `Turn`, `ProjectedSession`); UseCases `IAgentAdapter` and `ClaudeCodeAdapter` (hand-written fixtures copying real shapes; subagent events left for step 3), `RunProjections` (one pass: changed sessions, then a rebuild batch of 50), `ProjectionVersion.Current`; Infrastructure tables `sessions`, `turns`, `steps`, `model_responses`, `projection_state` (migration `AddProjections`, plus an `updated_at` index on `transcript_lines`), `ProjectionStore` (EF, a session replaced in one transaction; NUL shown as U+FFFD), `ProjectionSourceQuery`, `PostgresProjectionLock` (unpooled connection); Web `ProjectionWorker` (settings `Projections:Enabled`, `Lag`, `Interval`, `RetryDelay`).
   - Dry run on this session's real data before building storage (2026-10-26, read-only, counts only): 299 turns, 1,124 model responses (= unique message IDs), 885 tool calls; it found subagent tool events (with `agent_id`) being taken as the turn's own calls, Stop events missing from turn lineage, and older tool events without `prompt_id`; all three fixed with tests.
   - Changed from the ADR while building: the worker polls rather than waiting for a signal; the watermark's upper bound is the current time minus the lag, not `max(updated_at)` minus the lag (`docs/architecture.md` corrected); `steps.tool_input` is `text`, not `jsonb`.
   - Rollout check (2026-10-27): the first pass projected all 3 sessions in 1.4 s (299 completed or ended turns, 1 in progress). Against the raw rows: every hook prompt ID and every transcript prompt ID has a turn; every main-agent `PreToolUse` has a tool call step and no subagent call became one; all 1,731 response and thinking texts and all 923 string tool results match the transcript exactly; model responses match unique non-synthetic `message.id`s with the same output and cache-read totals (raw-side queries must skip the 13 lines containing `\u0000`, which Postgres can't convert, so they count slightly fewer). While this session (about 9,500 lines) is active it's rebuilt every pass in about 500 ms; acceptable for now, revisit (incremental rebuilds) if sessions grow much larger.
2. ✅ (released 2026-10-27; a fix follows) The work page with the session lens: `/work` in Interactive Server, bUnit, three resizable and collapsible panes, selection in the URL and short links, turns expanding to steps, the details pane with evidence; `docs/ui.md` gains the interactive-page conventions.
   - Built: projection version 2 (session `title` from Claude Code's latest `ai-title`, else the first prompt; step `summary` from the adapter: the first line of text, or a tool's key argument; sessions also keep their latest turn's repository, branch, host and user); UseCases `Work/` queries over `IWorkQueryService` (`ListSessions`, `GetSession`, `ListTurns`, `ListSteps`, `GetStep`, `LocateTurn`, `LocateStep`); `WorkQueryService`; `/work` (Interactive Server, prerendered, `[PersistentState]`), `WorkPage.razor.js` (links, history, Esc, dividers), `SessionList`, `SessionSummary`, `TurnList`, `StatusLabel`, `StepDetails`, `EvidenceList`, `MarkdownText`; `MarkdownDisplay` (Markdig 1.5.0); `WorkFormat`, `WorkLink`; short links; `SessionCheck` and `CircuitSessionRevalidation`; `/` and the brand go to Work; `blazor.web.js` with enhanced navigation off; bUnit 2.11.3 (the newest that targets .NET 10.0.12 packages).
   - Rollout (2026-10-27): the migration applied and the version bump rebuilt all 3 sessions in 1.5 s; titles and step summaries filled in (every prompt and response, 1,014 of 1,015 tool calls: the other's input has no text). Found: `/_framework/blazor.web.js` is refused when signed out (deny by default), so the login and setup pages loaded HTML as a script; fixed by loading Blazor's script only for signed-in users (test added), for the next release. Also found: the released image has no `blazor.web.js` at all (404), so the work page never became interactive and every click was a full page load. The SDK adds Blazor's script package only when it sees `.razor` files, and the Dockerfile restores from project files alone; fixed with `RequiresAspNetWebAssets` in the Web project (confirmed with a local image build), and a new CI job builds the image and checks both scripts are in it.
   - Found while using it (2026-10-27): a response often shows two thinking steps, the first "summary not available" and the second with text (195 responses). Version 3 merged them, assuming one block written twice; wrong: the two lines always have different signatures, so they are two reasoning blocks in one response, and Claude Code recorded a summary for only one. Version 4 shows each block again, as recorded. Lesson: check a duplicate's identity (here the signature) before merging. Also verified: `showThinkingSummaries` records summaries after a Claude Code restart.
   - Also found (2026-10-27): long sessions never became interactive in production (every click a full page load, scrolling back to the top). Reproduced with Playwright (Firefox and Chromium) against a local app and a made-up 350-turn session: the browser sends the prerendered state back on connecting, and SignalR's 32 KB default refused it. The limit is now 4 MB. The same browser run confirmed dragging, collapsing (also after a drag and while steps open), expanding, Esc, Back and remembered widths.
   - Still to check by eye: the page on a 22" monitor and a laptop: panes, dividers (drag, keys, collapse, remembered), Back/Forward, Esc, the details overlay below 1,200 px, a long tool output, Markdown in responses, evidence links.
3. The remaining steps: subagents (nested), context and event steps, internal helper counts; interrupted, failed, compacted and in-progress turns.
4. Git lenses (branch, commit attribution with reasons, release, Auto), day and workflow tag.
5. The read-only MCP server, designed with the user first.
6. Finishing touches: "Used in" back-links on raw pages, the rebuild progress notice, the narrow-screen details overlay.

## Last task: git context (before projections, done)

**Goal:** capture which commit each event was based on, and the repository's commits and tags as they appear, so Phase 4 can group work by commits and releases. Capture can't be backfilled, so it comes first. Decisions: [ADR 0021](adr/0021-git-context.md).

**Steps (test-first):**
1. ✅ (released in v0.4.2, 2026-10-26; the dev container's events record their commit) `HEAD` on every event: `gitCommit` read from `.git` in the CLI (loose or packed ref, detached `HEAD`, worktrees; none on a branch with no commits), envelope field (ADR 0011), `CaptureContext.GitCommit` (full lowercase hash, else 400) and `context_git_commit`, shown on the event page.
2. ✅ Commit records: per-worktree state and lock (`GitRepositoryStateStore`); when `HEAD` moved, `git rev-list --reverse --since=<baseline>` then `git log -z --name-status` for at most 200 commits, oldest first (baseline on first sight, 1,000 files per commit, 2 s timeout); a `spool/git/` folder; `git_commits` table (`GitCommit`, content-derived ID from repository and hash) with `IGitObservationStore`; `POST /git/observations`; `status` and `flush` cover it. Repositories without a remote are named `<host>:<working tree>`, with the same host as events (`AGENTLEDGER_HOST`). Release order: server first (an old server answers 404, and the CLI drops the batch).
3. ✅ Tag observations: a fingerprint of the tag files (`refs/tags/`, `packed-refs`); `git for-each-ref` when it changed, compared with the last list (state per shared git folder, so worktrees report a tag once); changes existing (baseline), created, moved, deleted, each with the commits it needs; batches of 1,000; `git_tag_observations` (append-only; each observation has an ID the CLI assigns, so a batch sent twice by parallel hooks is stored once), stored in one transaction with the batch's commits. `GitCommitCatchUp` became `GitCatchUp`.
4. ✅ Rollout check (2026-10-26, v0.4.5): the 3 commits since the baseline match `git log` exactly (parents, messages, file lists); the 7 local tags match `git for-each-ref`. Found: each baseline tag stored twice (a batch sent by two parallel hooks), fixed by client-assigned observation IDs; the 7 duplicate rows from before the fix are exact copies, left in place. After the fix (v0.4.6): `v0.4.4` and `v0.4.5`, fetched from GitHub, were each recorded once as created, at the commits git reports; the new commit `ec4be68` arrived too.

**Next:** Phase 4, projections (above).

## Last task: transcripts (Phase 3, done)

**Goal:** the transcript's content (responses, models, token usage) in the ledger, raw and byte-for-byte, seconds after it's written. Decisions: [ADR 0020](adr/0020-transcripts.md). Claude Code only.

**Steps (test-first):**
1. ✅ Core (`TranscriptAggregate`): `TranscriptFileName` (`main`, `subagents/agent-<id>`, with `SubagentId`), content-derived `TranscriptLineId` and `TranscriptFileId` (SHA-256 of length-prefixed fields, UUID version 8; pinned by values computed independently), `TranscriptLine` and `TranscriptFile`.
2. ✅ Infrastructure: `transcript_lines` (`text`, line ID as primary key, `byte_offset`, indexed for reading in order) and `transcript_files` (raw subagent meta, filled in once); `ITranscriptStore` write store: one transaction, `unnest` insert with `ON CONFLICT DO NOTHING`, audit timestamps set explicitly.
3. ✅ Use cases: `IngestTranscriptBatch` (envelope validation, 1–1,000 lines with distinct offsets, meta only for subagents; stored and already-stored counts), `ListTranscriptFiles` (main first, line counts) and `ListTranscriptLines` (keyset on byte offset and line ID) over `ITranscriptQueryService`.
4. ✅ Web: `POST /transcripts/lines` (ingest key, gzip request decompression, 8 MB limit after decompression answered with 413). Reading back, revised to work like raw events: `GET /api/raw/transcripts` (filters, keyset paging newest first) and `GET /api/raw/transcripts/{lineId}`; a "Raw transcripts" section with list and line pages; `line_type` column (backfilled by migration); session links between events and transcript lines; the v0.4.0 per-session page redirects.
5. ✅ CLI: `ITranscriptSource` (Claude Code: `transcript_path` and every subagent file in the session's folder, revised after the rollout check found a subagent's last line written after its final hook); `TranscriptStateStore` (offset, fingerprint, lock file); `TranscriptReader` (complete lines, byte offsets, 1 MB per run, lines over 4 MB skipped and logged, restart on shrink or replacement); batches of 1,000 lines or 4 MB in a separate spool folder, sent gzipped, up to 3 per hook; `flush` and `status` cover them. Also fixed: the CLI wrote its spool into the current directory when `~/.local/share` didn't exist yet (`SpecialFolderOption.DoNotVerify`).
6. ✅ Rollout (2026-10-25): v0.4.0, then v0.4.1 with the revised read side, the subagent fix and the setup page fix. Verified against the files: this session's main transcript byte-for-byte (6,793 lines at the first check) and its three subagents' (21, 23 and 25 lines); the backfilled `line_type` of all 7,177 main-file lines matches the file.

**Next:** to be decided with the user. Phase 4 (projections) is next on the roadmap; Phase 2's remaining steps (user management, HTTPS, GitHub login) when needed.

## Done before: API keys (Phase 2, step 3)

API keys released in v0.3.0 ([ADR 0019](adr/0019-api-keys.md)); the dev container sends with its own ingest key, and new receipts record it. Remaining Phase 2 work, when needed: 2b (user management), HTTPS (step 4), GitHub login (step 5).

**Open question:** a URL that matches no page (e.g. `/raw/events/not-a-guid`) returns 404 with an empty body, because it never reaches Blazor. `UseStatusCodePagesWithReExecute("/not-found")` would fix it but also changes `/api` 404s; decide when convenient.

**Deferred from the EF mapping task:**
- An index on `context_git_repo` / `context_git_branch`. EF 10 can't declare an index on complex-type columns. Add it with `migrationBuilder.Sql` when a projection or query actually filters by repo or branch.
- `AgentEventReceiptBuilder` lives in the unit test project, while the integration tests use a small local factory. If a third test project needs one, move the builder into a shared test-support project.

## Later phases (outline)

- **Projections (Phase 4):** designed in [ADR 0022](adr/0022-projections.md) and [ADR 0023](adr/0023-interactive-work-pages.md); see "Next task". Duplicate receipts are deduplicated by `event_id` inside each session's rebuild ([ADR 0010](adr/0010-raw-landing-accepts-duplicates.md)), rather than in a separate `agent_events` table as first sketched here.
- **Known capture quirks the Claude Code adapter must handle** (details in the research doc; how each is handled is in ADR 0022):
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
- **Times in the viewer's time zone** (raised 2026-10-27, needs more thought): today every time is UTC. Sketch: `app.js` stores the browser's time zone (`Intl…resolvedOptions().timeZone`) in a cookie; the server renders times in that zone from the first byte, on every page through the shared `Timestamp` component, always labelled with the zone and with the UTC value on hover; date dividers and the Day lens (step 4) use the same zone. The API image has the zone data (Ubuntu 24.04). Open: whether raw pages, as evidence, should stay UTC; what the first visit or a browser without script shows.
- **Persisting sign-in keys** (decided against for now, 2026-10-27): ASP.NET Core's data-protection keys live inside the container, so every deployment signs everyone out and fails forms left open across it. Acceptable for now; if it becomes a nuisance, keep the keys in the database or a mounted volume.
- **Redaction** of secrets and sensitive data, on the client side before sending, and/or on the server.
- **Optional estimated cost** (decided against for Phase 4, 2026-10-26; [ADR 0022](adr/0022-projections.md)): an instance-wide setting, off by default, showing "estimated list-price cost" from a price table per model and effective date (token kinds, fast mode, service tier, long context), cross-checked against Claude Code's `cost-state` totals. Useful where agents are billed per token (automated pipelines, API keys, Bedrock, Vertex) and as the one way to compare tokens across models. Added by rebuilding projections; no data is lost by waiting.
- **Reading other attribution formats** as extra evidence: Entire checkpoints (`Entire-Checkpoint:` commit trailers), git-ai's git notes, the Agent Trace format, and Claude Code's OpenTelemetry (which records the commit on `git commit`).
- **Cost rollups** per session and per tag.
- **Automated review of agent sessions** against `architecture.md`, as an external consumer of the ledger:
  - Deterministic rules (dependency direction) as architecture tests in CI (NetArchTest/ArchUnitNET).
  - A fast structured-output model to run a fixed checklist over every session.
  - An LLM, via the MCP server, to explain only the flagged sessions.
- **Deployment** to a cloud environment (container-based), once the Delivery phase is complete.
