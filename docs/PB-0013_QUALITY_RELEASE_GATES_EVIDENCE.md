# PB-0013 Permanent Quality and Release-Gate Baseline Evidence

**Task:** PB-0013 — Establish the permanent quality and release-gate baseline
**Branch:** `docs/PB-0013-quality-release-gates`
**Lifecycle:** 🟢 **DONE**
**Evidence date:** 2026-07-24

## Scope and Current Interpretation

PB-0013 validates the permanent quality baseline across [project rules](../AGENTS.md), the [product plan](Package_Builder_Plan.md), [technology architecture](TECH_STACK_AND_ARCHITECTURE.md), [implementation backlog](IMPLEMENTATION_BACKLOG.md), [normative quality and release gates](QUALITY_AND_RELEASE_GATES.md), and the relevant accepted ADRs. It adds deterministic documentation validation only; it introduces no application, engine, marketplace, packaging, UI, or installer functionality.

The normative requirement definitions live only in `docs/QUALITY_AND_RELEASE_GATES.md`. Other sources describe policy and ownership without creating alternate requirement IDs or release-blocking definitions.

## PB-0012 Rollover

PB-0012 was committed, pushed, merged, passed required CI, and was explicitly confirmed complete by the user on 2026-07-24:

- Final task commit: `335691dcceeaa645231539a2ec83a3dae9db2a3e`.
- Pull request: [#13](https://github.com/avivperets26/3DModels-Package-Builder/pull/13).
- Successful PR workflow: [run 30083665801](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30083665801).
- Merge commit: `f4b5a5d39b2de97e404f837150bbe0d869e3a366`.
- Successful required `main` workflow: [run 30083674462](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30083674462).
- No CI, completion, or quality exception was used.

This PB-0013 rollover marks PB-0012 `[x]` / 🟢 **DONE**, removes it from Active Work, and adds exactly one chronological Completion Log row. The detailed original and final evidence remains in [PB-0012 initial ADR evidence](PB-0012_INITIAL_ADRS_EVIDENCE.md).

## Final Publication and PB-0101 Rollover

PB-0013 completed every required publication and confirmation gate:

- Final task commit: `8f79883d9a78c1a211510ee4ea8c855405e12e3c`.
- Pull request: [#14](https://github.com/avivperets26/3DModels-Package-Builder/pull/14).
- Optional PR workflow: [run 30087261318](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30087261318), failed in the PB-0013 changed-file-history validator.
- Merge commit: `859a97a83d6328b45e70cd515a058c10bc519205`.
- Successful required `main` workflow: [run 30087267104](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30087267104).
- Explicit user confirmation of the task commit, push, merge, required `main` CI, and completion: 2026-07-24.
- No CI, completion, or quality exception was used.

The optional PR workflow is not described as successful. GitHub checked out a detached synthetic merge commit; in detached HEAD, `git branch --show-current` returns no branch-name line. The changed-file-history validator indexed that empty result and produced `Index was outside the bounds of the array.` The required `main` workflow passed. Pull-request and branch CI are optional under `AGENTS.md`, while required `main` CI succeeded, so no CI exception was needed.

At the beginning of `feat/PB-0101-product-identity`, the approved rollover marks PB-0013 `[x]` / 🟢 **DONE**, removes it from Active Work, records it exactly once in the Completion Log, and records E00/M0 complete.

## Historical PB-0013 Evidence

The historical record is preserved without rewrite or concealment:

- Quality-baseline commit `fc34bffff838cac41198940ed54b91b25c33f838` was committed on the PB-0001 branch, creating the documented historical one-task-per-branch conflict.
- PB-0013 branch commit `a1032c48f2a8d0dc98d0c589f1a845605950952b` was merged through historical pull request [#1](https://github.com/avivperets26/3DModels-Package-Builder/pull/1) as `13e5875b686c3219e3571d45ceaa93c463e881ff`.
- Those commits changed only `AGENTS.md` and the four quality-baseline Markdown sources. They included no application implementation.
- The historical merge did not provide complete task-specific validation, successful-CI evidence, or explicit user completion confirmation, so it did not complete PB-0013.

The current continuation occurs on the correct documented `docs/PB-0013-quality-release-gates` branch after the user fast-forwarded it to current `main` at `f4b5a5d39b2de97e404f837150bbe0d869e3a366`. No history rewrite, alternate PB-0013 branch, or completion exception is used.

## Exact Requirement Inventory

| Group | Exact IDs | Count |
|---|---|---:|
| User experience | UX-001–UX-009 | 9 |
| Testing | TEST-001–TEST-013 | 13 |
| Performance | PERF-001–PERF-008 | 8 |
| Security | SEC-001–SEC-013 | 13 |
| Installation | INSTALL-001–INSTALL-010 | 10 |
| Engineering | ENG-001–ENG-007 | 7 |
| Release blocking | REL-001–REL-008 | 8 |
| **Total** | **68 unique normative IDs** | **68** |

The validator requires this exact ordered inventory, one normative definition per ID, no gaps, no duplicates, no unexpected groups, and the explicit 68-requirement total.

## Threshold and Release-Gate Agreement

- Overall automated coverage remains at least 90% line and 85% branch.
- Security validation, path handling, naming, manifest validation, and package-integrity code remain at 100% branch coverage.
- Critical validation and security mutation thresholds require a measured, explicitly approved baseline; an unapproved surviving high-risk mutant blocks release.
- Percentages and test counts supplement but never replace requirement- and criterion-level evidence.
- The complete deterministic offline portfolio includes unit, contract, integration, end-to-end, UI, regression, installer, upgrade, and failure-recovery tests, with the five product cases, applicable targets, hostile inputs, engine validation, and clean import or reopen evidence.
- REL-001 through REL-008 remain complete and unique in the normative quality document. Other sources use the identical fail-closed summary instead of redefining them.

## E18 Ownership

Every normative requirement maps exactly once to one of the 15 E18 owner tasks:

| Requirement groups | E18 owner tasks |
|---|---|
| UX | PB-1802, PB-1803, PB-1804 |
| TEST | PB-1801, PB-1805, PB-1806, PB-1807 |
| PERF | PB-1808, PB-1809 |
| SEC | PB-1810, PB-1811, PB-1812 |
| INSTALL | PB-1813 |
| ENG | PB-1814 |
| REL | PB-1815 |

Each mapped task exists, names an owner, has concrete dependencies, and has a detailed measurable `Done when` clause that produces test, evidence, report, or fail-closed validation outcomes.

## Deterministic Validator

`scripts/Test-QualityAndReleaseGates.ps1` uses only Windows PowerShell/.NET and read-only Git inspection. It is compatible with Windows PowerShell 5.1 and PowerShell 7 and verifies:

1. The exact 68-definition inventory, valid groups, order, uniqueness, and count.
2. Exact requirement-to-E18 ownership with no unmapped or duplicate ID.
3. E18 owners, dependencies, existing task references, and measurable `Done when` clauses.
4. Coverage, mutation, required test-type, and meaningful-test evidence rules.
5. Exact REL-001 through REL-008 definitions and the shared fail-closed summary.
6. UX/accessibility, security/privacy, installation, containment, free-tooling, Visual Studio Code, and manual Git policy agreement.
7. Markdown structure and contained local links for the PB-0013 sources and evidence.
8. PB task IDs, dependencies, branches, lifecycle markers, PB-0013 completion, and PB-0101 Active Work/Completion Log consistency.
9. PB-0012 rollover, historical PB-0013 evidence, and final PB-0013 publication evidence.
10. Fixed historical PB-0013 changed-file scope plus safe normal-branch and detached-HEAD behavior, without treating successor-task files as PB-0013 changes.
11. Unresolved placeholder language and unsupported quality claims.

The validator is integrated into `scripts/Test-RepositoryBaseline.ps1` in-process and through standalone Windows PowerShell 5.1.

## PB-0013 Branch Validation Results

| Validation | Final local result |
|---|---|
| `scripts/Test-QualityAndReleaseGates.ps1` in portable PowerShell 7.6.4 | Pass; 11 checks passed, 0 failed. |
| `scripts/Test-QualityAndReleaseGates.ps1` in Windows PowerShell 5.1 | Pass; 11 checks passed, 0 failed. |
| Exact requirement inventory | Pass; UX 9, TEST 13, PERF 8, SEC 13, INSTALL 10, ENG 7, REL 8; 68 unique normative definitions total. |
| Cross-document thresholds and release blockers | Pass; 90% line, 85% branch, five critical areas at 100% branch, mutation approval policy, meaningful-test rule, and exact REL-001–REL-008 definitions agree. |
| E18 mapping | Pass; all 68 requirements map exactly once across 15 owned E18 tasks with concrete dependencies and measurable `Done when` clauses. |
| `scripts/Test-ArchitectureDecisionRecords.ps1` | Pass; 8 checks passed, 0 failed. |
| `scripts/Test-ContributionDocumentation.ps1` | Pass; 11 checks passed, 0 failed. |
| `scripts/Test-GitHubGovernance.ps1` | Pass; 9 checks passed, 0 failed. |
| `scripts/Test-RepositoryBaseline.ps1 -RequireTrackedFiles` | Pass; 29 checks passed, 0 failed, including in-process and standalone Windows PowerShell 5.1 quality validation. |
| `scripts/Invoke-CoreCi.ps1` | Pass; all 9 fail-closed stages passed in 1 minute 30 seconds. |
| Exact SDK, restore, and Release build | Pass; repository-local .NET SDK `10.0.302`, locked restore, 15 projects, 0 warnings, 0 errors. |
| Formatting and lint | Pass; `dotnet format --verify-no-changes`, Ruff `0.15.22` lint, and Ruff formatting checks passed. |
| Baseline smoke tests | Pass; 4 discovered, 4 passed, 0 failed, 0 skipped; source-candidate nonmutation check passed. |
| Markdown, local links, task graph, lifecycle, and changed-file scope | Pass on the PB-0013 branch; PB-0012 was rolled over exactly once, PB-0013 remained active before publication, and no application implementation was present. |
| `git diff --check` and `git diff --cached --check` | Pass; no whitespace errors in unstaged or staged changes. |

PowerShell 7 was absent from the host, so the official `PowerShell-7.6.4-win-x64.zip` was downloaded to the ignored repository-local `downloads` root, verified against published SHA-256 `80832551C52809301E6071C8BAC977BEB5A2F1EC953EB4DB9F94DEB953333793`, and expanded beneath the ignored repository-local `tools` root. This validation-only tool installation changed no tracked dependency or system installation.

## Documentation Impact

PB-0013 synchronizes PB-0012 completion evidence, makes the 68-ID authority and fail-closed release summary explicit, records manual Git ownership in the product plan, removes an unsupported vendor quality characterization, updates the ADR lifecycle validator, adds this evidence, and adds the deterministic quality validator to the repository baseline. The PB-0101 rollover adds final publication evidence and corrects detached-HEAD handling without changing any approved threshold, dependency, engine installation, marketplace rule, GitHub setting, or installer selection.

## Completion State

PB-0013 is logically complete and is synchronized `[x]` / 🟢 **DONE** during the PB-0101 rollover. It is absent from Active Work and appears exactly once in the Completion Log. PB-0101 remains `[ ]` / 🟡 **PROCESS** and is not in the Completion Log.
