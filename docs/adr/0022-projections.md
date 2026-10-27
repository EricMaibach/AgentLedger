# 0022. Projections: an activity hierarchy grouped into units of work, rebuilt per session, with lineage

- **Status:** Accepted
- **Date:** 2026-10-26

## Context

The ledger now holds three kinds of raw evidence: hook events ([ADR 0010](0010-raw-landing-accepts-duplicates.md)), transcript lines ([ADR 0020](0020-transcripts.md)) and git commits and tags ([ADR 0021](0021-git-context.md)). None of it is readable as "what the agents did": a turn is spread over dozens of hook events and transcript lines, a tool call over a `PreToolUse`, a `PostToolUse` and two transcript blocks.

Phase 4 turns the raw evidence into projections ([ADR 0001](0001-raw-event-ledger-with-projections.md)), interpreted by a per-agent adapter ([ADR 0003](0003-agent-neutral-core-with-adapters.md)), each linked to its evidence ([ADR 0015](0015-web-ui-and-evidence.md)).

**Scope, after researching existing tools (2026-10-26).**
- Viewing a session as a trace is well covered: Langfuse, Arize Phoenix, MLflow, Opik and others have Claude Code plugins, and MCP servers over their traces.
- What none of them does:
  - grouping agent work by **git** (branch, commit, release);
  - keeping the raw data as **evidence**;
  - running **self-hosted, team-shared and independent of any git host**.
- So units of work are the centre of Phase 4. The turn and step view stays plain, without trace-explorer extras (waterfall charts, latency graphs, full-text search over responses, side-by-side comparison).
- The read-only MCP server ([ADR 0008](0008-read-only-mcp-server.md)) follows the first projections, because agents querying their own history is the other gap.

What the stable ledger's real data showed (2026-10-26):
- Every hook event carries `prompt_id`, and almost every transcript user line carries the same value as `promptId` (1,348 of 1,357), including subagent lines.
- A subagent's transcript file names the tool call that launched it (`toolUseId` in its `.meta.json`).
- **Internal helper subagents** greatly outnumber real ones: 143 `SubagentStop` events with no agent type, no start event and no transcript, against 6 real subagents.
- Token usage is repeated on every line of a response. Transcripts hold no prices.
- Most thinking blocks hold only a signature. The API leaves thinking text out unless a client asks for summaries (Claude Code's `showThinkingSummaries` setting, or `--thinking-display summarized`). Even then the text is a summary, never the full reasoning.

## Decision

**The activity hierarchy (agent-neutral, defined in Core)**
- **Project:** a repository, identified as git context identifies it (normalized remote URL, else host and folder). Worktrees and clones of one repository are one project. A folder outside git is its own project.
- **Session:** one agent conversation, keyed by agent and native session ID.
  - Compaction stays in the session.
  - `/clear` and forks start a new session that records where it came from.
  - Its end is inferred ([ADR 0004](0004-session-end-is-inferred.md)).
  - A session can span branches and commits, so grouping happens per turn.
- **Turn:** one `prompt_id`, from the prompt to the agent stopping.
  - Status: **completed** (`Stop`), **failed** (`StopFailure`), **interrupted** (the next prompt arrives without a stop), or **in progress**.
  - Its prompt's source: **typed**, **queued** (typed while the agent was busy) or **injected** by the system. Injected prompts (hook output, task notifications) are shown dimmed and labelled, since they aren't a person's words.
  - A turn is the smallest unit assigned to a branch, commit or release, and is never split.
- **Steps,** in time order inside a turn:

  | Step | Content |
  |---|---|
  | Prompt | Text, source, attachments |
  | Thinking | Text when present, else "summary not available", dimmed |
  | Response | Assistant text |
  | Tool call | Tool, input, result, success or failure, duration, permission decisions |
  | Subagent | Agent type and task; nests its own steps |
  | Context | Added to the conversation: instructions and skills loaded, file mentions, hook output (shown compact) |
  | Event | Notification, permission request, compaction, folder change, model switch, settings change, errors |

- **Model responses** carry the model and token usage. A response can produce several steps (thinking, text, tool calls); its tokens are counted **once per `message.id`**.

**Merging hooks and transcripts (the Claude Code adapter)**
- **One step from both sources,** matched on `prompt_id`/`promptId` for prompts and `tool_use_id` for tool calls. Events with no shared key are placed by time.
- **The transcript decides content and order;** hooks add timing, failures and permission decisions.
- **Without transcript lines** (e.g. older sessions), a step is built from hooks alone and marked as such. The `Stop` hook's last message is the fallback for a response.
- `PostToolBatch` repeats `PostToolUse` and never becomes a step. Duplicate receipts count once (by `event_id`).
- **Subagents:**
  - The Agent tool call *is* the subagent step: its transcript file's `toolUseId` links them, and expanding it shows the subagent's steps, nested to any depth.
  - A background subagent that finishes after its turn still belongs to that turn, with its real times. The later "task finished" message links back to it.
- **Internal helper subagents** (no agent type, no start, no transcript) aren't steps. A turn shows how many there were, with links to their raw events.
- **Edge cases:**
  - an interrupted tool call shows "no result (interrupted)";
  - compaction is an event step, with the summary Claude Code wrote, inside the turn where it happened;
  - API errors are event steps of a failed turn.

**Units of work (lenses)**
- A lens groups turns into units. Each turn carries what every lens needs: project, session, the branch and `HEAD` when its prompt was sent (even if the branch changes during the turn), the day, and its events' tags.

  | Lens | A unit is |
  |---|---|
  | Session | one session's turns |
  | Day | the turns started that day, in the viewer's time zone |
  | Branch | the turns on that branch |
  | Commit | the turns that led to that commit |
  | Release | the commits between two release tags, with their turns |
  | Workflow tag | the turns whose events carry that tag `key=value` ([ADR 0005](0005-workflow-correlation-via-tags.md)) |

  The workflow tag lens is how an automation tool groups its runs (e.g. `AGENTLEDGER_TAG_workitem=123`) without the ledger knowing anything about it.
- **Auto, the default,** shows one tree per project:
  - in progress (uncommitted);
  - feature branches;
  - releases, each listing its commits, with **Unreleased** first.

  Turns on the default branch group under their commit. The default branch is inferred from its name (`main`, `master`, `trunk`); a per-project setting can come later.
- Each unit shows turns, sessions, people, time span, tokens, and the commits and files involved.

**Commit attribution**
- **The rule:** a turn goes to the **first commit made in its worktree after the turn started**, on its line of history. Each worktree is followed separately.
  - Discussion or research turns that led to a commit belong to it.
  - When one turn makes several commits, the turn goes to the first; the later ones show "also made during turn …".
- **No commit yet:** the unit is **in progress** while `HEAD` hasn't moved. If the branch was switched or reset without a commit, it becomes **not committed**, kept under the branch.
- **File check:** if the turns edited files and the commit touches none of them, the turns aren't assigned to it (e.g. a pull that brought in someone else's newer commits). Turns that edited nothing are assigned by time alone.
- **Rewritten history:** an amended or rebased commit keeps its author time and message, so the old commit is linked to the new one. The turns follow, and the old commit shows as rewritten.
- **Squash merges done on a git host** produce a commit that can't be linked back. That work stays visible under its branch but isn't counted in a release (accepted limitation).
- **Releases:**
  - A release tag looks like a version: `v1.2.3`, `1.2.3`, or a pre-release such as `v1.0.0-rc1`.
  - A release holds the commits reachable from its tag but not from the previous release tag, worked out from the recorded parents.
  - Commits after the latest release on the default branch are **Unreleased**.
  - A moved tag counts at its current position; a deleted tag stops being a release.
  - History from before install is unknown.
- **Every assignment stores its reason:** "first commit after the turn started", "rewritten from abc123", or "excluded: no shared files".

**Tokens, not cost**
- Tokens are shown **per model**, broken down into input, output, cache write, cache read and thinking. The headline figure is input plus output; cache reads are shown separately. Tokens from different models are **never added together**, since a token on one model isn't worth a token on another.
- **No cost in Phase 4.** On subscription plans a token has no marginal price, so a dollar figure would mislead. Each response's model and tokens are stored, so an **optional, instance-wide "estimated list-price cost"** can be added later by rebuilding. A price table per model and date would come with it, plus a cross-check against Claude Code's own `cost-state` totals.

**Lineage**
- **Stable IDs:** projected rows get content-derived IDs ([`ContentDerivedId`](../../src/AgentLedger.Core/ContentDerivedId.cs)) from raw identifiers:
  - turn: agent, session, `prompt_id`;
  - tool call: plus `tool_use_id`;
  - thinking or response: plus transcript line ID and block position;
  - event step: plus `event_id`.

  A rebuild produces the same IDs, so links, bookmarks and IDs given to agents keep working.
- **Sources:** turns, steps and model responses store their sources as arrays, `source_event_ids` (by `event_id`) and `source_line_ids`, with GIN indexes for the reverse lookup. A session doesn't: its key (agent and native session ID) already finds all its raw rows. Commit and release assignments store the git rows they used and their reason. Each session records the projection version that built it.
- **In the UI:** the details pane lists a step's evidence, shown inline or on its raw page. Raw pages link back to where they're used.
- **In the MCP:** results carry the IDs, so an agent can check an interpretation against the raw evidence.

**The projection engine**
- **Stored tables,** not computed on read: merging is real work, and the UI and MCP need fast totals per unit.
- **A background worker in the API process:**
  - It polls every few seconds (`Projections:Interval`, default 5 s), back to back while a rebuild is under way. (Revised while building step 1: a signal from ingest, as first planned, couldn't make rows appear sooner, since the commit lag applies anyway, and polling keeps ingest unaware of projections. Each poll is two indexed queries.)
  - It finds new raw rows with the project's watermark pattern (`docs/architecture.md`, "Incremental processing"): rows whose `updated_at` lies between the stored low watermark and the latest `updated_at` minus a short safety lag, so rows committed late aren't missed.
  - **Ingest is unchanged** and knows nothing about projections (only an index on `transcript_lines.updated_at` is added).
  - A Postgres advisory lock keeps it to one worker.
- **A whole session at a time:**
  - Any new raw row for a session rebuilds that session from all its raw rows, swapped in within one transaction.
  - Late, out-of-order or repeated data is harmless.
  - Git attribution is a separate step per project, rerun when its turns, commits or tags change.
- **Versioned logic:** the projection code has a version. When a release changes it, the worker rebuilds every session in the background, oldest first, while the UI keeps working and shows the progress.
- **Where the code lives:**
  - **Core:** the neutral model.
  - **UseCases (`Projections/ClaudeCode`):** the Claude Code adapter, pure C# from one session's raw rows to the neutral model, unit-tested with sanitized fixtures. Adapters move to their own projects when a second agent arrives.
  - **Infrastructure:** reading raw rows, bulk-writing projections (the write store pattern), checkpoint and lock.
  - **Web:** hosts the worker.

**Build order** (each step released and checked against the stable ledger)
1. Engine and core projections, with no UI: the neutral model; the adapter for prompts, thinking, responses and tool calls; tables with stable IDs and lineage; the worker. Checked against raw counts: turns against prompts, tool calls against Pre/Post pairs, tokens against unique `message.id`s.
2. The work page with the session lens ([ADR 0023](0023-interactive-work-pages.md)).
3. The remaining steps: subagents, context and event steps, internal helper counts, interrupted, failed, compacted and in-progress turns.
4. Git lenses (branch, commit, release, Auto), day and workflow tag.
5. The read-only MCP server, designed first.
6. Finishing touches: back-links on raw pages, the rebuild notice, the narrow-screen details overlay.

**Not doing:** evaluations, prompt management, datasets, trace-explorer extras, or AI suggestions to improve skills or instructions. The ledger is a neutral record; tools that improve agents read it through the MCP.

## Consequences

- The ledger answers "what went into this branch, commit or release" with evidence behind every answer, which the research found nobody else doing.
- Improved interpretation reaches all history by bumping the projection version.
- Commit attribution is an inference: its reasons are stored and shown, and squash merges on a git host are a known gap.
- Tokens across models can't be compared or ranked as one number until optional cost exists.
- Thinking is mostly absent unless users enable Claude Code's thinking summaries; even then it's a summary.
- Session rebuilds cost more than incremental updates, but sessions are small (the largest about 6,000 lines). If that changes, the engine can be made incremental without changing the model.
