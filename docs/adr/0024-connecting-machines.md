# 0024. Connecting machines: one command, enrollment codes, the CLI served by the ledger, self-update

- **Status:** Accepted (amends [ADR 0019](0019-api-keys.md): keys can also be created by enrollment)
- **Date:** 2026-10-30

## Context

Recording a machine's agent activity took five manual steps:
1. build or copy the CLI;
2. install the hooks;
3. work out the ledger's address from that machine (Docker networking);
4. create an ingest key in the web UI and paste it into `agentledger login`;
5. check `status`.

There were no CLI downloads, and nothing kept CLIs at the server's version. Dev containers have no browser, so a download page alone wouldn't help there. The user wanted one simple step, and the CLI to stay current without anyone remembering to update it.

## Decision

**Enrollment codes** (table `enrollment_codes`, aggregate `EnrollmentCode`)
- A signed-in user presses "Connect a machine" (`/settings/machines`). The ledger creates a one-time code, shown once, e.g. `K7PX-4QHM`:
  - 8 characters from an alphabet without look-alikes (about 40 bits);
  - stored only as a SHA-256 hash, with its owner, creation time and expiry (10 minutes later);
  - when used: the time, the host name the CLI reported, and the key it produced.
- **`POST /enroll`**, anonymous (the code is the credential) and rate-limited per IP address (10 a minute, like login):
  - exchanges an unused, unexpired code for a new **ingest key** owned by the code's creator, named `<host> (connected <date>)`, with no expiry;
  - the key is returned once, and the code is marked used in the same transaction;
  - an unknown, expired or used code gets the same 400.
- A key created this way is an ordinary personal key (ADR 0019): events sent with it are attributed to its owner, and it's revoked like any other.

**The ledger serves the CLI and its installer** (anonymous: a new machine has no key, and nothing served is secret)
- The release builds the Native AOT CLI for Linux x64 and Arm64, macOS Arm64 and x64, and Windows x64, on GitHub runners per operating system, since AOT can't target another OS. The binaries go into the API image (about 50 MB) and are attached to the GitHub Release.
- `GET /cli/{platform}` serves a binary; its SHA-256 checksum is published next to it.
- `GET /install.sh` and `GET /install.ps1` serve the install scripts, with the address they were downloaded from written into them. A machine is therefore set up with exactly the address it reached the ledger by.
- From source (development) there are no binaries: these answer 404 with an explanation.

**The install command**: `curl -fsSL <ledger>/install.sh | sh -s -- --code <code>`, or the PowerShell equivalent.
1. Detect the platform.
2. Download the matching CLI and check its checksum.
3. Install it to `~/.local/bin` (Windows: `%LOCALAPPDATA%\AgentLedger\bin`, added to the user PATH). No administrator rights.
4. Run **`agentledger setup --url <address> --code <code>`**:
   - save the address in the user config file;
   - exchange the code and save the key;
   - install the Claude Code hooks;
   - print `status`.
- **Re-runnable:**
  - without a code on a connected machine, it updates the CLI and refreshes the hooks;
  - with a code on a machine that has a key, it replaces the key (the old one stays valid until revoked).
- The page also shows the two-step version (download the script, read it, run it).
- **Address order:** `AGENTLEDGER_URL`, then the project's `.agentledger.json`, then the user config file, then the default ([ADR 0012](0012-cli-design.md)).

**Self-update: the CLI follows the ledger, not "the latest"**
- The ledger includes `X-AgentLedger-Version` in its answers to `POST /events`, `/transcripts/lines` and `/git/observations`.
- When it differs from the CLI's own version, and the last attempt was over an hour ago, the hook finishes its normal work first, then:
  - downloads the matching binary and checks its checksum;
  - replaces itself atomically, so a concurrent hook runs the old or the new file, never half of one;
  - refreshes the hook installation.
- Failures are logged and retried an hour later; the old CLI keeps working.
- Skipped when the running binary isn't the installed one (e.g. built from source, as in this repository's dev container) and when `"autoUpdate": false` is set.
- Rolling the ledger back rolls the CLIs back too. The ledger keeps accepting events from older CLIs, so a machine briefly behind loses nothing.

**Knowing each machine's CLI version**
- Every CLI request sends `User-Agent: agentledger-cli/<version>`.
- The ledger records it with the key's "last used" time (a new column on `api_keys`): it describes the delivery, not the event, so it stays out of the raw tables.
- `status` compares the CLI's version with the ledger's.

**The machines page** (`/settings/machines`)
- The connect button and the commands (address choice preselected to the one you browse from, copy buttons).
- The connected machines (enrolled keys): name, connected, last used, CLI version, "behind the ledger", status, and **Disconnect** (revokes the key).
- Members see their own; admins see everyone's.

**Dev containers:** a rebuild wipes the home folder. The README suggests running the install command (without a code) in `postCreateCommand` and keeping `~/.config/agentledger` in a named volume, so the key survives rebuilds.

## Consequences

- Connecting a machine is one copy-paste; no key or address is handled by hand, and no real key appears on screen or in shell history.
- Every connected machine keeps its CLI at the ledger's version, within about an hour of its next agent activity.
- A new table, two anonymous endpoints (rate-limited enrollment, downloads), a column on `api_keys`, a release build matrix, a larger image, and a CLI that rewrites its own file.
- The binaries aren't code-signed: browsers flag downloads, but `curl` and PowerShell, as used here, don't.
