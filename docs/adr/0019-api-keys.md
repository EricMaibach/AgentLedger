# 0019. API keys: personal, scoped, hashed; ingest always requires one

- **Status:** Accepted (amended by [ADR 0024](0024-connecting-machines.md): keys can also be created by exchanging a one-time enrollment code)
- **Date:** 2026-10-23

## Context

After password login ([ADR 0018](0018-password-login.md)), `POST /events` is still open to anything that can reach the ledger, and the JSON read API is usable only from a signed-in browser. Machines running agents need a credential to send events, and scripts (later the MCP server, [ADR 0008](0008-read-only-mcp-server.md)) need one to read. The envelope's `host` and `user` are self-reported by the CLI, so the ledger has no trustworthy record of who sent an event.

## Decision

**Format and storage**
- A key looks like `agl_<scope>_<keyId>_<secret>`: a recognizable prefix (for secret scanners and people), the scope, the key's own ID (its GUID in base62: 22 characters, not secret, for a single lookup and display), and a secret of 32 random bytes (base62, 43 characters). About 77 characters in all.
- Only a **SHA-256 hash of the secret** is stored; comparison is constant-time. A slow password hash isn't needed: a 32-byte random secret can't be guessed, and ingest checks a key on every request.
- The key is shown **once**, when created.

**Scopes: exactly one per key**
- **ingest:** `POST /events` only. **read:** `GET /api/…` only. Something needing both gets two keys, so a leaked ingest key (present on every machine running agents) can't read the ledger, and a leaked read key can't plant events.
- No key can manage keys or users; pages never accept keys.
- Keys travel in `Authorization: Bearer …`, never in URLs.
- `/api` accepts a signed-in cookie or a read key. **`POST /events` always requires an ingest key**: there is no setting to open it.
- No, unknown, revoked or expired key → **401**; a valid key with the wrong scope → **403**.

**Ownership: keys are personal**
- Any signed-in user creates keys of either scope, and sees and revokes their own; admins see and can revoke everyone's. Management lives at `/settings/api-keys`.
- **Every event sent with a key is attributed to the key's owner, who is responsible for it.** Keys must not be shared; each person creates their own.
- When a user is disabled (Phase 2 step 2b), their keys stop working.

**Lifecycle**
- A key has a name, a scope, its creator and creation time, an optional expiry (none by default; 30 days, 90 days or 1 year), and a "last used" time updated at most once an hour.
- Revoking is immediate (no caching) and permanent; revoked keys stay listed with who revoked them and when. Keys and their scopes are never edited; rotating means creating a new key and revoking the old one.

**Attribution in the raw table**
- `agent_event_receipts` gets a nullable **`api_key_id`**: the key that delivered this receipt. It's a fact about the delivery, like `received_at` ([ADR 0010](0010-raw-landing-accepts-duplicates.md)). The user is found through the key; a key's owner never changes, so that link is stable and isn't copied into the raw table. Projections may copy it for per-user views.
- The event detail page shows who sent each receipt and with which key.

**CLI**
- **401 and 403 keep the event in the spool** (they mean a configuration problem, not a malformed event); `status` says why.
- `agentledger login [--key …]` checks a key against the ledger and stores it in the user config file (readable only by the user; prompts without echo when `--key` is omitted). The key still comes from `AGENTLEDGER_API_KEY` or the user config, never the project file ([ADR 0012](0012-cli-design.md)).
- A small endpoint tells a key's holder whether the key is valid and its scope, for `login` and `status`.
- The dev container passes `AGENTLEDGER_API_KEY` from the host (`remoteEnv` with `${localEnv:…}`), so the key survives rebuilds and is never committed.

## Consequences

- Ingest is closed to anyone without a key, and every receipt from now on carries an authenticated sender.
- One more table (`api_keys`) and one nullable column on the raw table.
- Upgrading requires the new CLI first (older CLIs drop events the server refuses) and a key on every machine; events sent before the key is configured wait in the spool.
- A shared, non-personal sender (e.g. CI) would need its own kind of key later; it is not covered by personal keys.
