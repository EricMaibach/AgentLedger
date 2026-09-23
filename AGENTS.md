# AgentLedger

An auditable ledger of what AI coding agents do. It's a .NET 10 solution using Clean Architecture and DDD, with PostgreSQL.

## Read first
- **`docs/architecture.md`**: how code is written here (layers, patterns, testing, conventions). Follow it. If a task seems to need something that contradicts it, raise it rather than working around it.
- **`docs/roadmap.md`**: what's done and the **next task**. Update its status when work lands.
- `docs/adr/`: why things are the way they are. Add an ADR for any significant new decision.
- `docs/research/agent-hook-capabilities.md`: what each agent's hooks provide.

## Working rules
- Discuss the design with the user before adding a project, table, public API or new pattern.
- Write tests first, and see them fail for the right reason before implementing.
- Finish with `dotnet build`, `dotnet test` and `dotnet format --verify-no-changes` all passing.
- Don't commit unless asked.

## Environment
- VS Code dev container (`.devcontainer/`) with Postgres 18. `psql` with no arguments connects. The connection string comes from `ConnectionStrings__AgentLedger`.
- Tests use Testcontainers (Docker is available inside the dev container).
- `tools/probe/capture.sh` is a throwaway hook that dumps payloads to `.probe/` (gitignored). Captures contain conversation text: review them before using any as test fixtures.
