# 0003. Agent-neutral core with per-agent adapters

- **Status:** Accepted
- **Date:** 2026-09-23

## Context

The agents differ in:
- event names;
- payload casing and dialect (Copilot alone has three);
- what identifiers exist (Codex has a turn ID, Copilot has none);
- how sessions behave (resume, clear, fork).

New agents will be added over time.

## Decision

- **Core is agent-neutral.** Its concepts (event, and later session, turn, tool call) are defined independently of any vendor's payload.
- Each agent gets an **adapter**, a DDD anti-corruption layer. It translates that agent's raw events into the neutral model when building projections.
- The raw ledger stores each agent's native event type and payload untouched. Interpretation happens only in adapters.
- `AgentKind` enumerates the supported agents. Adding an agent means a new `AgentKind` value plus a new adapter, with no changes to existing Core logic.

## Consequences

- Vendor format changes are contained within one adapter.
- Some neutral concepts will be missing for some agents (e.g. no turn ID from Copilot). The model has to allow for absent data rather than invent it.
- Captured real payloads (see `docs/research/agent-hook-capabilities.md`) should back each adapter's tests. Review captures before committing them as fixtures: they can contain prompts and other conversation text.
