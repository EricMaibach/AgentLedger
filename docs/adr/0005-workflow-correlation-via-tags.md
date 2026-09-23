# 0005. Workflow correlation via opaque tags

- **Status:** Accepted
- **Date:** 2026-09-23

## Context

Agents are often launched by an external workflow tool (for example, one that moves user stories through a process). It's valuable to link agent activity back to the story or task that caused it, but AgentLedger should not depend on any particular workflow system.

## Decision

- AgentLedger knows nothing about stories, tasks or workflow states.
- Events carry optional key/value **tags** that AgentLedger stores and indexes but never interprets.
- Tags come from **environment variables** set when the agent is launched: `AGENTLEDGER_TAG_<key>=<value>`, e.g. `AGENTLEDGER_TAG_story=123`. Environment variables are per process, so concurrent agents on one machine keep separate tags.
- Where an agent supports it (Claude Code and Copilot CLI via `--session-id`), the launcher may also pre-assign the session ID.
- Git context (repo, branch, worktree) is always recorded. A worktree per task makes it a second, independent correlation key.
- There is no API for attaching tags after the fact until a real need appears.

## Consequences

- Any workflow tool can correlate by setting environment variables. There's nothing to integrate.
- Agents started by hand or in an IDE are logged without tags. That's accepted.
