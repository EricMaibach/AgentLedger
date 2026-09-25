# 0012. `agentledger` CLI design

- **Status:** Accepted (install scope, binary reference and per-project opt-out detailed in [ADR 0013](0013-hook-installation-and-opt-out.md))
- **Date:** 2026-09-25

## Context

[ADR 0002](0002-hook-ingestion-via-cli.md) decided that agent hooks call a small CLI, and [ADR 0011](0011-ingest-envelope-contract.md) fixed the envelope it sends. This ADR records how the CLI itself is built and behaves. It runs on **every** hook event (dozens of times per agent turn), in environments we don't control, and must never disrupt the agent.

## Decision

### Build and structure
- **Native AOT**, published as one self-contained executable per OS/CPU. It starts in roughly 10 ms (versus roughly 100 ms for JIT) and needs no .NET runtime where agents run.
- **Projects:**
  - `src/AgentLedger.Cli` is one project, not Clean Architecture layers, because it's a small tool with no domain model. Inside, the usual principles apply: small sealed classes behind interfaces, composition, tests first.
  - `tests/AgentLedger.Cli.Tests` holds its tests.
  - The CLI does **not** reference Core; the HTTP contract is its only coupling.
- **Libraries:**
  - `System.CommandLine` for commands;
  - `System.Text.Json` with **source generation**, because reflection-based serialization isn't available under AOT;
  - `HttpClient`.

  Keep dependencies minimal and AOT-compatible.
- Publishing AOT on Linux needs `clang` and `zlib1g-dev`, which the dev container installs. Other platforms are built by CI jobs on those platforms later (AOT can't cross-compile).

### Commands
| Command | For | Behavior |
|---|---|---|
| `hook <agent>` | agent hooks | Builds and sends one envelope. **Writes nothing to stdout or stderr and always exits 0.** Agents treat hook output and exit codes as instructions: Claude Code adds `SessionStart` / `UserPromptSubmit` stdout to the agent's context, and exit code 2 blocks the action. Diagnostics go to a log file. |
| `status` | people | Shows the resolved configuration and where each value came from, whether the API is reachable (`GET /status`), the spool size, and recent errors from the log. It's the first thing to run when events seem to be missing, since `hook` fails silently by design. |
| `flush` | people | Sends everything waiting in the spool now and reports the results. |
| `install <agent>` / `uninstall <agent>` | people | Adds (or removes) a `hook <agent>` entry for **every** hook event in the agent's settings (`.claude/settings.json`, or `~/.claude/settings.json` with `--user`). It merges rather than overwrites, and is idempotent. Claude Code first; other agents with their adapters. |

`<agent>` is the kebab-case wire name (`claude-code`, `codex`, `copilot-cli`, `vs-code-copilot`, `cortex-code`).

### Spool
- **On disk, never in memory:** each hook invocation is a separate short-lived process.
- **Location:** the OS local app-data folder (`~/.local/share/agentledger/spool` on Linux, `~/Library/Application Support/agentledger/spool` on macOS, `%LOCALAPPDATA%\agentledger\spool` on Windows). `AGENTLEDGER_SPOOL_DIR` overrides it.
  - In dev containers, the home folder is lost on rebuild unless it's on a volume. Events spooled at the moment of a rebuild are lost. That's accepted.
- **Format:** one file per event, `<eventId>.json`, containing the full envelope. Each file is written under a temporary name and then renamed, which is atomic, so parallel hook processes never need locks and never see half-written files.
- **Write first, then send.** `hook` always writes the event to the spool before POSTing, so an event survives even if the agent kills the hook mid-request (Claude Code gives `SessionEnd` about 1.5 s):
  - **201:** delete the file.
  - **400:** delete the file and log the error. The event is malformed, and resending won't help.
  - **500, timeout or network failure:** keep the file.
  - Then briefly send a few older spooled files, and exit.
- **Concurrent resends are harmless:** duplicates land as extra receipts and are deduplicated downstream ([ADR 0010](0010-raw-landing-accepts-duplicates.md)), so no coordination is needed.
- **Safeguards:**
  - folder and files readable only by the user (700/600), since they contain prompts and tool output;
  - a size cap (e.g. 10,000 files or 100 MB), dropping the oldest and logging it.

### Configuration
- **Settings:**
  - API URL: default `http://localhost:57678`;
  - API key: none by default;
  - send timeout: 1 s;
  - spool folder.
- **Resolved per setting, first one found wins:**
  1. environment variable (`AGENTLEDGER_URL`, `AGENTLEDGER_API_KEY`, `AGENTLEDGER_TIMEOUT_MS`, `AGENTLEDGER_SPOOL_DIR`);
  2. project file `.agentledger.json`, found by walking up from the project directory;
  3. user file `~/.config/agentledger/config.json` (`%APPDATA%\agentledger\config.json` on Windows);
  4. built-in default.
- Files are JSON.
- **The API key is never read from the project file,** which is committed to git. It's ignored there, and `status` warns about it.

### Envelope fields
| Field | Source |
|---|---|
| `eventId` | a new UUIDv7 per invocation |
| `agent` | the command argument |
| `capturedAt` | the clock, at start |
| `payload` | stdin, unmodified |
| `tags` | `AGENTLEDGER_TAG_*`, keys lowercased, empty values skipped |
| `projectDir` | `CLAUDE_PROJECT_DIR` for Claude Code, otherwise the current directory (not the payload's `cwd`, which changes with `cd`) |
| `host` | `AGENTLEDGER_HOST`, otherwise the machine name (meaningless in containers, hence the override) |
| `user` | `AGENTLEDGER_USER`, otherwise git `user.email` (repo config, then `~/.gitconfig`), otherwise the OS username |
| git repo / branch / worktree | read directly from the `.git` files; no `git` process (too slow per hook, and may not be installed) |
| `eventType`, `nativeSessionId` | the two top-level payload fields named for that agent (Claude Code: `hook_event_name`, `session_id`). Missing → `"unknown"` / empty; the event is still sent. |

- **Repo URLs are normalized** to `host/owner/repo`. This strips any credentials embedded in the remote URL (e.g. `https://<token>@github.com/...`), and makes HTTPS and SSH forms of the same repo identical.

## Consequences

- Agents are never slowed noticeably, blocked or confused by logging.
- Failures are silent by design, so `status` and the log file are essential for diagnosis.
- The CLI knows a tiny amount about each agent (two field names, and its settings file for `install`). That grows by one small entry per agent.
- Binaries must be built per platform.

## Future enhancement

- **The current commit hash** (`gitCommit`), read from the same `.git` files, would tie agent activity to the exact code state. It's deliberately deferred, and definitely wanted. It's an additive change: an optional envelope field ([ADR 0011](0011-ingest-envelope-contract.md)), a new `CaptureContext` value and a new column.
