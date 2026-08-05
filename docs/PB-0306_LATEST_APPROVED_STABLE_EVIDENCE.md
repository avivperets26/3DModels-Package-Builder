# PB-0306 Latest Approved Stable Selection Evidence

**Task:** PB-0306 — Implement Latest Approved Stable selection policy
**Branch:** `feat/PB-0306-version-selection-policy`
**Lifecycle:** 🟡 **PROCESS**
**Evidence date:** 2026-08-05

## Scope

PB-0306 adds a deterministic, side-effect-free selection policy under
`PackageBuilder.Application.Tools`. The policy accepts one PB-0305 official release catalog,
caller-supplied version approval and module evidence, and an optional marketplace-owned version
constraint. It returns an exact official release with either `ApprovedLatest` or `LastKnownGood`
origin, or a typed sanitized expected failure.

The selector never discovers, downloads, installs, launches, persists, or modifies a tool. It
does not fetch marketplace rules, run the compatibility suite, accept a licence, determine vendor
eligibility, or imply that an operator has a qualifying plan, seat, or royalty arrangement.
PB-0307 owns persisted approval transitions and test-result evidence; later marketplace tasks own
versioned requirements profiles.

## Policy Rules

1. Only releases present in the official catalog and parsed as `Stable` are eligible. Alpha,
   beta, preview, experimental, and release-candidate versions remain excluded even when approval
   input incorrectly labels them `ApprovedLatest`.
2. Optional marketplace constraints apply an inclusive minimum, inclusive maximum, and optional
   LTS-only requirement. Invalid cross-tool or inverted constraints fail closed.
3. Every requested module must be present in approval evidence using trimmed, case-insensitive
   module identity. Invalid or duplicate module identifiers fail closed.
4. The newest eligible `ApprovedLatest` version wins by canonical PB-0301 numeric precedence.
5. If none qualifies, the newest eligible `LastKnownGood` wins. Discovered, Installed, Candidate,
   and Rejected records never authorize selection.
6. Duplicate, cross-tool, undefined-state, or malformed approval evidence fails closed rather than
   being silently ignored.

## Public Types

| Type | Purpose |
|---|---|
| `LatestApprovedStableSelectionPolicy` | Evaluates validated catalog, approval, module, and marketplace inputs without I/O. |
| `LatestApprovedStableSelectionRequest` | Groups all deterministic policy inputs. |
| `ToolVersionApproval` / `ToolVersionApprovalState` | Carries the six documented lifecycle states and module evidence without claiming persistence. |
| `MarketplaceToolVersionConstraint` | Provides generic tool-matched inclusive bounds and optional LTS requirement. |
| `LatestApprovedStableSelection` / `ToolVersionSelectionOrigin` | Returns the exact release and whether approval or fallback authorized it. |
| `ToolVersionSelectionResult` / `ToolVersionSelectionError` | Represents success or a stable sanitized expected failure. |

## Acceptance and Requirements Mapping

| Criterion or requirement | Automated evidence |
|---|---|
| Preview versions are excluded by default | `SelectsNewestApprovedStableInsteadOfHighestCatalogVersion` |
| Marketplace constraints apply | `MarketplaceRangeAndLtsRequirementConstrainSelection`; invalid-constraint theory |
| Newest approved stable wins | `SelectsNewestApprovedStableInsteadOfHighestCatalogVersion` |
| Last Known Good is used when no Approved Latest version qualifies | `FallsBackToNewestLastKnownGoodWhenNoApprovedLatestIsEligible`, `RequiredModulesMustExistInApprovalEvidence` |
| Required modules and malformed inputs fail closed (ENG-004) | module, approval, constraint, and no-eligible-result tests |
| Pure policy remains deterministic and offline (TEST-012, ENG-003) | complete focused unit suite; production code has no I/O or adapter dependency |

## Current Validation Results

| Validation | Current result |
|---|---|
| Focused PB-0306 tests | Pass; 14 passed, 0 failed, 0 skipped. |
| Complete Application suite | Pass; 98 passed, 0 failed, 0 skipped. |
| Complete five-project test portfolio | Pass; 1,989 passed, 0 failed, 0 skipped: Domain 846, Application 98, Infrastructure 628, Contracts 402, WPF 15. |
| Debug and Release solution builds | Pass; 16 projects, 0 warnings, 0 errors in each configuration. |
| Repository baseline and full local Core CI | Pass; baseline 29/29 and all 9 Core CI stages passed in 4 minutes 11 seconds. |
| Formatting and prohibited-content checks | Pass; strict information-level .NET formatting, Ruff lint/formatting, `git diff --check`, and repository public-content checks succeeded. |
| Coverage instrumentation | Unresolved evidence gap; Coverlet 10.0.1 passed all 98 Application tests but emitted a report with zero instrumented points, so no coverage percentage is claimed. |

## Licensing Boundary

.NET, Blender, Unity, and Unreal remain externally licensed products. Selection does not grant
rights, establish eligibility, assign seats, accept terms, or calculate royalties. Unity operators
must satisfy Unity's applicable legal and plan/seat conditions; Unreal operators must satisfy the
Unreal Engine EULA's applicable eligibility, seat-subscription, and royalty conditions. Blender
remains GPL software, and .NET remains subject to Microsoft's licensing. Package Builder keeps
these conditions outside its version precedence logic.

## Manual Visual Test

Not applicable. PB-0306 changes Application policy and unit tests only and does not modify WPF.
The PB-1301 shell remains the current visual checkpoint.

## Remaining Gates

- Resolve or explicitly disposition the invalid coverage-instrumentation evidence before claiming
  the detailed coverage gate.
- User stages and commits PB-0306 on `feat/PB-0306-version-selection-policy`.
- User pushes the task branch and merges it into `main` through an optional PR or approved direct
  merge.
- Required `main` CI succeeds for the merge commit.
- User explicitly confirms completion.
- PB-0306 remains `[ ]` / 🟡 **PROCESS** and absent from the Completion Log until the PB-0307
  rollover records those gates.
