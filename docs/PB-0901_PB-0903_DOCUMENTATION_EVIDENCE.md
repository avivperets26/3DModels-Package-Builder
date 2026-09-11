# PB-0901–PB-0903 — Shared documentation implementation

Publication update (2026-09-11): task commit `49abdd61838f297df0f73c2c55801382e0a14b3c`
merged through PR #93 as `a7b242bce35285f65d4f83dfffdc6879b1b93b9d`.
[Main CI](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/34578120943)
succeeded and the user confirmed the merge. Each task's DONE state and one Completion Log row
were recorded at the start of PB-0904/PB-0905/PB-0907. Pending-publication notes below are historical.

## Scope and lifecycle

The user explicitly approved all three tasks on `codex/PB-0901-PB-0903-documentation`.
The branch was created only after fetch, main checkout, fast-forward pull and clean equality
with origin/main at `323e915223bf301b806ec220a42effd01cec150d`.
PB-0811 is recorded DONE once following PR #92, successful
[main CI](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/34520660383)
and the user's merge confirmation. These three tasks remain IN PROGRESS through publication gates.

## Acceptance and traceability

| Task / requirements | Implementation | Automated evidence |
| --- | --- | --- |
| PB-0901; ENG-004/005, SEC-002/004/009, TEST-012, PERF-004 | Scriban 7.4.0, embedded template, explicit primitive projection, escaped data, strict UTF-8/LF, bounded input/loop/output, cancellation, per-render context; ADR-0015 | `DocumentationTests`: deterministic bytes, hostile template/include/loop/HTML/link/media strings, Unicode, culture, cancellation and concurrent rendering; lock/build policy and ADR validators |
| PB-0902; SEC-002/006, ENG-003/004 | Exact reference/default selection, bounded catalog, existing JSON Schema and Domain parsing, contained physical files, configured identity/support/copyright/AI/branding | `PublisherProfileResolverTests`: explicit/default selection, ordinal mismatch, duplicate catalog, unsafe/device paths, read/schema/identity failures, branding; `FileConfigurationTextReaderTests`: actual physical configuration load, malformed/oversized input and UTF-8 contract |
| PB-0903; TEST-004/005/012, ENG-004 | Shared contents/formats/versions, measured metre dimensions and triangle/material/texture counts, scale/axes/pivot, engine/pipeline, dependencies, usage, publisher prose; portable overload returns existing document type | `DocumentationTests`: exact supplied data, portable/Unity/Unreal branches, missing/malformed/contradictory facts, immutable snapshots and collection limits; `SharedPortableReadmeTests`: strict UTF-8 in-memory archive round trip; existing portable regression suite |

## Ownership and reuse audit

Reused `ProductManifest`, `PublisherProfile`, `PublisherProfileJson`, `PublisherRoot`, typed
copyright/AI/branding values, `IConfigurationTextReader`, `IReparsePointInspector` and their
Infrastructure adapters. No profile schema or duplicate profile aggregate was introduced.
Common publisher prose moved from the portable renderer into Application and is used by both APIs.
The portable adapter references Application; Domain and Contracts do not reference Scriban.

The new API takes `ReadmeBuildData.Create(...)` output with required measured facts. It does not
launch an engine or infer geometry from manifest materials. Counts describe unique delivered
resources/product geometry and exclude preview scene resources. Callers must provide validated
target evidence. Zero material/texture counts are permitted when explicitly measured; missing
measurements fail. Engine versions are supplied results, not hardcoded supported-version claims.

The legacy PB-0504 overload remains case-aware and source-compatible; it shares publisher prose
but has no new metrics inferred for it. PB-0904 owns the subsequent case-specific template variants
and migration of those callers. The new portable overload produces the same document type used
by archive composition. This preserves earlier behavior while exposing the complete shared API.

The catalog is typed configuration: explicit root-to-repository-relative JSON file entries plus
the configured default reference. Only null selection invokes that default. No publisher name,
copyright owner, clock-based year or no-AI assumption is hardcoded. Branding is returned as typed
logical image references; this scope does not fetch or embed images. Engine/naming defaults stay
with existing target configuration owners, and the publisher-profile UI remains PB-1303.

Relative documentation/configuration path syntax is shared within this scope. Existing archive,
snapshot and naming validators retain their distinct ownership and richer filesystem lifecycles;
cross-owner path-policy consolidation should be assessed with PB-1806 rather than coupling
Application to Infrastructure. Local filesystem checks do not claim process-level sandboxing or
protection against a privileged process concurrently replacing checked paths.

## Validation and artifact cleanup

Validation on 2026-09-10 passed: 2477 tests across seven projects, zero failures/skips, zero-warning Release build, full .NET format verification, Ruff lint/format (51 files), all 35 repository baseline checks (including 9 cleanup checks), and a dependency vulnerability audit reporting no known vulnerable packages. Source-candidate integrity verification passed. The Scriban license was verified in the WPF build output. Compact logs and the audit live under `artifacts/PB-0901`; the full test summary is `artifacts/test-results/PB-0901/summary.json`. Template tests are in memory; the archive round trip is also in memory. File-reader tests
reuse owned disposable workspaces and disposal cleanup. No Blender or Unity test package is needed
for this pure documentation scope, and prior engine evidence is not relabelled as a new run.

## Changed files and publication handoff

Implementation: Application `Documentation/` types and embedded template, Application project and
portable project references, portable renderer, Infrastructure configuration reader; focused tests
in Application, Portable and Infrastructure. Dependency changes: central version, affected lock
files, third-party notices and copied Scriban license. Governance: AGENTS, engineering skill,
branch policy, dependency/architecture validators, ADR inventory/indexes, plan, architecture,
quality mapping, backlog and this evidence document.

Suggested commit: `feat: add typed documentation templates and publisher resolution`.
On 2026-09-11 the user authorized staging, commit and push of this validated scope. Merge remains
pending. The publication commands for this branch are:

```powershell
git diff --check
git status --short
git add AGENTS.md Directory.Packages.props docs scripts skills src tests
git commit -m "feat: add typed documentation templates and publisher resolution"
git push -u origin codex/PB-0901-PB-0903-documentation
```

Merge only after the required validation/publication review. Keep each new task IN PROGRESS until
successful main CI and user confirmation; record each completion once at the next task rollover.

### Reviewable file inventory

- `AGENTS.md`
- `Directory.Packages.props`
- `docs/adr/ADR-0015-typed-documentation-templates.md`
- `docs/adr/README.md`
- `docs/IMPLEMENTATION_BACKLOG.md`
- `docs/licenses/Scriban-LICENSE.txt`
- `docs/Package_Builder_Plan.md`
- `docs/PB-0811_TWELVE_ITEM_COLLECTION_EVIDENCE.md`
- `docs/PB-0901_PB-0903_DOCUMENTATION_EVIDENCE.md`
- `docs/QUALITY_AND_RELEASE_GATES.md`
- `docs/README.md`
- `docs/TECH_STACK_AND_ARCHITECTURE.md`
- `docs/TEST_ARTIFACT_CLEANUP.md`
- `docs/THIRD_PARTY_NOTICES.md`
- `scripts/TaskBranchPolicy.Common.ps1`
- `scripts/Test-ArchitectureDecisionRecords.ps1`
- `scripts/Test-CentralBuildConfiguration.ps1`
- `scripts/Test-RepositoryBaseline.ps1`
- `scripts/Test-SolutionArchitecture.ps1`
- `scripts/Test-TestProjects.ps1`
- `skills/package-builder-engineering/SKILL.md`
- `src/PackageBuilder.App.Wpf/packages.lock.json`
- `src/PackageBuilder.Application/Documentation/DocumentationPath.cs`
- `src/PackageBuilder.Application/Documentation/DocumentationResult.cs`
- `src/PackageBuilder.Application/Documentation/DocumentationTemplateEngine.cs`
- `src/PackageBuilder.Application/Documentation/DocumentationText.cs`
- `src/PackageBuilder.Application/Documentation/PublisherDocumentation.cs`
- `src/PackageBuilder.Application/Documentation/PublisherProfileResolver.cs`
- `src/PackageBuilder.Application/Documentation/ReadmeBuildData.cs`
- `src/PackageBuilder.Application/Documentation/SharedReadmeGenerator.cs`
- `src/PackageBuilder.Application/Documentation/Templates/readme.sbn`
- `src/PackageBuilder.Application/PackageBuilder.Application.csproj`
- `src/PackageBuilder.Application/packages.lock.json`
- `src/PackageBuilder.Cli/packages.lock.json`
- `src/PackageBuilder.Infrastructure/Configuration/FileConfigurationTextReader.cs`
- `src/PackageBuilder.Targets.Portable/PackageBuilder.Targets.Portable.csproj`
- `src/PackageBuilder.Targets.Portable/packages.lock.json`
- `src/PackageBuilder.Targets.Portable/PortableReadmeGenerator.cs`
- `tests/PackageBuilder.App.Wpf.Tests/packages.lock.json`
- `tests/PackageBuilder.Application.Tests/Documentation/DocumentationTests.cs`
- `tests/PackageBuilder.Application.Tests/Documentation/PublisherProfileResolverTests.cs`
- `tests/PackageBuilder.Application.Tests/packages.lock.json`
- `tests/PackageBuilder.Infrastructure.Tests/Configuration/FileConfigurationTextReaderTests.cs`
- `tests/PackageBuilder.Infrastructure.Tests/packages.lock.json`
- `tests/PackageBuilder.Targets.Portable.Tests/packages.lock.json`
- `tests/PackageBuilder.Targets.Portable.Tests/SharedPortableReadmeTests.cs`
