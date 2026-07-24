# PB-0102 Product Cases and Targets Evidence

**Task:** PB-0102 — Implement product-case and target models
**Branch:** `feat/PB-0102-product-cases-targets`
**Lifecycle:** 🟢 **DONE**
**Evidence date:** 2026-07-24

## Scope

PB-0102 adds the closed domain identities required to distinguish all five approved product cases and the Portable, Unity, and Unreal build-target families. The implementation remains entirely in `PackageBuilder.Domain`, preserves the PB-0101 naming API, and adds no engine, adapter, marketplace, WPF, filesystem, persistence, serialization, or networking dependency.

The task also performs the approved PB-0101 rollover and makes the permanent PB-0012/PB-0013 validators durable. Current task lifecycle is enforced generically by `Test-RepositoryBaseline.ps1` instead of hard-coding a moving domain-task successor in foundation validators.

## Final Publication and Rollover

- Final task commit `16d89bddcac6d26680a20bd7a30956fde1d09dd2` was pushed on `feat/PB-0102-product-cases-targets`.
- The task was merged through [pull request #16](https://github.com/avivperets26/3DModels-Package-Builder/pull/16) into `main` as `0ac28fbc61b7f5287c4161b1329b50df19dd7e22`.
- [PR workflow run 30092231887](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30092231887) completed successfully.
- Required [main workflow run 30092238172](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30092238172) completed successfully for the merge commit.
- The user explicitly confirmed the task commit, push, merge, successful required `main` CI, and completion on 2026-07-24.
- No CI, completion, or quality exception was used.
- The PB-0103 rollover marks PB-0102 `[x]` / 🟢 **DONE**, removes it from Active Work, and records it exactly once in the Completion Log.

## Public API

| API | Purpose |
|---|---|
| `ProductCase.Static` | Model without a rig and without animation. |
| `ProductCase.Rigged` | Model with a rig but without animation. |
| `ProductCase.RiggedAnimated` | Model with a rig and animation. |
| `ProductCase.ItemSet` | Related items intended to form a coordinated or assembled set. |
| `ProductCase.ItemCollection` | Multiple independently usable items grouped as a collection. |
| `ProductCase.All` | Read-only registry in stable canonical order. |
| `ProductCase.TryParse(string?)` | Parses an exact canonical identifier into a case singleton. |
| `BuildTarget.Portable` | Engine-independent FBX/GLB packaging identity. |
| `BuildTarget.Unity` | Unity target identity without Unity settings or dependencies. |
| `BuildTarget.Unreal` | Unreal target identity without Unreal settings or dependencies. |
| `BuildTarget.All` | Read-only registry in stable canonical order. |
| `BuildTarget.TryParse(string?)` | Parses an exact canonical identifier into a target singleton. |
| `CanonicalIdentifierParseResult<T>` | Returns `IsValid`, nullable `Value`, and a specific expected-input error. |

All value constructors are private. Callers can use only the declared immutable singleton instances, so no undefined numeric enum value or unregistered case/target can be retained. No enum or public numeric conversion boundary exists.

## Product-Case Definitions

| Product case | Canonical identifier | Preserved distinction |
|---|---|---|
| Static | `static` | Model without a rig and without animation. |
| Rigged | `rigged` | Model with a rig but without animation. |
| RiggedAnimated | `rigged-animated` | Model with a rig and animation. |
| ItemSet | `item-set` | Related items intended to form a coordinated or assembled set. |
| ItemCollection | `item-collection` | Multiple independently usable items grouped as a collection. |

Set and collection identity describes product grouping only. PB-0102 does not assume their later item manifests lack rigs or animations.

## Build-Target Definitions

| Build target | Canonical identifier | Boundary |
|---|---|---|
| Portable | `portable` | Engine-independent FBX/GLB packaging. |
| Unity | `unity` | Domain identity only; no Unity API or target setting. |
| Unreal | `unreal` | Domain identity only; no Unreal API or target setting. |

Targets are not marketplaces. No target is coupled to Fab, publisher identity, marketplace profiles, or engine-specific settings.

## Parsing and Invalid Input

Parsing is exact ordinal and case-sensitive. It does not trim, normalize, case-fold, or replace separators. Supported canonical identifiers use lowercase ASCII words separated by one hyphen.

`CanonicalIdentifierParseError` makes normal invalid input explicit:

| Error | Behavior |
|---|---|
| `None` | Parsing succeeded and `Value` contains the canonical singleton. |
| `Null` | Input was null. |
| `Empty` | Input was empty. |
| `WhitespaceOnly` | Input contained only whitespace. |
| `Malformed` | Input used leading/trailing whitespace, uppercase, non-ASCII text, punctuation, an underscore, a path separator, or invalid hyphen placement. |
| `Unknown` | Input was well formed but unsupported or misspelled. |

Failure results have `IsValid == false`, a null `Value`, and the exact error. Expected invalid input does not require exception handling.

## Determinism, Equality, and Immutability

- `ProductCase.All` order is Static, Rigged, RiggedAnimated, ItemSet, ItemCollection.
- `BuildTarget.All` order is Portable, Unity, Unreal.
- Parsing and equality use `StringComparison.Ordinal`.
- Hash codes use stable ordinal FNV-1a over the canonical identifier and do not depend on culture or randomized runtime string hashing.
- Both registries are read-only and reject mutation.
- All 15 product-case/build-target pairs remain representable; PB-0102 adds no combination restriction.

A separate target-selection aggregate is intentionally not added. PB-0102 needs target identities, while selection policy and manifest serialization belong to later tasks. This avoids inventing duplicate policy or engine settings before their owning tasks.

## Test and Coverage Matrix

Focused xUnit v3 public-behavior tests cover:

- All five cases and all three targets.
- Exact canonical identifiers and stable ordering.
- Valid parsing and canonical singleton reuse.
- Null, empty, whitespace-only, unknown, misspelled, path-like, malformed, and non-ASCII identifiers.
- Explicit case-sensitive behavior.
- Ordinal equality, null/type-safe equality, stable hashing, and Turkish-culture behavior.
- Static versus rigged versus rigged-animated identity.
- Item set versus item collection identity.
- Portable versus Unity versus Unreal identity.
- Read-only closed registries.
- Every one of the 15 product-case/target combinations.
- Success and failure behavior of `CanonicalIdentifierParseResult<T>`.

The focused suite currently passes 53/53 tests.

| Production file | Line coverage | Branch coverage |
|---|---:|---:|
| `CanonicalIdentifierParseResult.cs` | 100% | 100% |
| `CanonicalIdentifierParser.cs` | 100% | 100% |
| `ProductCase.cs` | 100% | 100% |
| `BuildTarget.cs` | 100% | 100% |

Coverage uses the centrally pinned `coverlet.collector` `10.0.1` and `ExcludeAssembliesWithoutSources=None` for deterministic PathMap assemblies. Reports remain beneath ignored `artifacts/PB-0102`.

## Engine-Independence Evidence

- `PackageBuilder.Domain.csproj` has no `ProjectReference` or `PackageReference`.
- Production source references only base-class-library namespaces and `PackageBuilder.Domain` namespaces.
- Product-case and target files do not reference `PackageBuilder.Targets.Portable`, `PackageBuilder.Targets.Unity`, `PackageBuilder.Targets.Unreal`, Blender, WPF, persistence, filesystem, networking, or marketplace types.
- Portable, Unity, and Unreal remain distinct target identities without adding adapter behavior.
- Every case/target combination is tested within the Domain test project alone.

## PB-0101 Rollover Evidence

- Final PB-0101 task commit: `915dda5d7cd6b93b741841336c4e06aea4ad99ef`.
- Pull request: [#15](https://github.com/avivperets26/3DModels-Package-Builder/pull/15).
- Successful PR workflow: [run 30089954442](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30089954442).
- Merge commit: `67d8884799a99bcfd5e1407fff534561206424d9`.
- Successful required main workflow: [run 30090184878](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30090184878).
- The user explicitly confirmed completion on 2026-07-24.
- No exception was used.

The backlog marks PB-0101 `[x]` / 🟢 **DONE**, removes it from Active Work, and records it exactly once in the Completion Log. PB-0102 remains `[ ]` / 🟡 **PROCESS**, appears in Active Work, and remains absent from the Completion Log.

## Durable Foundation Validators

`Test-QualityAndReleaseGates.ps1` and `Test-ArchitectureDecisionRecords.ps1` continue to validate exact PB-0012 and PB-0013 completion and publication history without naming PB-0102 or a later moving successor. `Test-RepositoryBaseline.ps1` generically requires the Active Work table to match all and only tasks marked 🟡 **PROCESS** or 🔴 **BLOCKED**, rejects duplicates, and requires the Completion Log to match all and only checked 🟢 **DONE** tasks.

The fixed historical PB-0013 commit list and detached-HEAD empty-branch handling remain intact. Validation covers the normal task branch, a `main` checkout, and detached HEAD.

## Explicitly Deferred Work

- PB-0103 owns source assets, complete texture roles, colour space, and normal conventions.
- PB-0105 owns rig and animation definitions.
- PB-0106 owns set and collection item manifests, item relationships, and per-item rig/animation details.
- PB-0107 owns publisher and marketplace profile models.
- PB-0108 owns build jobs and target requests.
- PB-0110 owns product-manifest JSON schemas, serialization, and converters.
- PB-0906 owns preview presentation settings.
- PB-1003 owns Fab required-target resolution.
- Engine-specific target settings and adapter behavior remain in their Portable, Unity, and Unreal tasks.

PB-0102 adds no marketplace profile, Fab coupling, publisher default, engine setting, JSON schema/converter, filesystem/persistence behavior, WPF model, target-selection aggregate, or premature PB-0103 through PB-0110 implementation.

## Validation Results

| Validation | Result |
|---|---|
| Focused PB-0102 tests | Pass; 53 passed, 0 failed, 0 skipped. |
| Complete `PackageBuilder.Domain.Tests` | Pass; 161 passed, 0 failed, 0 skipped. The previous 108 tests remain compatible and pass alongside 53 PB-0102 tests. |
| Coverage for every new production file | Pass; 100% line and 100% branch coverage. |
| `Test-SolutionArchitecture.ps1` | Pass; 7 checks passed, 0 failed across the exact 15-project inventory. |
| Domain project/source dependency scan | Pass; zero Domain project/package references and no engine, adapter, WPF, persistence, filesystem, marketplace, or networking reference in PB-0102 production source. |
| `Test-QualityAndReleaseGates.ps1` — normal branch, PowerShell 7 and Windows PowerShell 5.1 | Pass in both shells; 11 checks passed, 0 failed per run. |
| `Test-QualityAndReleaseGates.ps1` — `main`, PowerShell 7 and Windows PowerShell 5.1 | Pass in both shells; 11 checks passed, 0 failed per run. |
| `Test-QualityAndReleaseGates.ps1` — detached HEAD, PowerShell 7 and Windows PowerShell 5.1 | Pass in both shells; 11 checks passed, 0 failed per run. |
| `Test-ArchitectureDecisionRecords.ps1` — normal, `main`, and detached HEAD in both shells | Pass in all six runs; 8 checks passed, 0 failed per run. |
| `Test-RepositoryBaseline.ps1 -RequireTrackedFiles` | Pass; 29 checks passed, 0 failed, including in-process and standalone Windows PowerShell validation. |
| `Invoke-CoreCi.ps1` | Pass; all 9 fail-closed stages passed. |
| Release solution build | Pass; 15 projects, 0 warnings, 0 errors. |
| Core test projects | Pass; 164 discovered, 164 passed, 0 failed, 0 skipped. |
| .NET/Ruff formatting and lint | Pass; `dotnet format --verify-no-changes --severity info`, Ruff lint, and Ruff format verification. |
| `git diff --check` | Pass through the repository baseline for working tree and index. |

The first focused compile exposed two `CA1861` analyzer findings in test expectations, and the first formatting pass identified repository-style simplifications. Both were corrected before final validation. An initial disposable-clone copy attempt left the clones on the old committed validators; those old-state passes were rejected as evidence. The final clone matrix used strict error handling, copied a flattened candidate-file list, verified the refactored validator text in each clone, and then passed on `main` and detached HEAD without an exception or weakened gate.

## Completion State

PB-0102 is logically complete and its repository status is synchronized by the approved PB-0103 rollover. Its implementation, tests, task commit, task-branch push, pull-request workflow, merge into and push of `main`, required `main` CI, explicit user confirmation, `[x]` / 🟢 **DONE** marker, Active Work removal, and single Completion Log row all have evidence. No PB-0102 gate remains.
