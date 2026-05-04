# Assembly Architect — Agent Task Backlog

This folder contains one Markdown file per task. Each task is a self-contained prompt
intended to be handed to a single AI coding agent. Tasks must be executed **strictly in
the listed order** — later tasks assume the previous ones are merged.

## Conventions used in every task file

- **Vendor placeholder:** Replace `<vendor>` with the chosen company / author id (lower-case,
  no spaces, e.g. `acme`). Pick once during task `0.1` and keep consistent.
- **Package root:** `Packages/com.<vendor>.assembly-architect/`
- **Unity version:** Unity 6 (`6000.x`). Editor-only code.
- **Scripting:** C# 9 features allowed. `nullable` disabled (Unity default).
- **Style:** 4-space indent, CRLF, UTF-8, `internal` by default, `public` only on the
  package's intentional API.
- **Definition of Done (applies to every task unless overridden):**
  1. Code compiles in the Editor with zero warnings (treat warnings as errors locally).
  2. No new entries in the Console (errors or warnings) when the Editor reloads.
  3. Public types have XML doc comments.
  4. Any new behavior is covered by at least one edit-mode test in `Tests/Editor/`.
  5. The task's explicit acceptance criteria all pass.
  6. A single commit (or PR) is produced with message
     `feat(<area>): <task id> <short summary>` (e.g. `feat(core): 1.5 cycle detector`).

## Order of execution

| Phase | Tasks |
|-------|-------|
| 0 — Bootstrap            | 0.1, 0.2 |
| 1 — Domain & infra       | 1.1, 1.2, 1.3, 1.4, 1.5, 1.6 |
| 2 — Editor window shell  | 2.1, 2.2 |
| 3 — Graph view           | 3.1, 3.2, 3.3, 3.4 |
| 4 — Commands & Undo      | 4.1, 4.2, 4.3, 4.4 |
| 5 — Analysis & polish    | 5.1, 5.2, 5.3, 5.4, 5.5 |
| 6 — Tests & quality      | 6.1, 6.2, 6.3 |
| 7 — Distribution         | 7.1, 7.2, 7.3, 7.4 |

Phase 5 sub-tasks may be parallelized; everything else is strictly sequential.
