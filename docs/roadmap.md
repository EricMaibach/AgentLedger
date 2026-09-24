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

1. **Capture (current).** Every hook event from Claude Code lands in Postgres as a raw event.
2. **Transcripts. Critical, and immediately after capture.** The CLI reads agent transcripts incrementally, capturing assistant responses, reasoning and per-message token usage, which hooks don't expose.
3. **Projections and reading.** Sessions, turns and tool calls derived from raw events. A query API and the read-only MCP server ([ADR 0008](adr/0008-read-only-mcp-server.md)).
4. **Delivery.** CI (GitHub Actions: build, test, format check, architecture tests) and a container image.
5. **More agents.** Capture real payloads, then an adapter per agent: Codex, Copilot CLI, VS Code Copilot, Cortex Code.

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
- [ ] **`agentledger` CLI: `hook <agent>` with envelope, timeout, spool, always exit 0** ← next task
- [ ] Claude Code hooks registered through the CLI; a real session captured end to end

**Phase 1 is done when:**
- every hook event from a real Claude Code session is stored in Postgres, with its payload intact;
- a resend produces an extra receipt, and each agent event is still identifiable once by `event_id` (deduplication itself comes with projections).

## Next task: the `agentledger` CLI

**Goal:** hooks call `agentledger hook <agent>`. The CLI reads the payload from stdin, wraps it in the envelope ([ADR 0011](adr/0011-ingest-envelope-contract.md)) and POSTs it, and it never disrupts the agent ([ADR 0002](adr/0002-hook-ingestion-via-cli.md)).

This adds a new project, so the design is discussed first. Topics:
- project layout (`src/AgentLedger.Cli` plus a test project) and Native AOT constraints;
- `System.CommandLine`;
- spool location and format, and when resends happen;
- config file format and locations;
- how git context is collected.

**Requirements already decided:**
- **Always exits 0,** whatever happens, with a short HTTP timeout.
- **Building the envelope:**
  - a new UUIDv7 `eventId` per invocation;
  - `capturedAt` is the current time;
  - host, user and project dir (`CLAUDE_PROJECT_DIR` for Claude Code);
  - git repo, branch and worktree;
  - `AGENTLEDGER_TAG_*` → `tags`, with keys lowercased (`AGENTLEDGER_TAG_Story` becomes `story`), because environment variable names are case-sensitive on Linux but not on Windows;
  - stdin embedded as the raw-JSON `payload`.
- **Retry behavior:** on 500 or a network failure, write the envelope to the spool and resend it on a later run. On 400, log the error and drop the event: it's malformed, and resending won't help.
- **Config resolution:** `AGENTLEDGER_URL`, then project config, then user config, then the localhost default.
- No reference to Core.

**Afterwards (rest of Phase 1):**
- **End to end:** register the CLI in `.claude/settings.json` for every Claude Code hook event, run a real session, and verify every event against the probe captures.

**Deferred from the EF mapping task:**
- An index on `context_git_repo` / `context_git_branch`. EF 10 can't declare an index on complex-type columns. Add it with `migrationBuilder.Sql` when a projection or query actually filters by repo or branch.
- `AgentEventReceiptBuilder` lives in the unit test project, while the integration tests use a small local factory. If a third test project needs one, move the builder into a shared test-support project.

## Later phases (outline)

- **Transcripts (Phase 2):**
  - The CLI tracks a read offset per `transcript_path` and sends new transcript lines as events.
  - Subagent transcripts too (`agent_transcript_path` from `SubagentStop`).
  - Token usage and model per assistant message come from here.
- **Projections (Phase 3):**
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

## Ideas (not planned)

- **Hash-chaining** the raw event table for tamper evidence (possible because payloads are stored byte-for-byte; [ADR 0009](adr/0009-ledger-storage-details.md)).
- **Redaction** of secrets and sensitive data, on the client side before sending, and/or on the server.
- **Cost rollups** per session and per tag.
- **Automated review of agent sessions** against `architecture.md`, as an external consumer of the ledger:
  - Deterministic rules (dependency direction) as architecture tests in CI (NetArchTest/ArchUnitNET).
  - A fast structured-output model to run a fixed checklist over every session.
  - An LLM, via the MCP server, to explain only the flagged sessions.
- **Deployment** to a cloud environment (container-based), once Phase 4 exists.
