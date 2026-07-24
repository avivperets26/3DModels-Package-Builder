# PB-0012 Initial Architecture Decision Records Evidence

**Task:** PB-0012 — Record initial architecture decisions
**Branch:** `docs/PB-0012-initial-adrs`
**Lifecycle:** 🟢 **DONE**
**Evidence date:** 2026-07-24

## Scope and Current State

PB-0012 records the thirteen decisions already approved by `AGENTS.md`, the product plan, the technology architecture, the implementation backlog, and the quality and release gates. It creates the documentation and ADR indexes, adds a dependency-free ADR validator, and integrates that validator into the repository baseline.

The ADRs have status **Accepted** because the architecture direction is approved. Acceptance records the architecture direction; it does not indicate that implementation is complete. The records do not claim that Blender, Unity, Unreal, packaging, desktop UI, security hardening, marketplace publication, or installer functionality currently exists.

PB-0011 was finalized through the permanent one-merge rollover before PB-0012 implementation. Its final task commit `02491ce01e32559c2b41ce886f5595c286677555` was merged through [pull request #12](https://github.com/avivperets26/3DModels-Package-Builder/pull/12) as `5b37b3c8081d246c03eabe8dc3099b1a99f31ca1`. [PR workflow run 30080298582](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30080298582) and required [main workflow run 30080304495](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30080304495) succeeded. The user explicitly confirmed completion on 2026-07-24, and no CI exception was used.

## Final Publication Evidence

- Final task commit: `335691dcceeaa645231539a2ec83a3dae9db2a3e`.
- Pull request: [#13](https://github.com/avivperets26/3DModels-Package-Builder/pull/13).
- Successful PR workflow: [run 30083665801](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30083665801).
- Merge commit on `main`: `f4b5a5d39b2de97e404f837150bbe0d869e3a366`.
- Successful required `main` workflow: [run 30083674462](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30083674462).
- The user explicitly confirmed the commit, push, merge, required CI, and task completion on 2026-07-24.
- No CI, quality, completion, or other exception was used.

The approved PB-0013 rollover marks PB-0012 `[x]` / 🟢 **DONE**, removes it from Active Work, and records it exactly once in the Completion Log while preserving all earlier implementation and validation evidence below.

## ADR Inventory

| ADR | Decision | Status | Implementation boundary |
|---|---|---|---|
| 0001 | .NET 10 LTS and WPF | Accepted | Stack selected; product UI and application behavior remain backlog work. |
| 0002 | External engine workers | Accepted | Process boundary selected; engine workers remain unimplemented. |
| 0003 | JSON file worker protocol | Accepted | Protocol form selected; contracts and workers remain backlog work. |
| 0004 | Immutable staging and atomic promotion | Accepted | Safety model selected; staging and promotion remain backlog work. |
| 0005 | Latest Approved Stable engine policy | Accepted | Version policy selected; discovery and promotion automation remain backlog work. |
| 0006 | SQLite build history | Accepted | Metadata store selected; schema and repositories remain backlog work. |
| 0007 | Compiled-in adapters for version 1 | Accepted | Extension boundary selected; adapters remain backlog work. |
| 0008 | Marketplace requirements profiles | Accepted | Profile boundary selected; Fab profile and adapter remain backlog work. |
| 0009 | Requirements traceability and release evidence | Accepted | Fail-closed evidence model selected; complete traceability and evaluator remain backlog work. |
| 0010 | Accessible guided dry-run workflow | Accepted | UX requirements selected; WPF workflow remains backlog work. |
| 0011 | Threat model, secrets, and network consent | Accepted | Security and privacy rules selected; controls and suites remain backlog work. |
| 0012 | Quality toolchain and thresholds | Accepted | Free evidence categories and permanent thresholds selected; later PB tasks implement them. |
| 0013 | Installer, portable distribution, and lifecycle safety | Accepted | Lifecycle requirements selected; installer technology remains deferred to PB-1612. |

## Validator Coverage

`scripts/Test-ArchitectureDecisionRecords.ps1` is designed for Windows PowerShell 5.1 and PowerShell 7 without additional modules. It checks:

1. The exact thirteen-file inventory, sequential numbers, unique names, and absence of unexpected ADRs.
2. Exact ADR headings, required ordered sections, valid status values, and valid ISO dates.
3. Placeholder absence and non-empty decision content.
4. Resolution of local Markdown links across the reviewable repository set.
5. Direct links to all thirteen ADRs from both documentation indexes and architecture section 29.
6. The explicit distinction between an accepted decision and completed implementation.
7. Required repository links in every ADR.
8. Permanent containment, free-tooling, Visual Studio Code, engine-licensing, configurable-publisher, public-repository, privacy, security, accessibility, performance, evidence, and manual-Git policies.
9. PB-0013 preservation, PB-0011 rollover consistency, PB-0012 active lifecycle, and the PB-1612 installer-selection boundary.

The validator is invoked in-process and through standalone Windows PowerShell 5.1 by `scripts/Test-RepositoryBaseline.ps1`.

## Validation Results

| Validation | Final local result |
|---|---|
| `scripts/Test-ArchitectureDecisionRecords.ps1` in PowerShell 7 | Pass; 8 checks passed, 0 failed. |
| Standalone Windows PowerShell 5.1 ADR validation | Pass; 8 checks passed, 0 failed. |
| `scripts/Test-ContributionDocumentation.ps1` | Pass; 11 checks passed, 0 failed. |
| `scripts/Test-RepositoryBaseline.ps1 -RequireTrackedFiles` | Pass; 27 checks passed, 0 failed, including in-process and standalone Windows PowerShell 5.1 ADR validation. |
| `scripts/Invoke-CoreCi.ps1` | Pass; all nine fail-closed stages passed. |
| Exact SDK and locked restore | Pass; repository-local .NET SDK `10.0.302`; all projects were up to date under locked restore. |
| Release build | Pass; 15 projects, 0 warnings, 0 errors. |
| Formatting and lint | Pass; `dotnet format --verify-no-changes`, Ruff `0.15.22` lint, and Ruff formatting checks passed. |
| Baseline smoke tests | Pass; 4 discovered, 4 passed, 0 failed, 0 skipped; source-candidate nonmutation check passed. |
| Markdown and ADR links | Pass; all local links resolved, both indexes and architecture section 29 linked the exact thirteen-file ADR inventory. |
| Lifecycle and policy checks | Pass; PB-0011 appears once in the Completion Log and not in Active Work; PB-0012 remains active and absent from the Completion Log; permanent policy assertions passed. |
| `git diff --check` | Pass. |

The results above are the task-branch validation evidence retained from PB-0012. Final publication and completion evidence is recorded separately above.

## Documentation Impact

PB-0012 adds the documentation index, ADR index, thirteen ADRs, task evidence, architecture inventory links, root README link, validator documentation, and lifecycle synchronization required for the task. It changes no product behavior, dependency, engine installation, marketplace rule, GitHub setting, or installer selection.

## Remaining Gates

All PB-0012 acceptance, commit, push, merge, required CI, and explicit user-confirmation gates are satisfied. Its final rollover bookkeeping is recorded on the succeeding PB-0013 branch under the permanent one-merge workflow; no exception was used.
