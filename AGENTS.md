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
11. Work on one PB task per branch using its documented branch name.
12. Do not mark a PB task complete until its acceptance criteria and tests pass and the user confirms that the required commit, push, GitHub CI, and merge gates are complete, or the user approves and documents an exception.
13. Record completed work in the backlog completion log.
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
25. Codex must leave every PB task open until the user confirms that its commit, push, CI, and merge requirements were completed.

## PB Task Lifecycle Markers

- 🟢 **DONE** — Every documented acceptance criterion and required test has passed, required Git and GitHub gates have evidence, and the user has confirmed completion. The task checkbox may be marked `[x]` and the Completion Log may be updated only in this state.
- 🟡 **PROCESS** — Work is active, locally implemented, locally validated, pushed, under review, or otherwise progressing, but one or more completion gates remain. The task checkbox stays `[ ]`.
- 🔴 **BLOCKED** — Work cannot make meaningful progress because a specific unresolved dependency, decision, permission, external state, or repeated failure prevents continuation. Record the exact blocker and keep the task checkbox `[ ]`.

Lifecycle markers supplement rather than replace task checkboxes, acceptance evidence, the Completion Log, or the user's exclusive authority over Git and completion confirmation.

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
