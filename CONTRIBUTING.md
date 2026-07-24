# Contributing to Package Builder

Package Builder uses branch-sized PB tasks, repository-local tools, evidence-based validation, and user-controlled Git publication. These rules preserve the approved process; they do not replace [AGENTS.md](AGENTS.md), which remains authoritative.

## Before You Begin

1. Read [AGENTS.md](AGENTS.md) completely before inspecting files, planning, running project commands, or making changes.
2. Read the [product plan](docs/Package_Builder_Plan.md), [architecture](docs/TECH_STACK_AND_ARCHITECTURE.md), [implementation backlog](docs/IMPLEMENTATION_BACKLOG.md), and [quality gates](docs/QUALITY_AND_RELEASE_GATES.md) before architecture or implementation work.
3. Confirm the PB task, its documented branch, dependencies, acceptance condition, current lifecycle state, and any immediately previous task eligible for rollover.
4. Check the current branch and worktree without discarding or rewriting unrelated user changes.

## PB Tasks and Branch Workflow

`PB-####` identifies an implementation-backlog task. It is not a product version. Work on one implementation PB task per branch, using the task's documented branch:

```text
<type>/PB-####-short-description
```

Use lowercase words separated by hyphens after the task ID. Keep the branch independently reviewable and do not mix unrelated cleanup into it. The permitted previous-task rollover at the start of the next task branch is documentation synchronization, not a second implementation task.

## Allowed Branch Types

- `chore` — repository, dependency, or configuration work.
- `docs` — documentation-only work.
- `feat` — product capability.
- `fix` — correction to implemented behavior.
- `test` — test infrastructure or fixtures.
- `security` — security hardening.
- `release` — release preparation.

## Lifecycle Markers

- 🟢 **DONE** — every documented acceptance criterion, required automated test, Git and GitHub gate, required `main` CI gate, and explicit user confirmation has passed. The task is checked `[x]` only during the approved rollover.
- 🟡 **PROCESS** — work is active, locally implemented, locally validated, pushed, under review, or awaiting another completion gate. The task stays `[ ]`.
- 🔴 **BLOCKED** — a specific unresolved dependency, decision, permission, external state, or repeated failure prevents meaningful progress. Record the exact blocker and keep the task `[ ]`.

Locally implemented and locally validated are intermediate states, not completion.

## Permanent One-Merge Rollover

Each implementation task has one publication cycle. Do not return to an already merged task branch solely for completion bookkeeping.

The publication sequence is:

1. Complete local validation.
2. Commit the task branch.
3. Push the task branch.
4. Merge it into `main`.
5. Push `main`.
6. Wait for successful required `main` CI.
7. Receive explicit user confirmation of the commit, push, merge, required `main` CI, and completion.

After step 7, the task is logically complete but remains `[ ]` / 🟡 **PROCESS** in its already merged repository state. At the beginning of the next task branch:

1. Mark the immediately previous confirmed task `[x]` / 🟢 **DONE**.
2. Remove it from Active Work.
3. Add exactly one Completion Log row.
4. Record its final task commit, integration, successful required `main` CI, explicit user confirmation, and any explicitly approved exception.
5. Then implement the new PB task on that same branch.

Do not create a completion-only branch, commit, pull request, or merge. The final project task or milestone may use one final documentation-only synchronization when no successor exists.

## Pull Requests and Direct Merges

Pull requests are optional. A direct merge is allowed after local validation; it still requires the task commit, task-branch push, merge into and push of `main`, successful required `main` CI, and explicit user confirmation.

Branch CI is not required when a direct task-branch push does not trigger a workflow. Never claim a branch, push, pull request, CI run, merge, tag, or release without direct evidence.

## GitHub Governance

- Use the stable Markdown [bug report](.github/ISSUE_TEMPLATE/bug_report.md) or [feature request](.github/ISSUE_TEMPLATE/feature_request.md) template for public issues.
- Read [SECURITY.md](SECURITY.md) before raising a security concern. Never publish vulnerability details, credentials, private assets, unredacted logs, or personal data in an issue or pull request.
- The [pull-request template](.github/pull_request_template.md) is a review aid and does not make pull requests mandatory.
- [CODEOWNERS](.github/CODEOWNERS) routes review ownership but does not claim that branch protection or required code-owner review is enabled.
- [Dependabot](.github/dependabot.yml) proposes bounded weekly NuGet and GitHub Actions updates against `main`. Each proposal requires user review and manual merge; no automerge or publication is configured.
- GitHub public-repository secret scanning runs automatically for free. This repository intentionally has no `.github/secret_scanning.yml` because that file only defines scan exclusions, and no exclusion is approved.
- PB-1611 remains responsible for future pinned local and CI dependency, licence, vulnerability, and secret scanning.

## Manual Git Ownership

The user exclusively controls staging, commits, pushes, merges, pull requests, tags, releases, and GitHub settings. Codex or another automation agent may perform one of those actions only after the user explicitly authorizes that exact action; one authorization does not carry to a different or future action.

Read-only commands such as `git status`, `git diff`, `git log`, and `git branch --show-current` may be used for inspection and validation.

## Documentation Synchronization

Documentation is part of implementation. In the same PB task, update every document materially affected by changes to behavior, architecture, configuration, dependencies, approved versions, UX, security, performance, installation, packaging, folders, or workflow.

Keep the active task `[ ]` / 🟡 **PROCESS** or 🔴 **BLOCKED** on its own branch and do not add it to the Completion Log. Record tests, evidence, unresolved decisions, and the real current blocker. If no documentation changes are needed, the handoff must say `Documentation impact: none` and explain why.

## Commit Messages

Use one clearly scoped commit for the task unless the user explicitly chooses another reviewed history. Include the PB ID and describe the outcome, for example:

```text
docs(PB-0010): document the contribution workflow
```

Do not hide unrelated cleanup, generated output, or completion-only bookkeeping in the commit.

## Version and Dependency Policy

- Pin every approved SDK, tool, engine, action, and dependency version as far as its ecosystem permits.
- Promote a version only after the applicable verification and compatibility evidence passes.
- Use the repository-local .NET SDK selected by `global.json`; do not silently substitute a machine-wide SDK.
- Keep required development and test dependencies usable through a free local or self-hosted workflow.
- Treat `PB-####` values as task IDs, never product versions.
- Treat pinned tool and dependency versions as reproducibility inputs, never Package Builder release versions.
- Treat Package Builder product releases separately from independently versioned marketplace-requirements profiles.
- No final product release-versioning scheme is approved yet; planning examples are not a release decision.

## Visual Studio Code Workflow

Visual Studio Code with a PowerShell terminal is the supported development baseline. Open the repository root, then enter the contained environment:

```powershell
Set-Location C:\Dev\PackageBuilder
. .\scripts\Enter-PackageBuilderEnvironment.ps1
dotnet --version
```

The version must be `10.0.302`. Paid Visual Studio, paid extensions, and IDE-only build or test actions are optional and cannot become prerequisites.

## Free Tooling and Single-Root Containment

The required build, test, run, debug, validation, and packaging workflow must not depend on a paid IDE, library, subscription, software edition, hosted service, or runner plan. Optional paid integrations must remain isolated and nonessential.

All source, documentation, downloads, local tools, logs, caches, runtime data, temporary files, fixtures, reports, and generated artifacts must remain beneath `C:\Dev\PackageBuilder`. Repository scripts must not fall back to another data root, a user profile, or the system temporary directory.

Unity and Unreal integrations must disclose applicable external licence, eligibility, seat, and royalty terms. Never accept a third-party licence or imply eligibility for a contributor.

## Public Repository Safeguards

The approved GitHub repository is public. Treat every tracked file as publicly visible.

Never track:

- Credentials, tokens, private keys, signing material, or private configuration.
- Personal data or personal filesystem paths.
- Downloaded SDKs, tools, installers, or engine installations.
- Caches, logs, temporary files, build output, or generated validation output.
- Unity or Unreal generated engine state.
- Generated packages, marketplace releases, customer assets, or other private assets.
- Third-party files without a licence that permits public redistribution.

Keep local-only state in the ignored repository-local directories. Use redacted examples and diagnostics, and run the repository checks before every handoff.

## Complete Local Validation

From a PowerShell terminal in the repository root:

```powershell
Set-Location C:\Dev\PackageBuilder
. .\scripts\Enter-PackageBuilderEnvironment.ps1
& .\scripts\Install-Ruff.ps1
& .\scripts\Test-ContributionDocumentation.ps1
& .\scripts\Test-GitHubGovernance.ps1
& .\scripts\Test-RepositoryBaseline.ps1 -RequireTrackedFiles
dotnet restore .\PackageBuilder.sln --locked-mode
dotnet build .\PackageBuilder.sln --configuration Release --no-restore
dotnet format .\PackageBuilder.sln --no-restore --verify-no-changes --severity info --verbosity minimal
& .\scripts\Test-Formatting.ps1
& .\scripts\Test-BaselineUnitTests.ps1 -VerifyNoSourceChanges
& .\scripts\Invoke-CoreCi.ps1
```

`Invoke-CoreCi.ps1` is the authoritative complete local core pipeline. The preceding commands expose each setup, documentation, repository, restore, build, formatting, and test entry point for focused diagnosis. Run additional task-specific tests required by the active PB acceptance criteria.

Before handoff, also inspect `git diff --check`, the changed-file scope, Markdown links, PowerShell parsing, secrets and personal paths, prohibited files, task/dependency state, Active Work, and the Completion Log. Report exact results rather than inferring success.
