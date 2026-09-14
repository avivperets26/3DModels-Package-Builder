# PB-0016 — Runtime and test dependency refresh

## Scope and lifecycle

DONE, recorded at PB-1004/PB-1005/PB-1007 start. Implemented on `codex/PB-0016-dependency-refresh`, created on 2026-09-14 after fetch,
fast-forward pull and verification that clean main equals origin/main at
`05371f0e752ca931e26adc46f4d960ed65ae03de`. The user approved this maintenance scope.
PB-1001–PB-1003 roll to DONE once here using their recorded task/merge/main-CI evidence
and the user's acceptance and continuation. PB-0016 remains unchecked until publication,
main CI, user confirmation and subsequent rollover. No publication is claimed by local checks.


## Publication and completion rollover

Task commit `d11ccca72049edb410a7beabd126da643ddf87c2` was pushed and merged/pushed as
`7bbfc38bf4abddf56807881e953e7ce349de9155`. Both jobs in
[main CI](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/34888255159)
passed. Dependabot PRs #94–#97 were closed as superseded and verified by readback.
The user accepted the result and approved the next combined scope on 2026-09-14; this
next-task branch records PB-0016 DONE once. The local receipt is artifacts/PB-0016-publication.json.
Earlier lifecycle/handoff text below records the pre-publication state and is historical.

## Review decisions

Dependabot PRs #94–#97 fail the baseline's older exact-version approval expectations;
their application CI jobs were skipped. That establishes a pin-policy mismatch, not
application compatibility. Preserve those checks and update their reviewed values with
the central package pins and NuGet-generated lockfiles.

| PR | Package | Before | Candidate | Review |
| --- | --- | --- | --- | --- |
| [#94](https://github.com/avivperets26/3DModels-Package-Builder/pull/94) | Microsoft.Data.Sqlite | 10.0.11 | 10.0.12 | Managed provider patch; [official package](https://www.nuget.org/packages/Microsoft.Data.Sqlite/10.0.12). Exercise real SQLite repositories, migrations, transactions and backup. |
| [#95](https://github.com/avivperets26/3DModels-Package-Builder/pull/95) | Microsoft.Extensions.Hosting | 10.0.11 | 10.0.12 | Host composition patch; [official package](https://www.nuget.org/packages/Microsoft.Extensions.Hosting/10.0.12). Exercise WPF composition and lifetime. |
| [#96](https://github.com/avivperets26/3DModels-Package-Builder/pull/96) | Microsoft.NET.Test.Sdk | 18.9.0 | 18.10.0 | [Upstream release](https://github.com/microsoft/vstest/releases/tag/v18.10.0); retain explicit VSTest mode and verify discovery/execution/TRX. |
| [#97](https://github.com/avivperets26/3DModels-Package-Builder/pull/97) | xunit.runner.visualstudio | 3.1.5 | 4.0.0 | [Upstream release](https://xunit.net/releases/visualstudio/4.0.0); major adapter update, validated separately from the framework. Retain xunit.v3.mtp-off 3.2.2 and verify all seven test inventories. |

The native SQLite pin remains 3.53.3. This scope makes no claim of a native engine
upgrade or a particular security fix. JsonSchema.Net remains independently compiled MIT
source at 9.4.0; obsolete binary-update PR #79 must not replace that distribution path.
No test-framework migration or production logic is introduced.

Restored candidate nuspecs declare MIT for both Microsoft runtime packages and the test SDK,
and Apache-2.0 for the xUnit adapter. The adapter supports xUnit v1/v2/v3 and .NET 8 or later;
its version number does not require migrating our framework to v4. The Test SDK's upstream
changes remove Mono fallback and experimental test sessions and disable the MTP testhost by
default. Our .NET 10/VSTest configuration needs no migration for those changes. SQLite and
Test SDK metadata retain their licence-acceptance flag under MIT; no paid dependency is added.
Reviewed metadata and source commit identities are in `artifacts/PB-0016/package-metadata.json`.
The complete changed dependency closure has 34 MIT packages and one Apache-2.0 package;
see `artifacts/PB-0016/transitive-license-review.json` for each resolved version.

## Validation

- Runtime-only stage: restore succeeded; 651 infrastructure and 131 WPF tests passed,
  with zero failures/skips, before either test-tool update.
- Test SDK stage: restore succeeded; all 131 WPF tests passed using the prior adapter.
- Final set: locked restore and Release solution build passed with zero warnings/errors.
  All 2,672 tests passed across seven projects with zero failures/skips. Per-project discovery
  matches the PB-1001 baseline exactly: Domain 908, Application 254, Infrastructure 651,
  Contracts 493, WPF 131, Portable 221, Unity 14. Source-stability verification passed.
- Full-solution .NET formatting at severity info passed. Ruff lint and format passed (51 files).
- All four candidate package signatures passed `dotnet nuget verify --all`.
- NuGet's configured source reported no known vulnerable direct/transitive packages across
  all 21 solution projects on 2026-09-14. This is a dated advisory check, not a security guarantee.
- Ten lockfiles have semantic changes. The 35 changed resolved package versions belong to
  the reviewed runtime and test dependency closures. SQLitePCLRaw and xunit.v3.mtp-off pins
  are unchanged. Restored lockfiles were normalized to repository LF before validation.
- Repository baseline: 35/35 passed, including both supported PowerShell invocation modes,
  Markdown/links, exact Completion Log, branch policy, Git integrity and nine cleanup cases.
  Backlog accounting and final diff review passed. Recognizable secret-pattern review found
  none in changed lines; changed/new files contain no generated packages, credentials or tools.

Compact evidence: `artifacts/PB-0016/{runtime-infrastructure,runtime-wpf,sdk-wpf,build,format,
ruff-check,ruff-format,full-tests,signatures,repository-baseline}.log`, `vulnerabilities.json`,
`resolved-version-changes.json`, `test-inventory-comparison.json`, and staged TRX under `tests`.
The full deterministic summary is `artifacts/test-results/PB-0016/summary.json`.

| Acceptance | Existing checks |
| --- | --- |
| Exact pins and transitive resolution | Test-CentralBuildConfiguration, Test-TestProjects, NuGet locked restore |
| Runtime compatibility | Infrastructure and WPF suites, before test tool upgrades |
| Test SDK / adapter compatibility | Staged WPF run and full seven-project Test-BaselineUnitTests with TRX count comparison |
| Repository consistency | Test-RepositoryBaseline, Release build, .NET formatter, Ruff lint/format, backlog accounting |
| Provenance and dependency review | Restored nuspec licence metadata, dotnet nuget verify --all, transitive vulnerability audit |

## Reuse, cleanup and remaining work

Reuse the existing central package mechanism, independent approval maps, locked restore,
CI entry points and database/host tests. The two existing maps independently validate build
configuration and test-project structure; no third map or duplicate production rule is added.
Existing tests cover this dependency-only change; no implementation-mirroring tests are added.

Current state: validated locally, with no implementation blocker. On 2026-09-14 the user
authorized commit, push and merge of this reviewed scope. Publication is underway; exact
task/main commits, main CI and PR reconciliation will be recorded in the ignored receipt
`artifacts/PB-0016-publication.json` for next-task rollover. Backlog Active Work and the prior three Completion Log rows agree:
265 total, 138 DONE, 1 IN PROGRESS, 0 BLOCKED, 126 BACKLOG, 127 remaining (52.1%).

Generated test packages and temporary engine projects must be cleaned by their existing
fixtures. Preserve source/golden fixtures and compact evidence. The historical 15 empty
PB-1001 directories remain the separately documented automatic-review cleanup exception in
[the cleanup audit](TEST_ARTIFACT_CLEANUP.md); this scope does not retry the rejected action.
Post-test readback found zero new PB-1001 directories and zero files. The baseline's nine
cleanup cases also removed their synthetic disposable outputs beneath `artifacts/u/7759de2e`.

PRs #94–#97 stay open until this replacement scope is published. After verifying authenticated
owner `avivperets26`, the exact repository, PR number/author/head and the existing source reference,
obsolete [PR #79](https://github.com/avivperets26/3DModels-Package-Builder/pull/79) was closed
without merge on 2026-09-14 at 18:48:04Z. Readback confirmed closed, merged=false and unchanged
head `a2583834a546d7145415a60aa89f1601c3741b20`; local receipt: `artifacts/PB-0016/pr79-closure.json`.
Publication, main CI and user confirmation remain outstanding; there is no separate deployment destination.

## Handoff

Changed files cover central pins, generated lockfiles, two existing approval maps, exact
branch policy and its checks, AGENTS/engineering skill, backlog, prior-scope rollover,
current architecture/plan/quality/notices/index and this evidence/cleanup audit.

Suggested commit: `chore: refresh runtime and test dependencies`.
After reviewing the final changed-file inventory, the user can run:

```powershell
Set-Location C:\Dev\PackageBuilder
git status --short
git diff --check
git add -- Directory.Packages.props AGENTS.md docs scripts/TaskBranchPolicy.Common.ps1 scripts/Test-RepositoryBaseline.ps1 scripts/Test-CentralBuildConfiguration.ps1 scripts/Test-TestProjects.ps1 skills/package-builder-engineering/SKILL.md ':(glob)**/packages.lock.json'
git diff --cached --stat
git commit -m "chore: refresh runtime and test dependencies"
git push -u origin codex/PB-0016-dependency-refresh
```

Recheck the reviewed file set before running these commands. Main publication and its CI
must follow before claiming completion; these are instructions, not executed actions.
