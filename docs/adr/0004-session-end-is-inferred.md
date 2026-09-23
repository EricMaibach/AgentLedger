# 0004. Session end is inferred

- **Status:** Accepted
- **Date:** 2026-09-23

## Context

- VS Code's Copilot integration has no session-end event.
- Claude Code and Codex give `SessionEnd` hooks a budget of about 1–3 seconds.
- Nothing fires when an agent crashes or a machine sleeps.
- In captured Claude Code data, a resumed session reuses its ID, so one session can start and end several times.

## Decision

- A session's end is **inferred** (e.g. no events for an idle period), not taken from an end event.
- End events are recorded as data when present, not relied on for correctness.
- A session is modeled as having one or more activity periods, not a single start/end pair.

## Consequences

- Session boundaries are approximate, and depend on a configurable idle threshold.
- Duration and "active session" reporting must say that they are inferred.
