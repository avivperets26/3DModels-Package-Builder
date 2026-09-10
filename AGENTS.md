# Package Builder Project Rules

These rules apply permanently to the entire `C:\Dev\PackageBuilder` tree unless the user explicitly replaces or amends them.

1. `C:\Dev\PackageBuilder` is the single project root.
2. All source code, documentation, downloads, local tools, runtime data, logs, fixtures, and generated artifacts must remain beneath this root.
3. Never use `C:\Dev\PackageBuilderData` or scatter project files elsewhere.
4. Keep large local tools, downloads, logs, caches, and generated artifacts out of Git using `.gitignore`.
5. The required stack must have a free local or self-hosted workflow. No paid IDE, library, SaaS service, or subscription may be mandatory.
6. Unity and Unreal integrations must clearly disclose their external licensing, eligibility, seat, and royalty conditions.
7. The repository must build, test, run, and debug from Visual Studio Code without requiring paid Visual Studio or paid extensions.
8. Use the latest approved stable/LTS versions of .NET, Blender, Unity, and Unreal. Never use preview versions for production without explicit approval.
9. Pin every approved tool version so builds are reproducible.
10. Read these documents before making architectural or implementation changes:
    - `docs/Package_Builder_Plan.md`
    - `docs/TECH_STACK_AND_ARCHITECTURE.md`
    - `docs/IMPLEMENTATION_BACKLOG.md`
    - `docs/QUALITY_AND_RELEASE_GATES.md`
11. Work on one implementation PB task per branch using its documented branch name; approved previous-task completion synchronization at the start of the next task branch is not a second implementation task.
12. A PB task becomes logically complete only after its acceptance criteria and tests pass, its task branch is committed and pushed, it is merged into and pushed on `main`, required `main` CI succeeds, and the user explicitly confirms completion, unless the user approves and documents an exception.
13. Record each completed task exactly once in the backlog Completion Log through the permanent one-merge rollover workflow.
14. Preserve unrelated user changes and never use destructive Git or filesystem commands without explicit authorization.
15. Validate files, paths, names, textures, rigs, animations, and package contents before publishing.
16. Publisher names such as `AvivPeretsFBX` must be configuration values and never hard-coded.
17. Keep platform-specific logic behind separate portable, Unity, Unreal, and marketplace adapters.
18. Prefer deterministic, repeatable builds and produce validation reports for every package.
19. Every future Codex task must begin by reading this `AGENTS.md` file completely before inspecting files, planning, or making changes.
20. The user exclusively controls Git commits and remote operations. Codex must never run `git commit` or `git push` unless the user explicitly authorizes that exact action.
21. Codex must not stage files, merge branches, create pull requests, create tags, or publish releases unless the user explicitly authorizes the exact action.
22. Authorization for one Git or remote action does not authorize another action or a future action.
23. Codex may use read-only Git commands such as `git status`, `git diff`, `git log`, and `git branch --show-current` for inspection and validation.
24. At the end of every task, Codex must report the changed files, test and validation results, suggested branch name, suggested commit message, and manual commands the user can run.
25. Codex must leave each PB task `[ ]` and active in its own task branch. After successful `main` CI and explicit user confirmation make it logically complete, its `[x]`, 🟢 **DONE**, Active Work removal, and Completion Log row are recorded at the beginning of the next task branch.
26. Pull requests are optional. A direct merge requires local validation, the task commit, task-branch push, merge into `main`, push of `main`, successful `main` CI, and explicit user confirmation; branch CI is not required when the direct branch push does not trigger it.
27. Before adding a class, method, validator, state machine, or engine behavior, search for an existing implementation or contract that can be reused or extended.
28. Keep domain rules, application orchestration, engine adapters, UI presentation, persistence, and external-process concerns in their documented layers; do not copy business rules into Unity, Unreal, WPF, CLI, or marketplace adapters.
29. Prefer small cohesive types, composition, dependency injection, typed contracts, shared test vectors, and explicit adapter boundaries. Do not create god classes, unrelated utility collections, or speculative abstractions.
30. Extract genuinely shared behavior when semantics and lifecycle match. Do not force unrelated behavior through a shared abstraction merely to reduce line count.
31. Treat duplicated validation rules, state transitions, serialization rules, naming rules, and cross-engine preview semantics as defects unless a documented platform constraint requires a separate implementation.
32. New or changed public/internal functionality must include concise XML/documentation comments that explain responsibility, invariants, side effects, and non-obvious platform constraints without restating the code.
33. Every implementation handoff must report the reuse/duplication audit performed for the changed scope and identify any intentional duplication with its reason and follow-up owner.

## Public Repository Safeguards

The approved GitHub repository is [https://github.com/avivperets26/3DModels-Package-Builder](https://github.com/avivperets26/3DModels-Package-Builder), and its approved visibility is public.

- Treat every tracked file as publicly visible.
- Never commit credentials, tokens, private keys, personal data, customer assets, or private configuration.
- Never commit downloaded SDKs, engine installations, logs, caches, generated packages, or marketplace assets.
- Check for secrets and prohibited files before every handoff.
- Keep local-only files protected by `.gitignore`.
- Do not include third-party assets unless their licence permits public redistribution.
- Redact sensitive information from examples and diagnostics.

## PB Task Lifecycle Markers

- 🟢 **DONE** — Every documented acceptance criterion and required test has passed, required Git and GitHub gates have evidence, and the user has confirmed completion. The task checkbox may be marked `[x]` and the Completion Log may be updated only in this state.
- 🟡 **IN PROGRESS** — Work is active, locally implemented, locally validated, pushed, under review, or otherwise progressing, but one or more completion gates remain. The task checkbox stays `[ ]`.
- 🔴 **BLOCKED** — Work cannot make meaningful progress because a specific unresolved dependency, decision, permission, external state, or repeated failure prevents continuation. Record the exact blocker and keep the task checkbox `[ ]`.

Lifecycle markers supplement rather than replace task checkboxes, acceptance evidence, the Completion Log, or the user's exclusive authority over Git and completion confirmation. During the approved rollover interval, a task may be logically complete after successful `main` CI and user confirmation while its repository checkbox and marker remain `[ ]` / 🟡 **IN PROGRESS** until the beginning of the next task branch.

## Permanent One-Merge Rollover Workflow

### Approved combined scope: PB-0805 through PB-0807

On 2026-09-10 the user explicitly requested PB-0805, PB-0806 and PB-0807 on one branch.
Use `feat/PB-0805-PB-0807-multi-item-flow` for this scope. This is a task-specific exception to
the one-task-per-branch rule, not a permanent replacement. Retain separate acceptance evidence,
checkboxes, Active Work rows and eventual Completion Log rows for all three tasks. Publication
and completion gates remain unchanged. Roll PB-0804 completion into this branch once.

### Start every task branch from freshly synchronized main

Before creating any new implementation branch:

1. Inspect the current branch and worktree; preserve all unrelated or uncommitted work.
2. Run `git fetch origin`. If fetching fails, do not create the branch from stale remote information.
3. Switch to `main`, then run `git pull --ff-only origin main`.
4. Verify that `git rev-parse HEAD` and `git rev-parse origin/main` are identical and that the worktree is clean.
5. Only then create the task's documented branch from that verified latest `main`, and record its starting commit in the task evidence.

If main is ahead, diverged, or cannot be checked out safely, resolve that condition before branching;
never reset, discard, silently stash, or carry unrelated work into the new task. This standing user
instruction authorizes routine fetch, checkout and fast-forward main synchronization for requested
work. It does not authorize task merges, commits, pushes or other publication actions.

### Publish and roll over

1. Each implementation task has one publication cycle: commit the task branch, push it, and merge it into `main` once.
2. Never return to an already merged task branch solely for completion bookkeeping.
3. A direct merge is allowed and requires neither branch CI nor a pull request.
4. The required direct-merge sequence is:
   - Complete local validation.
   - Commit the task branch.
   - Push the task branch.
   - Merge it into `main`.
   - Push `main`.
   - Wait for successful `main` CI.
   - Receive explicit user confirmation.
5. Never claim branch CI occurred when a direct task-branch push did not trigger a workflow.
6. `main` CI remains required unless the user explicitly approves and documents an exception.
7. After successful `main` CI and user confirmation, the task is logically complete; no completion-only change is added to its already merged branch.
8. At the beginning of the next task branch, before implementing that task:
   - Mark the previous task `[x]` and 🟢 **DONE**.
   - Remove the previous task from Active Work.
   - Add exactly one Completion Log row.
   - Record the previous task's final task commit, merge into `main`, successful `main` CI, and explicit user confirmation.
9. This previous-task synchronization is an allowed documentation operation in the next task branch and does not violate the one-implementation-task-per-branch rule.
10. Implement the next task normally in that same branch after the synchronization.
11. Do not create a dedicated completion-only branch, commit cycle, pull request, or merge.
12. The final project task or a final milestone with no successor may use one final documentation-only synchronization.
13. Pull requests remain optional for implementation tasks.

## Non-Negotiable Quality Rules

These rules are mandatory acceptance requirements. Follow [the detailed quality and release gates](docs/QUALITY_AND_RELEASE_GATES.md) for the exact thresholds, requirements-to-tests matrix, security controls, performance budgets, evidence requirements, and release-blocking conditions.

### 1. Quality-first engineering

- Correctness, maintainability, usability, accessibility, performance, and security are mandatory acceptance requirements.
- Never claim production readiness, best practice, security, or performance without corresponding current test and validation evidence.

### 2. UX/UI

- Use a consistent, accessible, and beginner-friendly interface with keyboard-only operation, screen-reader support, high contrast, scalable text, and clear focus states.
- Critical workflows must provide keyboard navigation, clear progress and current stage, elapsed time, cancellation, actionable errors, safe retry, and preserved user input.
- Use sensible defaults and progressive disclosure so advanced options do not overwhelm first-time users.
- Provide dry-run previews before changing or generating files, and validate critical workflows with automated UI tests and representative first-time users.
- Generated Unity and Unreal preview scenes must implement the same versioned, engine-neutral interaction and presentation contract. Share state semantics, defaults, validation, labels, and test vectors; keep only engine API adaptation platform-specific.
- Do not copy business or interaction rules between the desktop application, Unity, and Unreal. Reuse typed contracts and reusable engine-local components for camera orbit/zoom, studio lighting, item selection, animation transport, and preview overlays.
- Preview controls must be discoverable, responsive, and accessible: pointer interaction requires keyboard equivalents, visible focus, clear labels and state, safe reset behavior, and layouts that remain usable at supported resolutions and UI scaling.
- Customer-facing preview code must remain minimal and dependency-safe. Do not export editor workers, test harnesses, prior-product state, or unrelated runtime logic with a product package.

### 3. Testing

- Every requirement and PB acceptance criterion must map to automated validation in the maintained requirements-to-tests traceability matrix.
- Include unit, integration, contract, end-to-end, UI, regression, failure-recovery, installer, and upgrade tests as appropriate, including all five product cases and hostile, incomplete, malicious, and unusually large inputs.
- Enforce the coverage and mutation-testing thresholds in the detailed quality gates, but never treat coverage percentages or mutation scores as replacements for meaningful requirements testing.
- No task is complete while required tests fail or requirements remain untested.

### 4. Performance

- Use measurable, approved performance budgets and repeatable benchmarks for small, medium, and large fixtures.
- Prefer streaming, bounded concurrency, cancellation-token propagation, timeouts, safe cleanup, proven cache invalidation, minimal safe copying, and controlled resource use for large assets.
- Performance claims and optimizations require reproducible benchmark and resource-usage evidence.

### 5. Security

- Treat imported files, archives, models, textures, scripts, plugins, and engine projects as untrusted.
- Protect against path traversal, archive bombs, command injection, unsafe scripts or executables, unsafe process arguments, symlink/reparse-point escapes, filename collisions, and credential exposure.
- Use least privilege, isolated working directories, bounded timeouts, verified checksums and signatures where available, pinned dependencies, an SBOM, dependency-vulnerability scanning, secret scanning, static analysis, redaction, and safe subprocess execution.
- Maintain and test the threat model and vulnerability procedures. Never introduce telemetry, uploads, cloud processing, or other external communication without explicit user consent.

### 6. Installation and usability

- Installation, portable use where practical, first-run setup, updates, repair, diagnostics, and uninstall must be simple and tested.
- Never silently install third-party engines, accept licences, or imply licence eligibility for the user.
- Provide clear prerequisite detection for .NET, Blender, Unity, Unreal, required modules, disk space, permissions, and project-root containment, plus actionable repair guidance and exportable redacted diagnostics.
- Avoid administrator access unless a component genuinely requires it, and never remove user projects or generated packages during uninstall without explicit user choice.

### 7. Free tooling

- The required workflow must not depend on paid software, paid extensions, subscriptions, or hosted services.
- Optional paid integrations must remain isolated, replaceable, and nonessential to development, testing, building, running, debugging, packaging, and validation.

### 8. Workspace discipline

- All Package Builder files and project-owned state must remain beneath `C:\Dev\PackageBuilder`; never use `C:\Dev\PackageBuilderData` or another scattered location.
- Downloads, local tools, logs, caches, runtime data, temporary files, fixtures, generated packages, reports, and other artifacts must be organized beneath the project root and excluded from Git where required by `.gitignore`.

### 9. Visual Studio Code

- Building, testing, running, debugging, diagnostics, and packaging must work from Visual Studio Code and repository-local command-line tooling without requiring paid Visual Studio or paid extensions.

### 10. Manual Git ownership

- The user controls staging, commits, pushes, merges, pull requests, tags, and releases.
- Codex may perform any of those operations only after explicit user authorization for that exact action; authorization never carries to another or future action.

### 11. Required first action

- Every Codex task must read this `AGENTS.md` file completely before inspecting files, planning, running project commands, or making changes.

## Documentation Synchronization and Task Status

### 1. Documentation is part of implementation

- Documentation is a required task deliverable, not optional cleanup.
- When work changes behavior, architecture, configuration, dependencies, approved versions, UX, security, performance, installation, packaging, folder structure, or workflows, update every relevant document during the same task.
- Do not update unrelated documentation merely to create activity or make a task appear larger.
- If documentation does not require changes, the final handoff must state `Documentation impact: none` and explain why.

### 2. Required task status synchronization

- At the beginning of a new task branch, after reading `AGENTS.md`, first synchronize any immediately previous task that has successful `main` CI and explicit user completion confirmation, then inspect and update Active Work for the new task.
- Active Work must identify the PB task, documented branch, current local state, and real current blocker.
- Update the blocker whenever circumstances change; never leave a resolved blocker as the reported state.
- Treat `implemented locally`, `validated locally`, `pushed`, `CI passed`, `merged`, and `complete` as distinct lifecycle states.
- Never describe locally implemented or locally validated work as fully complete.
- Keep the current task checkbox unchecked and its marker at 🟡 **IN PROGRESS** or 🔴 **BLOCKED** throughout its own task branch and one merge.
- Successful `main` CI plus explicit user confirmation makes the task logically complete, but its repository status is synchronized only at the beginning of the next task branch.
- During that rollover synchronization, update the prior task to `[x]` / 🟢 **DONE**, remove it from Active Work, and add exactly one Completion Log row with its task commit, merge, `main` CI, and user-confirmation evidence.
- Do not return to the merged task branch or create a completion-only publication cycle for this synchronization.
- If no successor task exists, the final project task or milestone may use one final documentation-only synchronization.
- Never infer GitHub branches, pushes, CI, pull requests, merges, repository settings, or releases from local Git state.

### 3. Evidence-based status

- Before reporting task status, use read-only inspection and the relevant validation commands or evidence sources.
- Record exact test results, validation evidence, branch name, and commit when available.
- Do not claim that tests, CI, pushes, pull requests, merges, tags, or releases occurred without evidence.
- Record unresolved contradictions, decisions, and blockers explicitly instead of hiding or silently resolving them.

### 4. Documentation sources

Review and update the sources affected by the task, which may include:

- `AGENTS.md`.
- `docs/IMPLEMENTATION_BACKLOG.md`.
- `docs/Package_Builder_Plan.md`.
- `docs/TECH_STACK_AND_ARCHITECTURE.md`.
- `docs/QUALITY_AND_RELEASE_GATES.md`.
- Environment baseline documents.
- Architecture Decision Records.
- User installation and usage documentation.
- Requirements-to-tests traceability records.

### 5. Handoff requirements

Every task handoff must report:

- Task ID and current lifecycle state.
- Work completed.
- Work remaining.
- Files changed.
- Documentation changed, or `Documentation impact: none` with an explanation.
- Tests and validation performed.
- Current blockers and unresolved decisions.
- Suggested branch.
- Suggested commit message.
- Manual Git commands, when appropriate.
- Whether backlog Active Work and the Completion Log reflect the verified current state.

### 6. Manual Git ownership

- Documentation synchronization never authorizes Codex to stage, commit, push, merge, create a pull request, create a tag, publish a release, or change GitHub settings.
- The user performs all Git and remote operations unless the user explicitly authorizes Codex to perform one exact action.
- Codex records a Git-related lifecycle state only after receiving direct evidence or explicit confirmation from the user.

## Backlog status accounting

- Follow the Status legend and Backlog Maintenance Rules in `docs/IMPLEMENTATION_BACKLOG.md`.
- At each start, finish, block, unblock, task addition/removal, and rollover, synchronize the canonical task header, checkbox, Active Work, and applicable completion evidence.
- Before implementation, mark the selected task IN PROGRESS and add its Active Work entry. Keep locally validated work IN PROGRESS until the existing Git, main CI, user-confirmation, and rollover gates pass.
- Run `& .\scripts\Update-BacklogStatus.ps1 -Write` after changes, then `& .\scripts\Update-BacklogStatus.ps1` before handoff. The repository baseline checks the summary too.
- Report DONE, IN PROGRESS, BLOCKED, BACKLOG, and remaining counts from the generated summary. Count each canonical PB definition once; label any historical completion imports explicitly.
