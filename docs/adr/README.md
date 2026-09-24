# Architecture Decision Records

Each file records one significant decision: the context, what was decided, and the consequences. ADRs are not edited after they are accepted. When a decision changes, add a new ADR that supersedes the old one and update the old one's status.

| # | Decision | Status |
|---|---|---|
| [0001](0001-raw-event-ledger-with-projections.md) | Store raw events in an append-only ledger; derive everything else as projections | Accepted |
| [0002](0002-hook-ingestion-via-cli.md) | Ingest through agent hooks that call a small CLI | Accepted |
| [0003](0003-agent-neutral-core-with-adapters.md) | Agent-neutral core; per-agent adapters interpret payloads | Accepted |
| [0004](0004-session-end-is-inferred.md) | Infer session end rather than relying on an end event | Accepted |
| [0005](0005-workflow-correlation-via-tags.md) | Correlate with external workflows through opaque tags from the environment | Accepted |
| [0006](0006-postgresql.md) | PostgreSQL for storage | Accepted |
| [0007](0007-clean-architecture-template-without-aspire.md) | Start from the Ardalis Clean Architecture template, without the Aspire AppHost | Accepted |
| [0008](0008-read-only-mcp-server.md) | Expose the ledger to AI through a read-only MCP server | Accepted |
| [0009](0009-ledger-storage-details.md) | Ledger storage: exact `json` payloads, snake_case names, `created_at`/`updated_at` watermarks | Accepted |

New ADRs: copy the structure of an existing one (Status, Context, Decision, Consequences) and use the next number.
