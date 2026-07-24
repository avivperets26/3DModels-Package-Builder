# PB-0010 Contribution Workflow Evidence

**Task:** PB-0010 — Add contribution and branch workflow documentation  
**Branch:** `docs/PB-0010-contribution-workflow`  
**Lifecycle:** 🟢 **DONE**
**Evidence date:** 2026-07-24

## Scope and Current State

PB-0010 adds the root project introduction, the permanent contributor workflow, a dependency-free contribution-documentation validator, and repository-baseline integration. The documentation describes only the repository-foundation capabilities currently present and labels package building, engine adapters, the desktop workflow, and marketplace packaging as planned work.

PB-0010 is logically complete and was synchronized exactly once at the beginning of PB-0011. It is `[x]` / 🟢 **DONE**, has been removed from Active Work, and has exactly one Completion Log row.

## Publication and Completion Evidence

| Gate | PB-0010 evidence |
|---|---|
| Final task commit | `eaf8846df7bf4bb8edc82d8407da8c1a61130231` |
| Pull request | [PR #11](https://github.com/avivperets26/3DModels-Package-Builder/pull/11) |
| Successful PR CI | [Run 30077559953](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30077559953) |
| Merge commit | `b7396bf6b557da26df2f2d08a70c6f6d1b1a3796` |
| Required `main` CI | [Run 30077718661](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30077718661) — successful |
| User confirmation | Explicitly provided on 2026-07-24 for commit, push, merge, green required `main` CI, and completion |
| Exception | None used |

## PB-0009 Rollover

The required immediately previous task synchronization was completed before PB-0010 implementation:

| Gate | PB-0009 evidence |
|---|---|
| Final task commit | `973aec7be954115e83fe1c18d0c8139f2d111fda` |
| Pull request | [PR #10](https://github.com/avivperets26/3DModels-Package-Builder/pull/10) |
| Final PR CI | [Run 30047612915](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30047612915) — successful |
| Merge commit | `96c13a565f9ed85d66d13a357cfa2571b2e4dd93` |
| Required `main` CI | [Run 30047819416](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30047819416) — successful; both repository-baseline and core-application jobs passed |
| User confirmation | Explicitly provided on 2026-07-23 for commit, push, merge, green required `main` CI, and completion |
| Exception | None used |

PB-0009 is `[x]` / 🟢 **DONE**, removed from Active Work, and recorded in exactly one Completion Log row. Its evidence document is finalized. PB-0013 is unchanged.

## Documentation Outcomes

### Root README

`README.md` records:

- Package Builder's planned local-first purpose and repository-foundation status.
- Planned portable FBX/GLB, Unity, Unreal, and marketplace-adapter targets.
- The five planned asset cases.
- Free prerequisites, repository-local .NET SDK `10.0.302`, and Visual Studio Code terminal setup.
- Exact setup, documentation validation, repository validation, restore, Release build, formatting, smoke-test, and complete core-pipeline commands.
- Current repository structure and `C:\Dev\PackageBuilder` single-root containment.
- Links to `CONTRIBUTING.md` and the primary rules, plan, architecture, backlog, and quality documents.
- An explicit disclaimer that model import, package building, desktop workflow, and marketplace release generation are not yet available.

### Contributor Workflow

`CONTRIBUTING.md` records:

- The requirement to read `AGENTS.md` first.
- PB task IDs, one task per branch, the branch pattern, and all seven approved branch types.
- DONE, PROCESS, and BLOCKED lifecycle meanings.
- The permanent one-merge rollover and single Completion Log row.
- Optional pull requests, allowed direct merges after local validation, required `main` CI, and explicit user confirmation.
- Exclusive user ownership of staging, commits, pushes, merges, pull requests, tags, releases, and GitHub settings.
- Documentation synchronization, clearly scoped commit messages, exact version pinning, the free Visual Studio Code workflow, and single-root containment.
- Public-repository safeguards and prohibited tracked secrets, personal data, downloaded tools, caches, logs, engine state, generated/private assets, and unlicensed third-party content.
- The complete local validation pipeline.

## Version Boundaries

The README and CONTRIBUTING file distinguish four concepts:

1. `PB-####` values are backlog task IDs, not product versions.
2. Pinned SDK, tool, engine, action, and dependency versions are reproducibility inputs, not Package Builder release versions.
3. Package Builder releases are separately approved product artifacts and Git events.
4. Marketplace-requirements profiles are independently versioned because marketplace rules can change outside a product release.

No final Package Builder release-versioning scheme is presented as approved. Planning examples remain examples.

## Dependency-Free Validator

`scripts/Test-ContributionDocumentation.ps1` uses only PowerShell, .NET platform APIs, and Git already required by the repository. It performs eleven fail-closed checks:

1. `README.md` and `CONTRIBUTING.md` exist in the tracked or unignored reviewable Git set.
2. Required headings exist.
3. README purpose, targets, five cases, current status, and unfinished-functionality disclaimer are present.
4. Contribution workflow and public-repository policies are present.
5. Branch types and lifecycle markers agree with `AGENTS.md` and the backlog.
6. Pull requests remain optional, direct merges remain allowed after validation, and the one-merge rollover sequence is complete.
7. Task, dependency, product-release, and marketplace-profile version concepts remain distinct.
8. Required command text is present and every referenced script or repository file exists.
9. Local Markdown links resolve inside the repository.
10. No paid tool is mandatory and unfinished capability is not presented as available.
11. The documents contain no credential-shaped example, personal filesystem path, binary content, or prohibited-content link.

`scripts/Test-RepositoryBaseline.ps1` invokes the validator both in-process and through standalone Windows PowerShell 5.1. It installs no additional software.

## Acceptance-Criterion Traceability

| PB-0010 criterion | Automated evidence |
|---|---|
| Professional README and accurate current status | Contribution validator checks 2–3, 8–10; baseline Markdown validation |
| Complete contribution and branch workflow | Contribution validator checks 2, 4–6 |
| Preserve approved Git and release policy | Contribution validator checks 5–6 against `AGENTS.md` and backlog |
| Distinguish task/tool/product/profile versions | Contribution validator check 7 |
| Dependency-free documentation validator | Direct validator execution and Windows PowerShell parsing |
| Baseline integration | Repository baseline in-process and standalone validator checks |
| Evidence and backlog synchronization | Repository baseline task/lifecycle/Active Work/Completion Log checks |
| PB-0010 rollover synchronization | Repository baseline lifecycle, Active Work, and single Completion Log row checks |
| PB-0013 preserved | Changed-file and focused diff audit |

## Validation Results

| Validation | Current result |
|---|---|
| `scripts/Test-ContributionDocumentation.ps1` | Pass; 11 checks passed, 0 failed. |
| Windows PowerShell parsing | Pass; every reviewable PowerShell script parsed successfully during the repository baseline, including the new validator. |
| `scripts/Test-RepositoryBaseline.ps1 -RequireTrackedFiles` | Pass; 23 checks passed, 0 failed, including in-process and standalone Windows PowerShell contribution validation. |
| `scripts/Invoke-CoreCi.ps1` | Pass; all nine stages passed in the final run. |
| Exact SDK and locked restore | Pass; repository-local .NET SDK `10.0.302`; all projects were up to date under locked restore. |
| Release build | Pass; 15 projects, 0 warnings, 0 errors. |
| Formatting | Pass; `dotnet format --verify-no-changes`, Ruff `0.15.22` lint, and Ruff formatting checks passed. |
| Baseline smoke tests | Pass; 4 discovered, 4 passed, 0 failed, 0 skipped; source-candidate nonmutation check passed. |
| Markdown structure and local links | Pass in the targeted validator and repository baseline. |
| `git diff --check` | Pass in the repository baseline, core pipeline, and final focused audit. |
| Secret, personal-path, and prohibited-file checks | Pass in the targeted contribution validator and repository baseline. |
| Task IDs, dependencies, lifecycle, Active Work, and Completion Log | Pass for the PB-0010 implementation checkpoint. PB-0010 was later synchronized exactly once during PB-0011 using the publication evidence above. |
| PB-0013 changed-file audit | Pass; its Active Work row and complete task-definition block exactly match `HEAD`. |

## Documentation Impact

- Added `README.md`.
- Added `CONTRIBUTING.md`.
- Added this PB-0010 evidence record.
- Finalized `docs/PB-0009_CORE_CI_EVIDENCE.md`.
- Updated `docs/IMPLEMENTATION_BACKLOG.md` for the PB-0009 rollover and active PB-0010 state.
- Updated `docs/TECH_STACK_AND_ARCHITECTURE.md` for the contributor-documentation files, validator, and current repository-baseline composition.
- `docs/Package_Builder_Plan.md` and `docs/QUALITY_AND_RELEASE_GATES.md` require no change because PB-0010 documents and validates their existing requirements without changing product behavior or a normative quality threshold.

## Completion State

PB-0010 has no remaining completion gate. Its final task commit was pushed, merged through PR #11, validated by successful PR and required `main` CI, and explicitly confirmed complete by the user on 2026-07-24. No exception was used. The permanent one-merge rollover bookkeeping is recorded in PB-0011.
