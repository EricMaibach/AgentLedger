# 0002. Hook ingestion via a CLI

- **Status:** Accepted (duplicate handling amended by [ADR 0010](0010-raw-landing-accepts-duplicates.md): the client ID identifies duplicates for downstream deduplication; ingestion no longer deduplicates)
- **Date:** 2026-09-23

## Context

Every target agent supports **command hooks**: a shell command run on lifecycle events, with a JSON payload on stdin. Hooks run inside the agent's process tree, often with tight time limits (Claude Code and Codex give `SessionEnd` about 1–3 seconds). A hook that hangs or fails can disrupt the agent.

The richest data (assistant responses, reasoning, token usage) is not in hook payloads at all. It's in transcript files on the machine where the agent runs.

A shell script would be simplest, but would be hard to test, hard to make robust on Windows, and poor at reading transcripts incrementally.

## Decision

- Hooks call a small **`agentledger` CLI**, built as a Native AOT single binary: `agentledger hook <agent>`.
- The CLI:
  - reads the payload from stdin and wraps it, **unmodified**, in an envelope:
    - a client-generated UUIDv7 event ID;
    - agent, event type, capture timestamp;
    - host, user, project directory, git repo/branch/worktree;
    - `AGENTLEDGER_TAG_*` variables;
  - POSTs it to the API with a short timeout;
  - **always exits 0**, whatever happens;
  - **spools to a local file** when the API is unreachable, and resends on a later run. The client-generated ID makes resends idempotent.
- The CLI does **not** interpret payloads. All agent-specific logic lives server-side ([ADR 0003](0003-agent-neutral-core-with-adapters.md)).
- The API URL is configuration. The first one found wins: the `AGENTLEDGER_URL` env var, project config, user config, then a `localhost` default. An API key is required when the API isn't local.
- The CLI must run next to the agent (host, dev container or server), because it reads local transcript files.
- The CLI does not reference Core. It depends only on the HTTP contract.
- Reading transcripts incrementally is the CLI's job, and comes after raw capture (see the roadmap).

## Consequences

- An agent is never blocked or broken by logging.
- Behavior is testable and identical across platforms.
- A binary has to be distributed per platform (GitHub releases; later a dev container feature).
- Hook configuration files may be shared between tools (Copilot CLI, Cortex Code and VS Code can read Claude Code's settings), so the CLI must record which agent actually invoked it.
