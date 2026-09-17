---
name: package-builder-engineering
description: Apply Package Builder's repository-specific architecture, reuse, documentation, validation, and manual-Git workflow when implementing or reviewing work under C:\Dev\PackageBuilder.
---

# Package Builder Engineering

The user-approved PB-0805–PB-0807 combined branch exception is recorded in AGENTS.md.
Keep all three task identities, statuses and acceptance records distinct within that branch.
The same rule applies to the separately approved PB-0808–PB-0810 combined scope in AGENTS.md.
The separately approved PB-0901–PB-0903 scope uses `codex/PB-0901-PB-0903-documentation`.
The separately approved PB-0904/PB-0905/PB-0907 scope uses `codex/PB-0904-PB-0905-PB-0907-docs-capture`.

Use this skill for implementation, review, refactoring, or roadmap work in the Package Builder
repository.

The approved PB-0908–PB-0910 combined scope uses `codex/PB-0908-PB-0910-media-reports`.

## Required workflow

The approved PB-1006/PB-1115 scope uses `feat/PB-1006-PB-1115-unreal-fab-e2e`.
Reuse native delivery/reopen and shared archive policy. Verify one source across all three
targets, bind native findings to exact archive hashes, and preserve historical profile pins.


The approved PB-1113/PB-1114/PB-1116 scope uses
`feat/PB-1113-PB-1114-PB-1116-unreal-package-preview`. Reuse safe archives and canonical
preview controls, verify fresh native extraction/reopen and runtime behavior, and clean outputs.
For this scope, honor the user-approved VS 2022 Build Tools installation exception in AGENTS.md.
VS Code remains the editor; compile the editor-only helper and validate it in Unreal before
claiming that prerequisite installation resolves the preview runtime acceptance.


The approved PB-1110–PB-1112 scope uses `feat/PB-1110-PB-1112-unreal-overview-validation`.
Reuse shared presentation tokens and image validation. Native saved-map reopen, final-material
captures, product-only coverage and hostile-project rejection are required; remove test projects.

New branches use conventional prefixes (`feat/`, `fix/`, `docs/`, etc.), never `codex/`;
historical branch names remain evidence. Commit subjects use conventional `feat:`, `fix:`, etc.
The approved PB-1107–PB-1109 scope uses `feat/PB-1107-PB-1109-unreal-materials-meshes`.
Reuse canonical material definitions and existing image/mesh policies; validate pixels and actual
Unreal material/mesh save, reopen and rendering behavior, then clean disposable projects.

The approved PB-1104–PB-1106 scope uses `codex/PB-1104-PB-1106-unreal-import`.
Reuse canonical naming/texture rules and shared Python containment helpers; validate real
save/reopen behavior and exclusive execution, then remove disposable projects.

The approved PB-1101–PB-1103 combined Unreal foundation scope uses
`codex/PB-1101-PB-1103-unreal-foundation`; live engine acceptance remains required independently
of plain-Python or .NET contract tests. Reuse shared protocol helpers across engine adapters.
Honor the explicit external Unreal installation exception in AGENTS.md; keep all generated state
in the repository and never change the user-managed engine installation.

The approved PB-1008–PB-1010 portable/Unity release scope uses `codex/PB-1008-PB-1010-fab-release`.
That historical milestone excluded Unreal; PB-1006/PB-1115 adds it with separate evidence and a new pinned profile. Unresolved sourced rules must never be waived for E2E acceptance.

The approved PB-1004/PB-1005/PB-1007 combined scope uses `codex/PB-1004-PB-1005-PB-1007-fab-validators`.

PB-0016 dependency maintenance uses `codex/PB-0016-dependency-refresh`; update package pins,
NuGet lockfiles and existing approval maps together while preserving exact-version checks.

The approved PB-1001–PB-1003 combined scope uses `codex/PB-1001-PB-1003-fab-foundation`.

The approved PB-0911/PB-0912 combined scope uses `codex/PB-0911-PB-0912-reports-support`.

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
   Follow AGENTS.md's test-artifact cleanup rule: remove owned generated packages, extractions and
   temporary engine projects after success or failure, while preserving source fixtures and compact
   evidence. Prefer the existing `scripts/UnityTestArtifacts.Common.ps1` for Unity `artifacts/u` runs.
   Record any deliberate manual retention and clean it after inspection; report failed cleanup.
   This applies retroactively to completed tasks and to every future task/rerun. Check
   `docs/TEST_ARTIFACT_CLEANUP.md` at startup for retained-run exceptions; update that audit after
   removing historical outputs without changing DONE statuses or historical validation results.
   After the last validation, follow AGENTS.md's end-of-task process cleanup: shut down owned
   engines, previews, test runners and build/compiler servers, then verify exit. Use the contained
   .NET environment's `dotnet build-server shutdown` after builds finish. Verify process ownership
   before stopping leftovers; preserve unrelated user apps, active tasks and reusable disk caches.
   Record intentional retention or shutdown failures in the handoff/cleanup audit.
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
