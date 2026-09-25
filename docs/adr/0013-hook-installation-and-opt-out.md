# 0013. Hook installation scope and per-project opt-out

- **Status:** Accepted
- **Date:** 2026-09-25

## Context

[ADR 0012](0012-cli-design.md) introduced `agentledger install <agent>` but left two questions open: how the installed hook refers to the binary, and which settings file it goes into. Claude Code reads hooks from three places:

| Scope | File | Affects |
|---|---|---|
| `user` | `$CLAUDE_CONFIG_DIR/settings.json` (default `~/.claude/settings.json`) | you, in every project |
| `local` | `<project>/.claude/settings.local.json` (gitignored) | you, in this project |
| `project` | `<project>/.claude/settings.json` (committed) | everyone who clones the project |

The main use case is a developer who installs AgentLedger once and wants every agent session traced, without setting up each project.

## Decision

- **One `install` command with `--scope user | local | project`, defaulting to `user`.** `uninstall` takes the same option and removes only AgentLedger's entries.
- **The binary is referenced by name** (`agentledger hook claude-code`), so one entry works on every machine and survives upgrades.
  - `install` checks that `agentledger` is on `PATH` and warns (with the fix) if not, because a missing binary makes every hook fail with "command not found".
  - `--absolute-path` writes the full path of the running binary instead, for setups where `PATH` can't be relied on.
- **Entries:** one per Claude Code hook event (27), `"matcher": "*"`.
  - All are `async: true`, except `SessionEnd`, which runs synchronously with a 2-second timeout so it isn't cut off as the session ends.
  - `install` merges into the existing file, keeping other hooks and settings, and is idempotent: our entries are recognized by their command and replaced.
  - A file that isn't valid JSON is left untouched and reported.
- **Per-project opt-out:** `"enabled": false` in a project's `.agentledger.json`, or `AGENTLEDGER_ENABLED=false`, makes `hook` exit silently without recording or spooling anything. It's resolved like every other setting (ADR 0012).
- **Double-recording guard:** a hook installed in two scopes runs twice per event, and each run creates its own event ID, so downstream deduplication can't merge them. `install` warns when AgentLedger is already installed in another scope, and `status` lists the scopes where it's installed.

## Consequences

- Installing once traces everything, which is the intended experience.
- Sensitive projects must opt out explicitly; user-scope tracing is opt-out, not opt-in.
- Removing the binary without `uninstall` breaks hooks in every project. The fix is documented.
- Dev containers have their own home folder, so each dev container needs its own install.
- `project` scope is for teams that have adopted AgentLedger. It shouldn't be used in public repositories, where most people who clone won't have the binary.
