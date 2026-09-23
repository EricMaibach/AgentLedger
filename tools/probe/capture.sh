#!/usr/bin/env bash
# Capture probe: dumps each agent hook payload plus relevant environment variables
# to .probe/<date>/ so real payloads can be studied and turned into test fixtures.
# Usage (as a hook command): capture.sh <agent-label> <event-name>
# The registered event name wins; the payload's own event field is only a fallback
# (some agents, e.g. Copilot CLI camelCase hooks, don't include it). Mismatches are recorded.
# Must never interfere with the agent: all errors are swallowed and it always exits 0.

{
  label="${1:-unknown}"
  registered="${2:-}"
  root="${CLAUDE_PROJECT_DIR:-${CORTEX_PROJECT_DIR:-$PWD}}"
  out="$root/.probe/$(date -u +%Y%m%d)"
  mkdir -p "$out"

  payload="$(cat)"
  reported="$(printf '%s' "$payload" | jq -r '.hook_event_name // .hookEventName // empty')"
  event="${registered:-${reported:-unknown}}"
  base="$out/$(date -u +%H%M%S.%N)_${label}_${event}_$$"

  printf '%s\n' "$payload" > "$base.json"

  if [ -n "$registered" ] && [ "$registered" != "$reported" ]; then
    printf '%s\tregistered=%s\treported=%s\n' "$(basename "$base")" "$registered" "${reported:-<none>}" >> "$out/mismatches.tsv"
  fi

  # Only variables that help identify the agent/session/context; mask anything secret-looking.
  env | grep -E '^(CLAUDE|CODEX|COPILOT|CORTEX|COCO|AGENTLEDGER|OTEL|GIT_|TERM_PROGRAM|VSCODE|PWD=)' \
      | sed -E 's/^([^=]*(TOKEN|KEY|SECRET|PASSWORD|AUTH)[^=]*)=.*/\1=***REDACTED***/I' \
      | sort > "$base.env"
} >/dev/null 2>&1

exit 0
