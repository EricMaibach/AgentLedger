# 0020. Transcripts: raw line tables, read incrementally by the CLI on every hook

- **Status:** Accepted
- **Date:** 2026-10-25

## Context

Hook payloads say what an agent *did*, but not most of what it *said* or what it *cost*. Assistant responses, reasoning (where present), the model and per-response token usage exist only in the transcript file the agent writes locally ([research](../research/agent-hook-capabilities.md)). Phase 3 brings that file into the ledger, raw, as Phase 1 did for hooks.

What real Claude Code transcripts look like (2026-10-25, structure only):
- one JSONL file per session (`~/.claude/projects/<project>/<session>.jsonl`), plus one per subagent (`<session>/subagents/agent-<id>.jsonl`, with a sibling `.meta.json`);
- large: a long session was 13 MB in about 6,000 lines; most lines are under 1 KB, the largest about 115 KB; gzip shrinks it about 4×;
- 13 line types; about a third of lines (the bookkeeping types) have no `uuid`;
- one model response is split over several lines, **each repeating the same token usage**; most "thinking" blocks hold only a signature, no text.

Hooks are installed with `async: true`, so the agent doesn't wait for them.

## Decision

**Storage: raw tables of their own**
- **`transcript_lines`:** one row per line: agent, native session ID, file, `subagent_id`, byte offset, the line's exact text, the API key that sent it ([ADR 0019](0019-api-keys.md)) and when it arrived. Hook events stay in `agent_event_receipts`: transcript lines are a file we read, not events the agent announced, and they outnumber hook events about 7 to 1.
- The text is stored as **`text`, not `json`**: we promise what's in the file byte for byte, valid JSON or not, so no line's content can ever cause a batch to be refused. Postgres compresses long values itself.
- **`transcript_files`:** one row per file in a session (`main`, or a subagent's), holding a subagent's `.meta.json` raw (its type, the tool call that launched it, its depth), stored once. It also lists a session's files without scanning lines.
- `file` is the session-relative file name (`main`, `subagents/agent-<id>`), not a path, which differs between machines. `subagent_id` is derived from it by the server.

**Identity: each line stored once**
- A line's ID is derived from agent, session, file, byte offset and a **SHA-256 of the line's bytes**, computed by the server. Anyone reading the same line gets the same ID.
- The ID is unique: a repeat is accepted and **not stored again**. Unlike hook events ([ADR 0010](0010-raw-landing-accepts-duplicates.md)), a repeat is provably byte-identical, so storing it adds nothing, and at transcript volumes it would multiply storage.
- Because the hash is part of the identity, a line that changed at the same offset (a rewritten file) becomes a different line, kept, rather than silently merged.

**Reading: the CLI catches up on every hook**
- Each hook run, after spooling its event, catches up the session's transcript (`transcript_path`) and, when named (`agent_transcript_path`), a subagent's: from the saved offset to the last **complete** line, at most about 1 MB per run.
- Per-file state (offset, a fingerprint of the file's start) lives next to the spool, with a lock: a second process finds it locked and skips; the next hook catches up.
- A file smaller than the saved offset, or with a different fingerprint, is read again from the start; identity makes the repeats harmless.
- Lines go into the **spool**, so an outage loses nothing. Opted-out projects ([ADR 0013](0013-hook-installation-and-opt-out.md)) aren't read.
- No backfill command: hook events can't be backfilled, so importing old transcripts would create sessions with transcripts but no events. A session already under way when the CLI first sees it is read from its beginning.

**Sending: gzipped batches**
- `POST /transcripts/lines` with an ingest key: one file's chunk per request (agent, session, file, optional subagent meta, lines with offset and text), up to about 1,000 lines or 4 MB decompressed; a single larger line goes alone.
- Requests are gzipped (`Content-Encoding: gzip`), decompressed by ASP.NET Core's request decompression with the size limit applied after decompression.
- The answer counts stored and already-stored lines. A batch's envelope can be refused (400); a line's content cannot.
- On the server, a batch is one insert that skips lines already stored.

**Reading them back**
- API (signed-in browser or read key, like `/api/raw/…`): `GET /api/raw/transcripts?session=…` lists a session's files; `GET /api/raw/transcripts/lines?session=…&file=…` pages through a file's lines in order (keyset paging, [ADR 0016](0016-read-api-and-keyset-paging.md)).
- UI: `/raw/sessions/{session}/transcript` shows the session's files and one file's lines in order (offset, line type, text, expandable to the exact text); session links in the event list lead to it. Still raw: rendering a conversation is Phase 4.

**Agent-neutral**
- The tables and server know nothing Claude-specific; meaning is interpreted per agent in projections ([ADR 0003](0003-agent-neutral-core-with-adapters.md)).
- The CLI has one small strategy per agent for where its transcripts are, how a session's files are named, and its side files. Claude Code only for now.
- Assumes append-only, line-based files (verified for Claude Code; Codex and Copilot are JSONL per the research). An agent that rewrites a single document would need its own design.

## Consequences

- The richest data (responses, usage, models) reaches the ledger seconds after it's written, through the same spool, keys and receipts as events.
- Two new tables and a second ingest endpoint; storage grows with transcripts (a long session is about 13 MB).
- Projections must count token usage once per response (`message.id`), not per line, and expect reasoning text to be mostly absent.
- Sessions from before the CLI read transcripts stay without them, as they are without events.
