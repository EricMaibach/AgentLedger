# 0001. Raw event ledger with projections

- **Status:** Accepted
- **Date:** 2026-09-23

## Context

AgentLedger captures activity from several coding agents (Claude Code, Codex, GitHub Copilot, Cortex Code). Each has its own hook payload formats, and the vendors say their transcript formats are not stable. Our understanding of what the payloads mean will keep improving: for example, we only found out by capturing real payloads that Claude Code's resume reuses the session ID, and that some `UserPromptSubmit` events are not typed by a human.

If we normalized events into a Session/Turn/ToolCall model at ingestion time, every mistake in that interpretation would be permanent.

## Decision

- The ledger is an **append-only table of raw events**. Each row is one captured hook payload, stored **unmodified** as `jsonb`, plus a small envelope of fields every agent has: event ID, agent, event type, native session ID, capture and receive timestamps, capture context, tags.
- Rows are never updated or deleted by the application.
- Structured views (sessions, turns, tool calls, token usage, cost) are **projections** derived from the raw events. They can be dropped and rebuilt from the full history at any time.
- Projections are added one at a time, when a real query needs them. The first milestone stores raw events only.

## Consequences

- Improved interpretation can be applied retroactively by rebuilding projections.
- Ingestion stays simple and agent-agnostic, so it rarely has reason to fail.
- Queries against raw events need `jsonb` knowledge and are agent-specific until projections exist.
- Storage grows with every event. Large content (transcripts) may need separate storage later.
