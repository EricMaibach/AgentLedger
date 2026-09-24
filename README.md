# AgentLedger

[![CI](https://github.com/EricMaibach/AgentLedger/actions/workflows/ci.yml/badge.svg)](https://github.com/EricMaibach/AgentLedger/actions/workflows/ci.yml)

An auditable ledger of what AI coding agents do.

AgentLedger hooks into coding agents (Claude Code, Codex, GitHub Copilot, Snowflake Cortex Code) and records everything they expose: prompts, tool calls, responses, reasoning and token usage. Everything goes into a single PostgreSQL store, so that people and other AI can review, measure and correlate agent activity across tools.

> **Status:** early development. Phase 1 (capturing raw hook events from Claude Code) is in progress. See the [roadmap](docs/roadmap.md).

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
   In Development, migrations are applied on startup, and API docs are at `http://localhost:57678/scalar`.

## Contributing

Read [`docs/architecture.md`](docs/architecture.md) first. Before finishing a change, all of these must pass:
- `dotnet build`
- `dotnet test`
- `dotnet format --verify-no-changes`
