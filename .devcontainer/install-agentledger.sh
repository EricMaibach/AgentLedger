#!/usr/bin/env bash
# Builds the agentledger CLI from this checkout (Native AOT), puts it on PATH and installs its
# Claude Code hooks, so this dev container records its own agent sessions (dogfooding, docs/adr/0014).
# Runs when the container is created; rerun it after changing the CLI.
set -euo pipefail

repo="$(cd "$(dirname "$0")/.." && pwd)"
out="$(mktemp -d)"
trap 'rm -rf "$out"' EXIT

echo "Building the agentledger CLI (Native AOT)..."
dotnet publish "$repo/src/AgentLedger.Cli" --configuration Release --runtime linux-x64 --output "$out" --nologo --verbosity quiet
mkdir -p "$HOME/.local/bin"
install -m 755 "$out/agentledger" "$HOME/.local/bin/agentledger" # just the binary, not the .dbg symbols

"$HOME/.local/bin/agentledger" install claude-code

# Not an error if the stable ledger isn't running yet: hook events wait in the spool until it is.
"$HOME/.local/bin/agentledger" status || true
