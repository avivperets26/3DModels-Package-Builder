# PB-0015 — JSON Schema source distribution evidence

Status: 🟡 **IN PROGRESS** — implemented and validated locally; publication gates remain.
Branch: `chore/PB-0015-json-schema-distribution`.

## Decision and scope

Compile JsonSchema.Net 9.4.0, JsonPointer.Net 7.0.2 and Json.More.Net 3.0.1 from the same
MIT source commit, `399f198431f65cf6896fe6038f833ef6d0b27a39`. Remove all three publisher
binary packages from restore graphs. Keep Humanizer.Core 3.0.10, whose package declares MIT
without licence acceptance, centrally pinned and hash-locked.

PB-0014 found conditional maintenance-fee terms in the existing JSON binaries, not just the
proposed update. The [pinned source licence](../third_party/json-everything/upstream/LICENSE)
and [upstream EULA](https://github.com/json-everything/json-everything/blob/399f198431f65cf6896fe6038f833ef6d0b27a39/OSMFEULA.txt)
distinguish independent source compilation from publisher binary distribution. The selected
workflow depends on MIT source permission, with its notice preserved, rather than any assumed
revenue threshold or paid subscription.

| Option reviewed | Result |
| --- | --- |
| Continue with publisher NuGet binaries | Does not establish the required unconditional no-subscription distribution path. |
| Independently compile pinned MIT source | Selected. Initial three-library spike compiled on .NET 10.0.302 with zero warnings/errors. Preserves existing Json.Schema APIs and contract implementations. |
| Replace with Apache-2.0 Corvus.JsonSchema dynamic validation | Plausible alternative, but its dynamic-validator path introduces runtime Roslyn compilation and API migration. No performance comparison is claimed. Unnecessary migration risk given the successful source-build path. |

Corvus reference: [upstream repository](https://github.com/corvus-dotnet/Corvus.JsonSchema).
The source/archive hashes, import boundaries, reviewed build differences, update ownership and
deployment commands are recorded in the [distribution README](../third_party/json-everything/README.md).

## Acceptance traceability

| Acceptance requirement | Validation |
| --- | --- |
| Review transitive binary terms and provide no-subscription workflow | Pinned MIT licence, upstream EULA review, Humanizer nuspec review, source-only project graph and distribution notices. |
| Reproducible source distribution | `Test-JsonSchemaDistribution.ps1` checks 181 source/resource/licence hashes and exact provenance; clean independent builds compare assembly hashes. |
| Reject accidental publisher binary regression | Distribution validator inspects all 21 lock files, project PackageReferences and central versions; six negative-fixture cases in `Test-JsonSchemaDistributionGuards.ps1`. |
| Preserve Draft 2020-12 behavior and diagnostics | Nine `JsonSchemaDistributionTests` cases cover local references, allOf/unevaluatedProperties, prefixItems/items, required/type/length rules, errors and unknown remote references; existing contract tests cover all five product cases and worker/profile/preview/build-lock schemas. |
| Offline validation and locked restore | Default registry returns no remote document; unresolved reference fails during evaluation. Full core CI uses locked restore; clean source build uses an isolated package cache. |
| Deployment and notices | CLI publish inspection checks three independently compiled DLLs and MIT notice/provenance/README propagation. |
| Preserve project quality gates | Full `Invoke-CoreCi.ps1`, including baseline, locked restore, warning-free build, formatting, Ruff and all seven test projects. |

## Reuse and duplication audit

Searched existing Contracts serializers, schema registries, project inventories, baseline validators,
formatting entry points and dependency notices before changes. Reused every production schema
and contract implementation; no application validation rule or serializer was copied. The three
vendor projects are an intentional upstream source import, isolated from application policy.
Only the reviewed upstream compile/resource exclusions and assembly identity metadata are
repeated in the local build wrappers; dependency-maintenance engineering owns synchronization.
The existing central-build and architecture validators now cover an exact 21-project graph.
Application analyzer policy remains unchanged; vendor formatting is guarded by hashes.

## Local evidence and remaining gates

Validation ran on Windows with the pinned .NET SDK 10.0.302. Evidence is retained under ignored
`artifacts/PB-0015`; the results below are recorded here for review after generated logs are absent.

- Focused conformance/offline tests: 9 passed, 0 failed, 0 skipped.
- Full `Invoke-CoreCi.ps1`: all nine stages passed in 3 minutes 44 seconds. Repository baseline:
  34 passed, 0 failed. Locked restore, Release build (0 warnings/errors), .NET formatting and
  Ruff lint/format verification passed. All seven test projects passed: 2,329 tests, 0 failed,
  0 skipped (Domain 884; Application 130; Infrastructure 647; Contracts 438; WPF 15;
  Portable 201; Unity 14). The nine focused cases are included in that total.
- Source distribution guards: 181 imported files verified; all 21 lock graphs exclude publisher
  binaries. Six negative cases passed both in PowerShell 7 and standalone Windows PowerShell 5.1.
- Clean source restore used a newly created package cache containing only Humanizer.Core.
  Independent source builds in different directories produced byte-identical DLLs, zero warnings
  and zero errors. SDK Git/SourceLink inference is explicitly disabled for these vendor projects;
  otherwise the enclosing checkout's revision metadata changes portable-PDB identity and DLL hashes.
- CLI publish carried all three libraries as project dependencies, not publisher packages.
  The published MIT licence, manifest and README matched their source hashes; the current CLI
  entry point exited 0. That entry point is a skeleton, so this is deployment/assembly evidence,
  not a claim that a complete product workflow or installer was exercised.
- NuGet vulnerability audit inspected 21 project graphs and returned zero known vulnerable
  package records. This audit covers NuGet dependencies; it is not a vulnerability guarantee for
  independently compiled source. Future source updates require upstream advisory and licence review.

| Independently built DLL | SHA-256 (Release, .NET SDK 10.0.302) |
| --- | --- |
| Json.More.dll | `5b9aaacaf440ea3672d044c3d9a7422792db82a850185ff53843ceb0bd973c0c` |
| JsonPointer.Net.dll | `03025e1807e30a7d24278f0a5c6ca1ad06758e5ce23f49f0a2e71905340f9d5c` |
| JsonSchema.Net.dll | `2063ac442e2d40cf5ba8ff44da178124df61b5c2ada758c5e7a77a65ed7e5ca2` |

Publication, main CI and user confirmation have not occurred for PB-0015. It remains unchecked
and IN PROGRESS. PR #79's publisher-binary proposal remains superseded in design, but should
only be closed after the replacement is published under the user's Git authorization.

## Changed scope and handoff

Changed files include the three source projects and 181 upstream source/resource/licence files
under `third_party/json-everything`, their manifest/build properties/README/lock files,
`PackageBuilder.sln`, `Directory.Packages.props`, the Contracts project and 16 affected application/test
lock files. `JsonSchemaDistributionTests.cs` adds the focused tests. The two new distribution
validation scripts integrate with `Test-RepositoryBaseline.ps1`; central-build, architecture,
test-project, formatting and contribution-documentation validators reflect the new exact graph
and vendor boundary. `Invoke-CoreCi.ps1`, `Test-Formatting.ps1` and `.gitattributes` preserve
upstream whitespace without changing application style rules.

README, CONTRIBUTING, architecture, third-party notices and this evidence explain the workflow.
Backlog Active Work reflects PB-0015; the Completion Log records PB-0014 once through the approved
rollover. Summary: 264 total, 111 DONE, 2 IN PROGRESS, 0 BLOCKED, 151 BACKLOG, 153 remaining (42.0%).
PB-0714's existing lifecycle state is preserved.

Suggested/current branch: `chore/PB-0015-json-schema-distribution`.
Suggested commit: `chore(PB-0015): build JSON validation libraries from pinned MIT source`.
No implementation blocker remains; local validation passed and the normal publication gates
remain. No stage, commit, push, merge or PR mutation is authorized by this handoff.

After reviewing the diff and authorizing publication, the user can run these commands from the
repository root. Review each selected file before committing; main merge/CI remains a separate gate.

```powershell
git diff --check
git status --short
git add -- .gitattributes CONTRIBUTING.md Directory.Packages.props PackageBuilder.sln README.md docs/IMPLEMENTATION_BACKLOG.md docs/TECH_STACK_AND_ARCHITECTURE.md docs/THIRD_PARTY_NOTICES.md docs/PB-0015_JSON_SCHEMA_DISTRIBUTION_EVIDENCE.md
git add -- scripts/Invoke-CoreCi.ps1 scripts/Test-CentralBuildConfiguration.ps1 scripts/Test-ContributionDocumentation.ps1 scripts/Test-Formatting.ps1 scripts/Test-FormattingConfiguration.ps1 scripts/Test-RepositoryBaseline.ps1 scripts/Test-SolutionArchitecture.ps1 scripts/Test-TestProjects.ps1 scripts/Test-JsonSchemaDistribution.ps1 scripts/Test-JsonSchemaDistributionGuards.ps1
git add -- third_party/json-everything src/PackageBuilder.Contracts/PackageBuilder.Contracts.csproj tests/PackageBuilder.Contract.Tests/Validation/JsonSchemaDistributionTests.cs
git add -- ':(glob)src/**/packages.lock.json' ':(glob)tests/**/packages.lock.json'
git diff --cached --check
git commit -m "chore(PB-0015): build JSON validation libraries from pinned MIT source"
git push -u origin chore/PB-0015-json-schema-distribution
```
