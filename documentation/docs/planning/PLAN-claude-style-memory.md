# Plan: OpenClaw-Inspired Memory Features

## Goal

Add memory capabilities inspired by OpenClaw memory/session workflows while preserving the current local-agent architecture:

1. Session continuity (`session_id`, resume, continue, fork)
2. Persistent transcript/state storage
3. File checkpointing + rewind
4. Structured streaming state events
5. Retention and safety controls

This plan is incremental and intentionally starts with low-risk pieces.

## Status (Updated February 19, 2026)

Current implementation status (Python repo):

- Phase 1 (Session persistence): Completed.
- Phase 2 (Checkpoint/rewind for `write_file`/`append_file`): Completed.
- Phase 3 (expanded mutation coverage + advanced events/callbacks): In progress.

.NET repo status: Not yet started. This plan documents the target architecture for both repos.

## Source Inspiration and Scope

Primary inspiration:

- OpenClaw memory/session/checkpoint workflow patterns (resume, branching, recoverability, and event visibility).

Scope clarification:

- This plan adapts those patterns to this repo's local architecture.
- It does not attempt to replicate proprietary hosted backend internals from any vendor.

## Detailed Feature Intent + Implementation Comparison

This section clarifies what "OpenClaw-inspired memory" means in this repo.

Important scope note:

- The goal is practical workflow parity (resume, fork, rewind, event traceability), not implementation parity with external platforms.
- Where external behavior is unspecified, this plan chooses explicit, deterministic local behavior.

Quick comparison:

| Capability | OpenClaw-Inspired Expectation | Planned Here | Why Different |
| --- | --- | --- | --- |
| Session continuity | Resume and branch prior conversations | Explicit `session_id` create/resume/continue/fork with deterministic precedence | Local/offline operation and predictable testable semantics |
| Transcript persistence | Conversation state survives beyond one run | SQLite-backed `sessions/messages/tool_calls` with ordered replay | Inspectable local storage and transactional safety |
| Rewind | Recover from undesired agent changes | First-class checkpoint + per-file rewind reporting | Local file mutation risk is primary; explicit undo is required |
| Streaming state events | Meaningful runtime state transitions | Typed local events persisted to DB, callback-ready later | Repo-specific observability and offline replay/debugging |
| Retention and safety | Controlled memory lifecycle | Configurable caps/retention, planned redaction hooks | Operator-controlled local compliance and storage bounds |

### Comparison: Claude Code Memory Features (Current)

Based on Anthropic's Claude Code memory docs (retrieved February 19, 2026), Claude Code memory is primarily instruction memory via `CLAUDE.md` files and related commands (`/memory`, `/init`, `#` shortcut), with hierarchical file loading and imports.

What Claude Code memory emphasizes:

- File-based instruction memory in multiple scopes (enterprise, project, user).
- Hierarchical loading and precedence of memory files.
- Importing additional files from memory files (`@path` syntax).
- Fast authoring/editing workflow for memory files via CLI shortcuts and commands.

How this plan overlaps:

- Both approaches aim to preserve useful context across sessions.
- Both make memory operator-editable, not opaque.
- Both support project-level conventions and reusable workflow guidance.

How this plan differs:

- This plan adds persisted conversational state (`sessions/messages/tool_calls`) rather than only instruction files.
- This plan includes checkpoint/rewind of file mutations, which is outside Claude Code memory-file scope.
- This plan includes structured persisted runtime events for replay/debugging.
- This plan proposes retention/pruning over persisted execution artifacts, not just memory-file maintenance.

Why both can coexist conceptually:

- Claude Code-style memory files are strong for stable instructions ("how to work").
- Session/checkpoint/event memory is strong for execution history ("what happened").
- Combining both yields policy + history + recoverability for local autonomous coding workflows.

### 1) Session continuity (`session_id`, resume, continue, fork)

What we are planning:

- Every conversation is assigned a stable `session_id`.
- Users can explicitly resume an existing session.
- Users can continue a named session across process restarts.
- Users can fork a session to branch from prior context without mutating the original lineage.

What it does exactly:

- `session_id` identifies a single conversation timeline.
- `resume` reopens an existing timeline and loads its persisted messages as active context.
- `continue` with a known ID reuses that timeline across process restarts without requiring transcript copy/paste.
- `fork` creates a new timeline that starts from the same prior transcript, then diverges independently.
- `parent_session_id` preserves ancestry so operators can trace where a branch came from.
- Session status (`active`, `archived`, `deleted`) controls lifecycle without physically deleting everything immediately.

Use cases:

- Ongoing research thread: continue a multi-day analysis session without re-briefing the assistant each day.
- Policy drafting alternatives: fork one branch for "strict policy" and another for "balanced policy" and compare.
- Customer support escalation: resume the exact prior troubleshooting context for a returning case.
- Operations handoff: day shift forks a night-shift session to test a different remediation path safely.

### 2) Persistent transcript/state storage

- Persist user/assistant/system messages and tool call outcomes in SQLite.
- Rehydrate in-memory conversation from persisted rows at startup/resume.
- Keep sequence ordering explicit with monotonic per-session `seq`.

### 3) File checkpointing + rewind

- Create checkpoints around mutating tool execution.
- Track pre-change file state and restore via `rewind_files(checkpoint_id)`.
- Start strict with `write_file`/`append_file`; expand carefully for `bash` and MCP later.

### 4) Structured streaming state events

- Emit typed lifecycle events for session/message/tool/checkpoint/rewind milestones.
- Persist events in DB first; optional callbacks can be layered later.

### 5) Retention and safety controls

- Configurable caps for sessions/messages plus time-based retention.
- Planned optional redaction filters before persistence.
- Explicit defaults that preserve current behavior unless memory is enabled.

## Data Model (SQLite)

Use SQLite for portability and transactional safety.

Database file:

- Default: `.micro_x/memory.db` under current working directory
- Configurable via new config field `MemoryDbPath`

Tables:

1. `sessions` — id, parent_session_id, created_at, updated_at, status, model, metadata_json
2. `messages` — id, session_id, seq, role, content_json, created_at, token_estimate
3. `tool_calls` — id, session_id, message_id, tool_name, input_json, result_text, is_error, created_at
4. `checkpoints` — id, session_id, user_message_id, created_at, scope_json
5. `checkpoint_files` — checkpoint_id, path, existed_before, backup_blob/backup_path
6. `events` — id, session_id, type, payload_json, created_at

## Config Additions

- `MemoryEnabled` (bool, default `false`)
- `MemoryDbPath` (string, default `.micro_x/memory.db`)
- `SessionId` (string, optional)
- `ContinueConversation` (bool, default `false`)
- `ResumeSessionId` (string, optional)
- `ForkSession` (bool, default `false`)
- `EnableFileCheckpointing` (bool, default `false`)
- `MemoryMaxSessions` (int, default `200`)
- `MemoryMaxMessagesPerSession` (int, default `5000`)
- `MemoryRetentionDays` (int, default `30`)

## Rollout Phases

### Phase 1: Session Persistence (Low Risk)
- SQLite store, session create/resume/fork, message persistence/reload

### Phase 2: Checkpoint/Rewind for File Tools (Medium Risk)
- Checkpointing for `write_file` + `append_file`, rewind command

### Phase 3: Expanded Mutation Coverage + Events (High Risk)
- Best-effort `bash` tracking, event persistence, retention/pruning

## Migration and Backward Compatibility

- Default `MemoryEnabled=false` keeps existing behavior unchanged
- When enabled with no prior DB, auto-create schema
- Existing compaction remains active; summaries become persisted messages
