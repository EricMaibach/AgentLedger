# 0021. Git context: HEAD on every event, commits and tags observed from the local repository

- **Status:** Accepted
- **Date:** 2026-10-26

## Context

Projections (Phase 4) will group agent activity into units of work. One of the strongest signals is git: on a feature branch the branch is the unit; on the default branch, commits mark the boundaries; releases (version tags) group commits. Events already record the branch, but not the commit, and nothing records commits or tags. When an agent edits files the commit doesn't exist yet, so work can only be tied to commits after the fact.

Capture can't be backfilled, so this is captured before projections are designed, without interpreting it ([ADR 0001](0001-raw-event-ledger-with-projections.md)). It must need no setup per repository and no integration with a git host (any host, or none, must work).

## Decision

**`HEAD` on every event**
- The capture context gains the commit checked out at that moment (`gitCommit`, read from the `.git` files like the branch, with no `git` process). Optional: absent outside a repository or with a detached, unreadable `HEAD`.
- It is the commit the work was based on: the first event after a commit sees the new `HEAD`, so boundaries are found after the fact. Attributing work to commits is an inference, made by projections.

**Commits and tags, observed by the CLI when they change**
- On every hook the CLI compares the repository's `HEAD` and a fingerprint of its tag files (`.git/refs/tags/`, `.git/packed-refs`) with what it saw last (state per worktree, with a lock, next to the spool). Almost always nothing changed, and no process runs.
- When `HEAD` moved: `git log` between the last seen and the current `HEAD`, oldest first, at most 200 commits per run, with parents, author and commit times, the full message, and changed files (path and change kind; at most 1,000 per commit, with a truncated flag beyond that). Only commits made after the CLI first saw the repository.
- When the tag files changed: `git for-each-ref refs/tags`, compared with the last list: tags created, moved or deleted. Each observation carries the commits that describe it: existing and created have their commit, moved has its commit and the previous one, deleted has the previous one. Annotated tags are followed to their commit. Tag state is kept per shared git folder, since worktrees share tags.
- **First sight of a repository:** a baseline of its current `HEAD` and existing tags (so the first release after install knows where the previous one was), but no commit history: commits from before the CLI can't be linked to agent activity.
- `git` missing or failing: logged, commits and tags skipped (`HEAD` still captured). A 2-second timeout per command. Opted-out projects ([ADR 0013](0013-hook-installation-and-opt-out.md)) aren't checked.
- Spool first, then save the state, as for transcripts ([ADR 0020](0020-transcripts.md)): a repeat after a crash is harmless.

**Sending and storing**
- `POST /git/observations` with an ingest key ([ADR 0019](0019-api-keys.md)), through its own spool folder: one repository's commits and tag changes as one batch, stored in one transaction. The answer counts new and already stored commits and recorded tag observations.
- The repository is identified by its normalized remote URL, as events record it; without a remote, by host and project folder.
- **`git_commits`:** one row per commit, keyed by repository and hash (a hash is the commit's content, so a repeat is the same commit and is skipped). Merges have two parents and, as git lists them by default, no files.
- **`git_tag_observations`:** appended, never updated: tags can be moved or deleted, so the table keeps what was seen when; the current state is derived.
- Both record the key that sent them and when they arrived.

**Not doing**
- Git hooks per repository (forgotten, and they clash with tools that manage hooks), and git-host APIs (GitHub, GitLab…): local tags work for any host; tags made remotely arrive with the next fetch, which is fine for grouping history.

## Consequences

- Phase 4 can offer a commit lens (turns between commits, matched to changed files) and releases (commits between release tags, with their work and cost), rebuilt as late data arrives.
- Two more raw tables, a column on receipts and a third ingest endpoint; the CLI runs `git` occasionally.
- Commits made where no agent ran, and history from before install, aren't seen.
