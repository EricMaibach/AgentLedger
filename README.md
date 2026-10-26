# AgentLedger

[![CI](https://github.com/EricMaibach/AgentLedger/actions/workflows/ci.yml/badge.svg)](https://github.com/EricMaibach/AgentLedger/actions/workflows/ci.yml)

An auditable ledger of what AI coding agents do.

AgentLedger hooks into coding agents (Claude Code, Codex, GitHub Copilot, Snowflake Cortex Code) and records everything they expose: prompts, tool calls, responses, reasoning and token usage. Everything goes into a single PostgreSQL store, so that people and other AI can review, measure and correlate agent activity across tools.

> **Status:** early development. Capturing raw hook events from Claude Code works, and AgentLedger records its own development. Next: a web UI and authentication (Phase 2). See the [roadmap](docs/roadmap.md).

## How it works

```
agent hook ──stdin──► agentledger CLI ──HTTP──► API ──► PostgreSQL
```

- Agents run a small CLI from their hooks. It wraps each payload in an envelope, sends it to the API, and never blocks or breaks the agent.
- The API appends every event to an **append-only ledger of raw events**. Structured views (sessions, turns, tool calls, cost) are projections that can be rebuilt from the raw history.
- Agent-specific formats are handled by per-agent adapters, so the core model stays agent-neutral.

## Architecture

A .NET 10 solution using Clean Architecture and Domain-Driven Design, based on the [Ardalis Clean Architecture template](https://github.com/ardalis/CleanArchitecture).

| Project | Role |
|---|---|
| `AgentLedger.Core` | Domain model: aggregates, value objects, domain rules |
| `AgentLedger.UseCases` | Application commands and queries (Mediator) |
| `AgentLedger.Infrastructure` | EF Core + Npgsql persistence |
| `AgentLedger.Web` | HTTP API (FastEndpoints) |
| `AgentLedger.ServiceDefaults` | OpenTelemetry, health checks |

More detail:
- [Architecture and engineering approach](docs/architecture.md)
- [Architecture Decision Records](docs/adr/)
- [Agent hook research](docs/research/agent-hook-capabilities.md)

## Getting started

Requirements: Docker and VS Code with the Dev Containers extension.

1. Open the repository in VS Code and choose **Reopen in Container**. This starts a .NET 10 SDK container plus PostgreSQL 18. `psql` works with no arguments.
2. Build and test:
   ```bash
   dotnet tool restore  # local tools (dotnet-ef)
   dotnet build
   dotnet test        # functional tests start a throwaway Postgres container via Testcontainers
   ```
3. Run the API:
   ```bash
   dotnet run --project src/AgentLedger.Web --launch-profile http
   curl http://localhost:57678/status
   ```
   In Development, migrations are applied on startup. Open `http://localhost:57678/` in a browser: on a database with no users it asks you to create the admin account (within 10 minutes of starting the app). After signing in, the web UI is at `/raw/events`, API keys are under Settings, and API docs are at `/scalar`.
4. The dev container sends its own agent activity to the stable ledger (dogfooding). Give it a key by setting `AGENTLEDGER_API_KEY` on the machine running VS Code before opening the container (`devcontainer.json` passes it in), or run `agentledger login` inside the container (lost on rebuild).

## Running a ledger

`deploy/compose.yml` runs the released image with its own Postgres, for example as a Portainer stack. Its comments list the variables, the first-run setup, and how to reset a forgotten password. By default the ledger is reachable only from the machine it runs on; see [ADR 0018](docs/adr/0018-password-login.md) before exposing it on a network. After creating the admin, create an ingest key under Settings → API keys for each machine that sends events.

## Recording agent activity: the `agentledger` CLI

Agents run a small CLI from their hooks. It's published as a single Native AOT binary with no .NET runtime needed, and a hook run takes a few milliseconds.

```bash
dotnet publish src/AgentLedger.Cli -c Release -r linux-x64 -o ~/.local/bin   # a folder on your PATH
agentledger install claude-code          # hooks for every Claude Code event, in all your projects
agentledger login                        # store your ingest key (create it in the web UI under Settings → API keys)
agentledger status                       # configuration, API reachability, key state, waiting events, recent log
```

- **An ingest key is required** to send events ([ADR 0019](docs/adr/0019-api-keys.md)). Keys are personal: create your own in the web UI. `login` checks it with the ledger and saves it in your user config file; `AGENTLEDGER_API_KEY` works too. Without a valid key, events wait in the spool.
- **Transcripts too** ([ADR 0020](docs/adr/0020-transcripts.md)): each hook also sends the new lines of the session's transcript (and subagents'), read where it stopped last time. They wait in the spool like events, and `status` shows how many batches are waiting.
- **Git context** ([ADR 0021](docs/adr/0021-git-context.md)): each event records the checked-out commit, and when it changes, the new commits (message, changed files) are sent too, read with `git log`; so are tags as they're created, moved or deleted. Nothing to set up; history from before the CLI first saw a repository isn't sent.
- **`hook <agent>`** is what the installed hooks call. It's silent and always exits 0, so it never disrupts the agent. If the API is unavailable, events wait in a local spool and are sent later, or right away with `agentledger flush`.
- **Pointing at another API:** set `AGENTLEDGER_URL`, or add `{ "url": "..." }` to `.agentledger.json` in a project.
- **Opting a project out:** add `{ "enabled": false }` to that project's `.agentledger.json`.
- **Removing it:** run `agentledger uninstall claude-code` before deleting the binary.

Details: [ADR 0012](docs/adr/0012-cli-design.md), [ADR 0013](docs/adr/0013-hook-installation-and-opt-out.md).

## Contributing

Read [`docs/architecture.md`](docs/architecture.md) first. Before finishing a change, all of these must pass:
- `dotnet build`
- `dotnet test`
- `dotnet format --verify-no-changes`
