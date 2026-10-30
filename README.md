# AgentLedger

[![CI](https://github.com/EricMaibach/AgentLedger/actions/workflows/ci.yml/badge.svg)](https://github.com/EricMaibach/AgentLedger/actions/workflows/ci.yml)

An auditable ledger of what AI coding agents do.

AgentLedger hooks into coding agents (Claude Code, Codex, GitHub Copilot, Snowflake Cortex Code) and records everything they expose: prompts, tool calls, responses, reasoning and token usage. Everything goes into a single PostgreSQL store, so that people and other AI can review, measure and correlate agent activity across tools.

> **Status:** early development. Capturing raw hook events from Claude Code works, and AgentLedger records its own development. Next: a web UI and authentication (Phase 2). See the [roadmap](docs/roadmap.md).

## How it works

```
agent hook ──stdin──► agentledger CLI ──HTTP──► API ──► PostgreSQL
```

- Agents run a small CLI from their hooks. It's a single Native AOT binary with no .NET runtime needed, and a hook run takes a few milliseconds.

**Connecting a machine** ([ADR 0024](docs/adr/0024-connecting-machines.md)): in the web UI, open Settings → Machines, choose the address the machine reaches the ledger at, and press "Connect a machine". Run the command it shows on that machine (a computer, a server or a dev container); the one-time code in it works once, for ten minutes:

```bash
curl -fsSL http://<ledger>/install.sh | sh -s -- --code K7PX-4QHM                    # Linux and macOS
$env:AGENTLEDGER_CODE='K7PX-4QHM'; irm http://<ledger>/install.ps1 | iex             # Windows PowerShell
```

It downloads the CLI from the ledger (checksum-checked) into `~/.local/bin` (Windows: `%LOCALAPPDATA%\AgentLedger\bin`), saves the ledger's address, exchanges the code for the machine's own ingest key (owned by you, listed under Settings → Machines, where "Disconnect" revokes it), installs the Claude Code hooks and shows `agentledger status`. No administrator rights needed.

- **Updates are automatic:** the installed CLI follows the ledger's version, updating itself within about an hour of its next hook after you update the ledger. To control updates yourself, add `"autoUpdate": false` to `~/.config/agentledger/config.json`; running the install command again (without a code) updates it.
- **Dev containers:** a rebuild wipes the home folder. Run the install command without a code in `postCreateCommand`, and keep `~/.config/agentledger` in a named volume so the machine's key survives rebuilds; otherwise connect it again with a new code.
- **Building from source** (as this repository's own dev container does): `dotnet publish src/AgentLedger.Cli -c Release -r linux-x64 -o ~/.local/bin`, then `agentledger setup --url http://<ledger> --code <code>` (or `agentledger install claude-code` and `agentledger login` with a key from Settings → API keys). A CLI built from source never updates itself.

- **An ingest key is required** to send events ([ADR 0019](docs/adr/0019-api-keys.md)). Keys are personal: connecting a machine creates one for you; `login` stores one you created under Settings → API keys; `AGENTLEDGER_API_KEY` works too. Without a valid key, events wait in the spool.
- **Transcripts too** ([ADR 0020](docs/adr/0020-transcripts.md)): each hook also sends the new lines of the session's transcript (and subagents'), read where it stopped last time. They wait in the spool like events, and `status` shows how many batches are waiting. Claude Code leaves thinking text out of its transcripts by default. To record it, set `"showThinkingSummaries": true` in `~/.claude/settings.json` and restart Claude Code (verified 2026-10-27), and pass `--thinking-display summarized` for headless runs (`claude -p`). Even then it's a summary of the model's reasoning, not the full reasoning.
- **Git context** ([ADR 0021](docs/adr/0021-git-context.md)): each event records the checked-out commit, and when it changes, the new commits (message, changed files) are sent too, read with `git log`; so are tags as they're created, moved or deleted. Nothing to set up; history from before the CLI first saw a repository isn't sent.
- **`hook <agent>`** is what the installed hooks call. It's silent and always exits 0, so it never disrupts the agent. If the API is unavailable, events wait in a local spool and are sent later, or right away with `agentledger flush`.
- **Pointing at another ledger:** run `agentledger setup --url …`, set `AGENTLEDGER_URL`, or add `{ "url": "..." }` to `.agentledger.json` in a project (the environment variable wins, then the project file, then the user config).
- **Opting a project out:** add `{ "enabled": false }` to that project's `.agentledger.json`.
- **Removing it:** run `agentledger uninstall claude-code` before deleting the binary.

Details: [ADR 0012](docs/adr/0012-cli-design.md), [ADR 0013](docs/adr/0013-hook-installation-and-opt-out.md), [ADR 0024](docs/adr/0024-connecting-machines.md).

## Contributing

Read [`docs/architecture.md`](docs/architecture.md) first. Before finishing a change, all of these must pass:
- `dotnet build`
- `dotnet test`
- `dotnet format --verify-no-changes`
