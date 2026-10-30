#!/bin/sh
# Installs the agentledger CLI from this ledger and connects this machine (ADR 0024).
#   curl -fsSL <ledger>/install.sh | sh -s -- --code XXXX-XXXX
# Without a code: installs or updates the CLI and its hooks, keeping the machine's connection if it has one.
# Installs to ~/.local/bin (or $AGENTLEDGER_INSTALL_DIR); needs no administrator rights.
set -eu

LEDGER_URL="__AGENTLEDGER_URL__"
CODE=""

while [ $# -gt 0 ]; do
  case "$1" in
    --code)
      [ $# -ge 2 ] || { echo "agentledger install: --code needs a value" >&2; exit 2; }
      CODE="$2"
      shift 2
      ;;
    --code=*) CODE="${1#--code=}"; shift ;;
    *) echo "agentledger install: unknown option $1" >&2; exit 2 ;;
  esac
done

case "$(uname -s)" in
  Linux) os=linux ;;
  Darwin) os=osx ;;
  *) echo "agentledger install: $(uname -s) isn't supported by this script; on Windows use install.ps1." >&2; exit 1 ;;
esac
case "$(uname -m)" in
  x86_64 | amd64) arch=x64 ;;
  aarch64 | arm64) arch=arm64 ;;
  *) echo "agentledger install: the $(uname -m) architecture isn't supported." >&2; exit 1 ;;
esac
platform="$os-$arch"

bin_dir="${AGENTLEDGER_INSTALL_DIR:-$HOME/.local/bin}"
mkdir -p "$bin_dir"
# Downloaded next to its final place, so the last step is an atomic rename: a hook running meanwhile uses the old file
# or the new one, never half of one.
new="$bin_dir/.agentledger.new.$$"
trap 'rm -f "$new"' EXIT

echo "Downloading agentledger for $platform from $LEDGER_URL..."
curl -fsSL "$LEDGER_URL/cli/$platform" -o "$new"
expected="$(curl -fsSL "$LEDGER_URL/cli/$platform.sha256" | cut -d ' ' -f 1)"
if command -v sha256sum > /dev/null 2>&1; then
  actual="$(sha256sum "$new" | cut -d ' ' -f 1)"
else
  actual="$(shasum -a 256 "$new" | cut -d ' ' -f 1)"
fi
if [ "$expected" != "$actual" ]; then
  echo "agentledger install: the download doesn't match its checksum; nothing was installed." >&2
  exit 1
fi
chmod 755 "$new"
mv -f "$new" "$bin_dir/agentledger"
echo "Installed $bin_dir/agentledger"

if [ -n "$CODE" ]; then
  "$bin_dir/agentledger" setup --url "$LEDGER_URL" --code "$CODE"
else
  "$bin_dir/agentledger" setup --url "$LEDGER_URL"
fi

case ":$PATH:" in
  *":$bin_dir:"*) ;;
  *) echo "Note: $bin_dir isn't on your PATH. Add it, e.g. in ~/.profile: export PATH=\"$bin_dir:\$PATH\"" ;;
esac
