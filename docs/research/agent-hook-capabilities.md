# Agent Hook Capabilities — Research (2026-09-23)

What each agent exposes for capturing activity and identifying session/agent boundaries. Compiled from official docs (and Codex source) by research agents; **not yet verified empirically except where marked ✔︎ verified**. Claude Code was verified with a capture probe on 2026-09-23; see "Claude Code: captured payloads". Vendors change these fast — re-check before building each adapter, and capture real payloads first (see "Capture status").

Legend: ✅ supported · ⚠️ partial/caveat · ❌ not available · ? undocumented

## Summary matrix

| | Claude Code | Codex | Copilot CLI | VS Code Copilot ("Local") | Cortex Code (CLI) |
|---|---|---|---|---|---|
| Session id in hook stdin | ✅ `session_id` | ✅ `session_id` (root thread id) | ✅ `sessionId` / `session_id` | ⚠️ optional | ✅ |
| Set session id at launch | ✅ `--session-id <uuid>` ✔︎ verified | ❌ | ✅ `--session-id <uuid>` | ❌ | ⚠️ SDK `sessionId` only |
| Launch env vars reach hooks | ✅ | ✅ (snapshot at session start) | ✅ likely | ⚠️ IDE process env | ? likely |
| SessionStart + source | ✅ startup/resume/clear/compact ✔︎ verified; fork unverified | ✅ same (+fork) | ✅ startup/resume/new | ⚠️ first prompt, `source:"new"` only | ✅ event; source ? |
| SessionEnd | ✅ reason; 1.5 s total budget | ⚠️ root only, 1–3 s timeout, `reason` always `other` | ✅ complete/error/abort/timeout/user_exit | ❌ none | ✅ reason ? |
| Subagent start/stop w/ id | ✅ both, `agent_id`/`agent_type` ✔︎ verified; internal helpers fire Stop only | ✅ both | ⚠️ start: name only; stop: `agentId` + full response | ✅ both | ⚠️ SubagentStop only (SDK has both) |
| Subagent id on tool events | ✅ ✔︎ verified | ✅ | ❌ | ❌ | ⚠️ SDK optional |
| Turn id | ✅ `prompt_id` ✔︎ verified | ✅ `turn_id` | ❌ (OTel `github.copilot.turn_id`) | ❌ | ❌ |
| Tool-call id (Pre↔Post) | ✅ `tool_use_id` ✔︎ verified | ✅ `tool_use_id` | ❌ | ✅ | ⚠️ SDK only |
| Transcript file | ✅ JSONL, per-message `usage`, `model`, `requestId` ✔︎ verified | ✅ rollout JSONL incl. `token_usage_record` | ⚠️ `~/.copilot/session-state/<id>/events.jsonl`, schema ? | ⚠️ "not a stable hook API" | ⚠️ `~/.snowflake/cortex/conversations/`, schema ? |
| OpenTelemetry | ✅ metrics/logs incl. cost USD; traces beta | ✅ `[otel]`, `turn.cost_microusd` | ✅ rich GenAI spans, content capture | ✅ settings `github.copilot.chat.otel.*` | ❌ local; spans go to Snowflake event table |
| Async/non-blocking hooks | ✅ `async: true` | ✅ `async: true` (max 8) | ⚠️ timeouts fail-open | — | ❌ synchronous only |
| Fork lineage | ⚠️ `source:"fork"` only, no parent id | ✅ `forked_from_id` in rollout | ? | ? | ? |

## Cross-cutting findings (design drivers)

1. **Shared hook files.** Copilot CLI and Cortex Code read Claude Code's `.claude/settings.json` automatically, and VS Code does too when `chat.useClaudeHooks` is on. One hook config can fire under several tools, so the CLI must detect which tool invoked it. Possible signals:
   - env vars: `CLAUDECODE=1`, `CLAUDE_PROJECT_DIR`, `CORTEX_PROJECT_DIR`
   - where `transcript_path` points
   - the payload's field casing and dialect
2. **Several payload dialects.**
   - Copilot has three: VS Code Local, CLI camelCase (timestamp in epoch ms), and CLI PascalCase (snake_case fields, Claude tool names).
   - Tool names differ per agent. Codex uses `apply_patch` and `spawn_agent`; Cortex Code uses lowercase names such as `bash`.
3. **Session end is unreliable.**
   - VS Code has no SessionEnd event.
   - Claude Code and Codex give SessionEnd hooks a budget of about 1–3 seconds.
   - Nothing fires on a crash.
   - So end-of-session must be *inferred*, e.g. after an idle timeout, and the CLI must write to its local spool first rather than calling the API synchronously.
4. **Transcripts are the richest and least stable source.** Assistant responses, reasoning and token usage mostly exist only in transcripts, and every vendor says the format may change. Store the raw data and reparse it; use OTel as the second channel for cost.
5. **Deployment quirks.**
   - **Codex:** user and project hooks must be trusted in `/hooks`. Trust is tracked by a hash of the hook command, so any command change needs re-trusting. Managed hooks are pre-trusted.
   - **Copilot cloud agent:** reads only `.github/hooks/*.json` from the default branch. It runs in a firewalled sandbox where files are discarded, so only `http` hooks can get data out.
   - **Copilot CLI `-p` mode:** repo hooks don't load unless the folder is trusted.
   - **Cortex Code:** credit usage lives in `SNOWFLAKE.ACCOUNT_USAGE.SNOWFLAKE_COCO_USAGE_HISTORY`, which has no session id; it can only be joined via `REQUEST_ID`, which hooks don't expose. `--private` mode disables history.

## Per-agent notes

### Claude Code (checked against CLI v2.1.280)
- **Events:** SessionStart/End, UserPromptSubmit, Pre/PostToolUse, PostToolUseFailure, PermissionRequest, Stop, SubagentStart/Stop, PreCompact/PostCompact, Notification, plus others.
- **Common fields:** `session_id`, `transcript_path`, `cwd`, `hook_event_name`, `prompt_id` ✔︎ verified (`permission_mode` appears on tool events only). Inside a subagent, `agent_id` and `agent_type` are added.
- **SubagentStop** carries `agent_transcript_path`, which points to a separate JSONL file per subagent.
- **Session id behaviour:**
  - `/clear` creates a new id ✔︎ verified. Fork (`--fork-session`, `/branch`) is documented to create one too (unverified).
  - Resume and compact keep the same id ✔︎ verified. `--continue` is documented to keep it (unverified).
- **Each assistant line in the transcript carries** ✔︎ verified:
  - `usage` (input, output, cache create/read, thinking tokens)
  - `model`, `requestId`
  - `uuid`/`parentUuid`, `sessionId`
  - `cwd`, `gitBranch`, `version`
- **Hook environment:** `CLAUDE_PROJECT_DIR` and `CLAUDE_ENV_FILE` are set; env vars from launch are inherited.
- **Exit codes:** exit 2 blocks. A logger must always exit 0, and should use `async: true` where possible.
- **OTel:** enabled with `CLAUDE_CODE_ENABLE_TELEMETRY=1`. Exports `claude_code.token.usage` and `claude_code.cost.usage`. Content logging is opt-in via the `OTEL_LOG_*` settings.
- **Docs:** https://code.claude.com/docs/en/hooks · https://code.claude.com/docs/en/monitoring-usage · https://code.claude.com/docs/en/sessions

### Codex (source `openai/codex` main, 2026-09-23)
- **Hook engine:** Claude-compatible; the class is literally `ClaudeHooksEngine`. Hooks are stable and on by default.
- **Config:** `~/.codex/hooks.json`, `.codex/hooks.json`, `[hooks]` in `config.toml`, or plugins.
- **Events:** SessionStart/End, UserPromptSubmit, Pre/PostToolUse, PermissionRequest, Pre/PostCompact, SubagentStart/Stop, Stop, Interrupt.
- **Common fields:** `session_id`, `transcript_path`, `cwd`, `hook_event_name`, `model`, `permission_mode`, `turn_id`, and optionally `agent_id`/`agent_type`.
- **IDs form a tree:**
  - `session_id` is the root thread id.
  - `agent_id` is the child thread id.
  - OTel `conversation.id` is the per-thread id, not the root.
- **Session id behaviour:** resume keeps the id; fork creates a new id with `forked_from_id`.
- **Rollouts:** `~/.codex/sessions/YYYY/MM/DD/rollout-<ts>-<threadId>.jsonl`.
- **Hook environment:** hooks get a snapshot of the Codex process env taken at session start. `CODEX_SESSION_ID`/`CODEX_THREAD_ID` go to the model's shell commands, not to hooks.
- **Legacy `notify`:** fire-and-forget; the JSON is passed as the last argv argument. Deprecated.
- **Hooks in the IDE extension/app:** earlier issues (#17930, #18090) said they didn't run there; current source suggests they probably do now. Unverified.
- **Docs:** https://developers.openai.com/codex/hooks · https://developers.openai.com/codex/config-advanced · schemas: https://github.com/openai/codex/tree/main/codex-rs/hooks/schema/generated

### GitHub Copilot
- **VS Code has several "harnesses", each with its own hooks:**
  - Local (the extension host)
  - Copilot (Agent Host; the same implementation as Copilot CLI)
  - Claude (the real Claude Agent SDK)
  - Codex
  - Cloud
- **Local harness:**
  - Config: `.github/hooks/*.json`, `~/.copilot/hooks/`, or `.claude/settings*.json` (needs `chat.useClaudeHooks`).
  - Events: SessionStart, UserPromptSubmit, Pre/PostToolUse, PreCompact, SubagentStart/Stop, Stop.
  - There is no SessionEnd.
- **CLI:**
  - Config: policy dirs, `.github/hooks/*.json`, `~/.copilot/hooks/`, `.github/copilot/settings.json`, `.claude/settings*.json`, or plugins.
  - Events: session start/end, prompt, tool pre/post/failure, agentStop (fires per turn), subagent start/stop, errorOccurred, preCompact, permissionRequest, notification.
  - Also has `type:"http"` hooks.
- **OTel:** `COPILOT_OTEL_ENABLED` (CLI) or `github.copilot.chat.otel.*` (VS Code). Span tree is `invoke_agent` → `chat` → `execute_tool`, with the `gen_ai.*` usage attributes, and message content is optional. OTel is the best way to attribute tool calls to subagents.
- **Docs:** https://code.visualstudio.com/docs/agents/reference/hooks-reference · https://docs.github.com/en/copilot/reference/hooks-reference · https://code.visualstudio.com/docs/agents/guides/monitoring-agents

### Snowflake Cortex Code ("CoCo")
- **Hooks follow the Claude Code schema.**
  - Config: `.claude/` or `.cortex/` `settings*.json`, `~/.claude/settings.json`, or `~/.snowflake/cortex/hooks.json`.
  - Tool ids are lowercase.
- **Events (11):** Pre/PostToolUse, PermissionRequest, UserPromptSubmit, SessionStart/End, PreCompact, Stop, SubagentStop, Notification, Setup.
  - The CLI has no SubagentStart.
- **Payloads:** the CLI's shell-hook payload is only documented by a single PreToolUse sample. The SDK docs are more complete.
- **Execution:** hooks are synchronous (default timeout 60 s). Exit 2 blocks.
- **Hook environment:** `CORTEX_PROJECT_DIR`, `CORTEX_ENV_FILE`, `CORTEX_CODE_REMOTE`.
- **Snowflake-side data:**
  - Spans go to `SNOWFLAKE.LOCAL.AI_OBSERVABILITY_EVENTS`.
  - Credits are in `SNOWFLAKE_COCO_USAGE_HISTORY`.
  - SQL run by the agent appears in `QUERY_HISTORY`, tagged via `COCO_ADDITIONAL_QUERY_TAGS`.
- **Docs:** https://docs.snowflake.com/en/user-guide/cortex-code/extensibility · https://docs.snowflake.com/en/user-guide/cortex-code-agent-sdk/hooks · https://docs.snowflake.com/en/user-guide/cortex-code/observability

## Claude Code: captured payloads (2026-09-23, CLI v2.1.280)

Captured with `tools/probe/capture.sh`, registered for every event in `.claude/settings.local.json`. The registered event name always matched `hook_event_name` (`mismatches.tsv` was never written).

**Triggered and captured:** SessionStart, SessionEnd, UserPromptSubmit, Pre/PostToolUse, PostToolUseFailure, PostToolBatch, Stop, SubagentStart/Stop, Pre/PostCompact, Pre/PostModelSwitch, InstructionsLoaded, CwdChanged, ConfigChange, Notification.
**Not triggered:** PermissionRequest/PermissionDenied (the session ran in auto mode), UserPromptExpansion, StopFailure, Task*, TeammateIdle, Elicitation, and fork.

### Fields
- **Every payload:** `session_id`, `transcript_path`, `cwd`, `scratchpad_dir`, `hook_event_name`. Almost every payload also has `prompt_id`.
- **Tool events:** add `permission_mode` and `effort`.
- **Subagent tool events:** add `agent_id` and `agent_type`. `session_id` stays the parent's.

### Identity and grouping
- **Group events by `prompt_id`.** It links every event in a turn, including compact and model-switch sequences, into Session → Turn → Event.
- **`cwd` is not a stable project key.** A `cd` in Bash fires `CwdChanged {old_cwd, new_cwd}`, and later payloads carry the new `cwd`. Use `CLAUDE_PROJECT_DIR` or `transcript_path` to identify the project.
- **A session can start and end many times.** Resume fires `SessionEnd reason=resume`, then `SessionStart source=resume` with the **same** id. Model a session as a series of activity periods, not one start/end pair.
- **A SessionStart may be followed by nothing.** One session had a SessionStart and then no events, no SessionEnd and no transcript. A new session started 3 s later with `CLAUDE_CODE_TUI_JUST_SWITCHED=fullscreen` in its env. The likely cause (unconfirmed) is a TUI restart. Treat a session with no events as noise.
- **Hooks added partway through a session:** its events start with no SessionStart.

### Lifecycle sequences
- **`/clear`:** `SessionEnd reason=clear`, then `SessionStart source=clear` with a **new** id. The hook payloads contain nothing linking the old and new sessions.
- **`/compact`**, all sharing one `prompt_id`:
  1. `PreCompact {trigger:"manual", custom_instructions}`
  2. `SubagentStop`, with `agent_type:""` and no SubagentStart. This is the summarizer, which runs as a hidden subagent.
  3. `SessionStart {source:"compact", model}`, same id.
  4. `PostCompact {trigger, compact_summary}`. The summary is the full summary text, about 8 KB.
- **`/compact` on an empty session** fires PreCompact only: no PostCompact and no SessionStart.
- **`InstructionsLoaded` is deferred.**
  - It fires at the next prompt, not when the session starts.
  - Fields: `file_path`, `memory_type`, `load_reason` (`compact`, `include`, …) and `parent_file_path`. For example, AGENTS.md is loaded via `@AGENTS.md` in CLAUDE.md.
- **Resume SessionStart** adds `seconds_since_last_response`, `context_tokens`, `prompt_cache_likely_expired` and `estimated_cache_write_usd`.

### Tools
- **A failed tool call fires `PostToolUseFailure` instead of `PostToolUse`.**
  - Fields: `tool_name`, `tool_input`, `tool_use_id`, `error` (a string), `is_interrupt` and `duration_ms`. There is no `tool_response`.
  - A missing file in Read and a non-zero exit in Bash both count as failures. For Bash, the error reads `"Exit code 3\n<stderr>"`.
- **`PostToolBatch` repeats the `tool_response` from each PostToolUse**, so store it once.

### Subagents
- **A normal subagent** produces events in this order:
  1. The parent's `PreToolUse(Agent)`.
  2. `SubagentStart {agent_id, agent_type}`.
  3. The subagent's own tool events.
  4. `SubagentStop`.
- **`SubagentStop` fields:** `agent_transcript_path` (`<session>/subagents/agent-<id>.jsonl`, with a sibling `.meta.json` holding `agentType`, `toolUseId`, `spawnDepth` and `requestShape`) and `last_assistant_message`.
- **Background agents:** the parent's `PostToolUse(Agent)` fires as soon as the agent launches. The agent's real end is `SubagentStop`.
- **Internal helpers:** a `SubagentStop` with `agent_type:""` and no SubagentStart is one of Claude Code's own helpers, not an agent the user launched. Two were seen:
  - the compaction summarizer;
  - the prompt-suggestion helper, whose `last_assistant_message` was a predicted user reply.

  Tag these as internal.

### Prompts not typed by the user
Some `UserPromptSubmit` events are not typed by the user:
- a subagent's result (`<agent-message …>`);
- background-task notices (`<task-notification>`).

These reuse the current turn's `prompt_id`. The only way found to tell them apart is the prompt text.

### Model switches
- **`/model`** fires `PreModelSwitch` then `PostModelSwitch`. Both carry the same fields:
  - `from_model`, `to_model`, `requested_model`, `source:"picker"`
  - `context_tokens`, `prompt_cache_warm`, `cache_ttl`, `estimated_cache_write_usd`, `pricing`
- **`requested_model` can differ from `to_model`.** A request for `claude-fable-5-1[1m]` gave `claude-fable-5-1`. Record `to_model`.
- **The model is per turn, not per session.**

### Other events
- `Notification {notification_type:"idle_prompt", message}`.
- `ConfigChange {source:"local_settings", file_path}`.

### Sensitive content
These fields contain conversation text:
- `UserPromptSubmit.prompt`
- `Stop.last_assistant_message` and `SubagentStop.last_assistant_message`
- `PostCompact.compact_summary`
- `tool_input` and `tool_response`

The probe's `.env` capture redacted the secret-like vars it found (e.g. `CLAUDE_CODE_MESSAGING_TOKEN`).

## Capture status

Payloads are captured with a probe hook (`tools/probe/capture.sh`) that dumps stdin and a redacted environment for every event.

- **Claude Code:** captured (see "Claude Code: captured payloads" above). Not yet triggered: permission request/denied, fork, `UserPromptExpansion`.
- **Codex, Copilot, Cortex Code:** not yet captured. Capture each before writing its adapter.
- Sanitized captures will become golden-file test fixtures for the adapters. Raw captures contain conversation text and are not committed.
