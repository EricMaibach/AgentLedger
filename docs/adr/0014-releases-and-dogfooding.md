# 0014. Releases as container images, and dogfooding with a stable ledger

- **Status:** Accepted
- **Date:** 2026-09-25

## Context

AgentLedger should record its own development: every agent session working on this repository goes into a ledger, which is the best test of whether it works (see "Open questions" in the roadmap). The API and database in the dev container are unsuitable for that:
- they run the code being changed, which may not build or may be buggy (a bug answering 400 would make the CLI drop events);
- the dev database is routinely wiped during development;
- the dev API only runs when someone starts it.

## Decision

**Releases:**
- Publishing a GitHub Release (tag `vX.Y.Z`) runs `.github/workflows/release.yml`. It re-runs the full CI on the tagged commit, then builds the `Dockerfile` and pushes `ghcr.io/ericmaibach/agentledger-api:X.Y.Z` and `:latest`. Pre-releases get their version tag but don't move `latest`.
- The image is multi-stage (SDK to build, ASP.NET runtime to run), runs as a non-root user, and applies migrations on startup. The version and commit are stamped in, and `GET /status` reports them (`X.Y.Z+<commit>`).
- The GHCR package is public, so it can be pulled without credentials.

**The stable ledger:**
- `deploy/compose.yml` runs the released image with **its own** Postgres 18 and data volume, on host port 58090, with restart policies and health checks. It's deployed as a Portainer stack on the development machine, independent of the dev container.
- **Updates are manual:** after a release, Portainer's "Pull and redeploy". Automatic image updates (e.g. the maintained Watchtower fork `nicholas-fedor/watchtower`; the original `containrrr/watchtower` is archived) were deliberately left out for simplicity.
- The database password comes from a stack variable (`DB_PASSWORD`), never from the committed file.

**Dogfooding in the dev container:**
- The dev container's environment sets `AGENTLEDGER_URL=http://host.docker.internal:58090` (with an `extra_hosts` mapping, since Docker Engine on Linux doesn't provide that name) and `AGENTLEDGER_HOST=agentledger-devcontainer`.
- On creation, `.devcontainer/install-agentledger.sh` builds the CLI from the checkout (Native AOT), installs it into `~/.local/bin`, and runs `agentledger install claude-code` (user scope, [ADR 0013](0013-hook-installation-and-opt-out.md)). Rerun the script after changing the CLI.

## Consequences

- The ledger only runs released, CI-verified code, and its data can't be touched by development work.
- While the ledger is down or updating, hook events wait in the CLI's spool and are delivered later ([ADR 0012](0012-cli-design.md)). This is also the safety net for a broken release.
- A schema change in a release migrates the ledger's database when the new image starts.
- Every Claude Code session in the dev container is recorded, including prompts and tool output. Recording AgentLedger's own development is the point; a project can opt out with `"enabled": false` ([ADR 0013](0013-hook-installation-and-opt-out.md)).
- The CLI in the dev container is built from the working copy, not from a release. A broken CLI can't disrupt the agent (it always exits 0), but it could fail to record. `agentledger status` shows it.
