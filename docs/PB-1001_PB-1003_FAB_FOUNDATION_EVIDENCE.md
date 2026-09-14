# PB-1001–PB-1003 — Fab requirements foundation

## Scope and lifecycle

The user approved all three tasks on one branch. `codex/PB-1001-PB-1003-fab-foundation` starts
at freshly fetched/pulled clean main `ea8c727f93836c0a9c1c8d49c5ff27787a6f1113`.
The exact exception is recorded in AGENTS, the engineering skill and tested branch policy.
PB-0911/PB-0912 were rolled to DONE once with publication/main-CI/user-confirmation evidence.
The current three tasks remain IN PROGRESS and are implemented and validated locally. The final
repository baseline passes all 35 checks. Publication and completion gates remain outstanding.
On 2026-09-14 the user explicitly authorized commit, push, merge and deployment. Publication is
underway; the task/merge commits and main CI result will be retained in
`artifacts/PB-1001-publication.json` for the next-task rollover. The user clarified that updating
GitHub/main is sufficient deployment for this scope; no separate app distribution is requested.
Backlog counts: 264 total,
135 DONE, 3 IN PROGRESS, 0 BLOCKED, 126 BACKLOG, 129 remaining (51.1%).

## Acceptance and ownership

| Task | Delivered API / data | Required automated evidence |
| --- | --- | --- |
| PB-1001 | `FabRequirementsProfileJson`, immutable snapshot, embedded reviewed JSON | Six sections, source/date provenance, numeric bounds and unknown clauses, strict fields/duplicates/nulls/limits, order/culture-independent canonical SHA-256 |
| PB-1002 | `FabRequirementsProfileUpdater`, generic repository contract and real SQLite adapter | Cache/current separation, test failures, explicit review, stale-current conflict, immutable revisions, atomic rollback, restart, corruption/cancellation, old build pins |
| PB-1003 | `FabRequiredTargetResolver` | Five cases × four formats, category restrictions, combined outputs, optional companions, invalid selections, unresolved rules |

The focused tests are `FabRequirementsProfileTests` and `FabRequirementsUpdateTests` in the
existing WPF test host, which already references both real adapters transitively. No UI controls,
external engines, asset uploads or extra test projects are needed for this foundation.

## Source review and explicit limits

The baseline was reviewed on 2026-09-12 against these official sources:

- [Fab formats and structure](https://dev.epicgames.com/documentation/en-us/fab/asset-file-format-and-structure-requirements-in-fab).
- [Fab listing workflow](https://dev.epicgames.com/documentation/en-us/fab/publishing-assets-for-sale-or-free-download-in-fab).
- [Fab technical requirements](https://www.fab.com/o/technical-requirements), published update 2026-03-17.
- [Unity submission guidelines](https://assetstore.unity.com/publishing/submission-guidelines), published update 2026-05-20.

`effectiveOn` is Package Builder's local adoption date, explicitly labelled `local-reviewed-adoption`;
it is not an invented official effective date. Source update dates are null when not published.
The technical page's full accordion content could not be retrieved during this review. Its missing
coverage remains `technical-review`, unresolved. The summary's ambiguous `6B` exchange-format
limit remains `other-format-bytes`, with no guessed numeric value. Profile approval does not resolve
either issue; target resolution returns applicable unresolved rules and cannot certify compliance.
Detailed/manual clauses are sourced assertions; downstream validators must not interpret absence
of a numeric check as a pass. Decimal-byte interpretation and README/media defaults are labelled
Package Builder policies. The portable-format list covers implemented FBX/GLB capabilities only;
it does not claim that Fab rejects every other accepted exchange/DCC format.

## Local API workflow

1. Migrate/open a project-contained database through the existing `SqliteDatabaseMigrator`.
2. Create `SqliteRequirementsProfileRepository`, then `FabRequirementsProfileUpdater`.
3. `CacheAsync` imports explicit local JSON. Loading the embedded baseline does not approve it.
4. `LoadCurrentAsync`, `LoadCachedAsync` and `Compare` expose the exact review context. Comparison
   includes all canonical fields; arrays are normalized and shown as complete changed sections.
5. `TestAsync` runs the caller's trusted `IFabRequirementsCompatibilitySuite` against the immutable
   candidate and binds its complete evidence to the captured current digest. The suite must run
   all relevant fixtures and retain its referenced compact evidence; it must not fabricate pass counts.
   Failed evidence is returned for diagnostics and cannot promote. The caller owns failed-run logs.
6. `ApproveAsync` needs an explicit affirmative choice, reviewer, UTC timestamp and passing result.
   Cache content, expected current digest, effective date and evidence time are checked. Promotion
   writes the approved flag, immutable approval receipt and current pointer in one transaction.
7. Record `BuildLockIdentity` in `build.lock`. Its schema-v1 version combines revision and SHA-256.
   Reproduce using `LoadPinnedAsync`; missing, altered or unapproved content fails without fallback.
8. Resolve explicit `FabListingConfiguration` against that selected profile. GLB adds portable output;
   unselected compatible formats remain optional, and docs/media follow recorded companion policy.

The generic database has one immutable revision per marketplace; each Fab revision represents the
asset-listing profile. This scope does not implement multiple independent Fab profile families.
Approval requires retesting after a competing current update. Already approved revisions are never
rewritten or re-promoted; reverting rules requires a new reviewed revision, preserving old build pins.
SQLite operations use bounded local synchronous provider calls behind the repository Task contract,
with a five-second busy timeout and cancellation checks before work and before transaction commit.

## Reuse audit

Marketplace rules stay in the Fab adapter. Generic persistence/evidence contracts contain no Fab
conditions. The shared JSON safeguard is exposed with its existing disposal contract instead of
copying duplicate-property/depth checks. Engine and profile promotions share extracted
`CompatibilityEvidenceValidation`; its count sum uses Int64 to avoid overflow. SQLite path validation
is extracted from the existing engine approval repository. Engine lifecycle transitions remain
unchanged; requirements do not pretend to be installed tools. Existing schema-v2 RequirementsProfiles
and Settings tables, repository results, build.lock v1, ProductCase, BuildTarget and PreviewMediaPolicy
are reused. Fab image policy now derives from the selected versioned profile. No new dependencies.

## Validation and cleanup

Release build passes with zero warnings/errors. Locked restore, full-solution .NET formatting at
severity info, Ruff lint and Ruff formatting (51 files) pass. The complete seven-project suite passes
2,672/2,672 tests, with zero failures/skips and its source-stability check passing. The only later C#
edit renamed a test field and made theory rows typed; the affected 85 cases passed again afterwards.
Those cases comprise 61 profile/resolver tests and 24 updater/SQLite/evidence tests. Evidence is in
`artifacts/test-results/PB-1001/summary.json`, `artifacts/PB-1001/tests/fab-foundation.trx`, and the
scope build/format/Ruff logs. The first repository run passed 33/35 gates; the two ADR gates shared
one missing required distinction sentence. It was restored and both ADR validator invocations
passed all eight checks. The final full baseline rerun passes 35/35 with zero failures, recorded in
`artifacts/PB-1001/repository-baseline-final.log`. Backlog accounting and `git diff --check` pass.

Tests use synthetic JSON and short-lived real SQLite databases beneath the test output root. Every
database from a completed fixture setup is removed in disposal, including failing test bodies; no
test packages or engine projects are created. The initial constructor failure left 15 zero-file GUID
directories; automatic approval review blocked their removal. See the exact exception in
[the cleanup audit](TEST_ARTIFACT_CLEANUP.md). Keep compact logs/TRX/evidence under ignored artifacts
and logs directories.

Suggested commit: `feat: add versioned Fab requirements profiles and target resolution`.
After reviewing local results, the user can stage/commit/push this existing task branch. Main merge,
main CI and explicit confirmation precede DONE rollover at the next task start.

## Changed files and manual publication handoff

The 29 changed files cover the Fab profile/model/loader/updater/resolver/media adapter and project
resource entry, two generic persistence contracts, shared JSON/evidence/path safeguards, the SQLite
repository, two focused test files, the exact combined-branch policy and its tests, AGENTS/engineering
skill, backlog, architecture/plan/quality/ADR/index, prior-scope rollover evidence and cleanup audit.
There are no new packages, project references or generated customer assets in the change set.

Review the changed/new files before staging. These commands are a suggested user-run handoff,
the commands reviewed before the user's 2026-09-14 publication authorization:

```powershell
Set-Location C:\Dev\PackageBuilder
git status --short
git diff --check
git add -- AGENTS.md docs profiles/marketplaces/requirements scripts/TaskBranchPolicy.Common.ps1 scripts/Test-RepositoryBaseline.ps1 skills/package-builder-engineering/SKILL.md src/PackageBuilder.Contracts/Json/JsonInputSafeguards.cs src/PackageBuilder.Contracts/Persistence/CompatibilityEvidenceValidation.cs src/PackageBuilder.Contracts/Persistence/RequirementsProfilePersistence.cs src/PackageBuilder.Infrastructure/Persistence/SqliteRepositoryPath.cs src/PackageBuilder.Infrastructure/Persistence/SqliteRequirementsProfileRepository.cs src/PackageBuilder.Infrastructure/Persistence/SqliteToolVersionApprovalRepository.cs src/PackageBuilder.Marketplaces.Fab tests/PackageBuilder.App.Wpf.Tests/FabRequirementsProfileTests.cs tests/PackageBuilder.App.Wpf.Tests/FabRequirementsUpdateTests.cs
git diff --cached --stat
git commit -m "feat: add versioned Fab requirements profiles and target resolution"
git push -u origin codex/PB-1001-PB-1003-fab-foundation
```

Recheck the reviewable set if other work is added before publication. Exact publication outcomes
belong to the receipt and the next-task rollover; the authorization itself is not success evidence.
