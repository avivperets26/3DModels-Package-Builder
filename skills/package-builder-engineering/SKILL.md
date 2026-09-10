---
name: package-builder-engineering
description: Apply Package Builder's repository-specific architecture, reuse, documentation, validation, and manual-Git workflow when implementing or reviewing work under C:\Dev\PackageBuilder.
---

# Package Builder Engineering

The user-approved PB-0805–PB-0807 combined branch exception is recorded in AGENTS.md.
Keep all three task identities, statuses and acceptance records distinct within that branch.

Use this skill for implementation, review, refactoring, or roadmap work in the Package Builder
repository.

## Required workflow

1. Read `/AGENTS.md` completely, then read the affected plan, architecture, backlog, and quality
   sections before changing files.
2. Confirm the current branch, working-tree state, active PB task, dependencies, and rollover state.
   Before creating a task branch, follow AGENTS.md's fresh-main sequence: fetch origin, switch to
   main, pull with `--ff-only`, verify clean main equals origin/main, then branch and record the base SHA.
   A failed fetch, ahead/diverged main, or unrelated dirty work must be resolved without discarding work.
3. Search for existing types, functions, policies, validators, state machines, fixtures, and test
   vectors before adding behavior.
4. Put canonical rules in the documented Domain, Contracts, or Application boundary. Keep WPF,
   CLI, Unity, Unreal, persistence, marketplace, and process code as adapters.
5. Reuse only when semantics, ownership, error behavior, and lifecycle match. Prefer composition and
   cohesive types; avoid speculative abstractions, god classes, and miscellaneous utility modules.
6. Add concise comments for responsibilities, invariants, side effects, and non-obvious engine or
   security constraints. Do not narrate self-evident statements.
7. Add focused tests plus architecture or conformance tests when behavior crosses a boundary.
8. Run the narrow tests first, then the repository-required validation proportional to the change.
9. Synchronize only genuinely affected documentation and lifecycle evidence.
10. Report the reuse/duplication audit, intentional duplicates, validation evidence, and manual Git
    handoff. Never stage, commit, push, merge, or publish without exact user authorization.

## Reuse audit questions

- Does an equivalent canonical rule already exist?
- Would extraction reduce real duplication without coupling unrelated platform behavior?
- Can Unity and Unreal share a typed contract or test vector while keeping engine calls separate?
- Is the class or method responsible for one cohesive concern?
- Are errors structured, deterministic, and tested at the owning boundary?
- Is any remaining duplication justified by a documented platform constraint?

## Backlog status accounting

- Follow the Status legend and Backlog Maintenance Rules in `docs/IMPLEMENTATION_BACKLOG.md`.
- At each start, finish, block, unblock, task addition/removal, and rollover, synchronize the canonical task header, checkbox, Active Work, and applicable completion evidence.
- Before implementation, mark the selected task IN PROGRESS and add its Active Work entry. Keep locally validated work IN PROGRESS until the existing Git, main CI, user-confirmation, and rollover gates pass.
- Run `& .\scripts\Update-BacklogStatus.ps1 -Write` after changes, then `& .\scripts\Update-BacklogStatus.ps1` before handoff. The repository baseline checks the summary too.
- Report DONE, IN PROGRESS, BLOCKED, BACKLOG, and remaining counts from the generated summary. Count each canonical PB definition once; label any historical completion imports explicitly.
