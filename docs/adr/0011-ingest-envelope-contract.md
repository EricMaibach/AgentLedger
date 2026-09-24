# 0011. Ingest envelope contract

- **Status:** Accepted
- **Date:** 2026-09-24

## Context

The `agentledger` CLI sends one HTTP request per hook invocation to `POST /events`. The CLI deliberately doesn't reference Core ([ADR 0002](0002-hook-ingestion-via-cli.md)), so this JSON contract is the only thing the two share, and it has to be pinned down before the CLI is built.

## Decision

`POST /events` with a JSON body (camelCase fields):

```json
{
  "eventId": "0199a1c2-7f3e-7d41-9c2b-5a1e0f3d8b77",
  "agent": "claude-code",
  "eventType": "PreToolUse",
  "nativeSessionId": "3829bce8-...",
  "capturedAt": "2026-09-24T12:00:00Z",
  "host": "devbox",
  "user": "eric",
  "projectDir": "/workspace",
  "gitRepo": "github.com/EricMaibach/AgentLedger",
  "gitBranch": "main",
  "gitWorktree": null,
  "tags": { "story": "123" },
  "payload": { "session_id": "3829bce8-...", "hook_event_name": "PreToolUse", "...": "..." }
}
```

- **`eventId`:** a client-generated UUIDv7, the same for every resend of the event.
- **`agent`:** **kebab-case** (`claude-code`, `codex`, `copilot-cli`, `vs-code-copilot`, `cortex-code`), the same string as the CLI argument in `agentledger hook claude-code`. The API maps it to the stored `AgentKind` name (`ClaudeCode`).
- **Required:** `eventId`, `agent`, `eventType`, `capturedAt`, `host`, `user`, `projectDir`, `payload`. Optional: `nativeSessionId`, the git fields, and `tags` (missing means none).
- **`payload`:** the hook's stdin, embedded as **raw JSON** (not a JSON-encoded string). The API stores its exact text, whitespace and key order included.

**Responses:**
- **201** `{ "receiptId": "..." }` for every accepted message, resends included ([ADR 0010](0010-raw-landing-accepts-duplicates.md)).
- **400** a validation problem, with errors keyed by request field (e.g. `{"errors":{"agent":["..."]}}`). The request is wrong, so the client should not retry it.
- **500** a server problem. The client keeps the event in its spool and retries.

## Consequences

- The raw-JSON payload keeps the envelope readable. The API must bind it as a `JsonElement` and store `GetRawText()`, never re-serialize it.
- Kebab-case keeps the CLI argument and the wire value identical. The mapping to `AgentKind` names lives in the Web layer (`AgentWireName`).
