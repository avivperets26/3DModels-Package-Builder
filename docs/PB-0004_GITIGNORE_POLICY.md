# PB-0004 Git Ignore Policy Evidence

- **Task:** PB-0004 — Add repository-safe `.gitignore` rules
- **Lifecycle:** `[x]` — 🟢 **DONE**
- **Documented branch:** `chore/PB-0004-gitignore`
- **Verification date:** 2026-07-23
- **Final corrective commit:** `235c952a06951fa21e9b18b72a1ac69ce45e3487`
- **Final direct-main merge:** `835916065f38b735ae31b83092dea989298c0d0e`
- **Completion date:** 2026-07-23
- **Project root:** `C:\Dev\PackageBuilder`

PB-0004 is complete. The original policy implementation was merged through pull request #5 with successful pull-request and `main` workflows. The standalone-invocation correction was subsequently committed, pushed, merged directly into `main` without a corrective pull request, and validated by a successful corrected `main` workflow. The user explicitly confirmed the corrective push, direct merge, and successful CI. No CI exception was used.

## Policy Coverage

| Category | Ignored generated or local-only state | Important trackable exclusions |
|---|---|---|
| Repository roots | `tools`, `downloads`, `logs`, `runtime-data`, and `artifacts` at repository root | Source-controlled files elsewhere in the repository |
| .NET | `bin`, `obj`, test results, BenchmarkDotNet output, binary logs, and coverage output | C# source, `.csproj`, and `.sln` files |
| Editors | Visual Studio per-user state, JetBrains per-user state, and VS Code machine-local caches/files | Shared `.vscode/settings.json`, `tasks.json`, `launch.json`, and `extensions.json`; shared JetBrains project style |
| PowerShell and Python | Pester/transcript/cache output, bytecode, tool caches, virtual environments, and package metadata | `.ps1` and `.py` source |
| Blender | Numbered backups, autosave/recovery files, blend caches, crash state, and Python-generated content | `.blend` source plus FBX, GLB, glTF, OBJ, and texture inputs |
| Unity | `Library`, `Temp`, `Obj`, `Logs`, `UserSettings`, `Build`, `Builds`, `.utmp`, `.gradle`, memory captures, recordings, crashes, and generated template IDE files | `Assets`, `Packages`, `ProjectSettings`, `.unity`, `.prefab`, `.mat`, source models/textures, and `.unitypackage` inputs |
| Unreal | `Binaries`, `DerivedDataCache`, `Intermediate`, `Saved`, local dependency data, and generated template IDE files | `Config`, `Content`, `Source`, `.uproject`, `.uasset`, and `.umap` |
| OS and temporary state | OS metadata, editor swaps, temporary files, lock files, and local temporary directories | Normal documentation and source files |
| Secrets and signing | Actual `.env` variants, local secret configuration, credential files, private keys, signing stores, and local credential databases | `.env.example`, `.env.template`, safe client-secret examples, and non-secret configuration |

No rule globally ignores `.fbx`, `.glb`, `.gltf`, `.obj`, `.blend`, `.png`, `.jpg`, `.jpeg`, `.tga`, `.exr`, `.unity`, `.prefab`, `.mat`, `.uproject`, `.uasset`, `.umap`, C#, PowerShell, Python, JSON, YAML, Markdown, or documentation formats.

## Automated Validation

`scripts/Test-GitIgnorePolicy.ps1` performs table-driven checks without creating test files or directories:

- It uses `git check-ignore -v --no-index` for 107 synthetic repository-relative paths.
- It expects 67 generated/local-only examples to be ignored and 40 source, fixture, engine, editor, package-input, safe-example, or documentation examples to remain trackable.
- It verifies the required repository-root rules are present and rejects duplicate rules, filesystem-absolute rules, backslashes, parent traversal, blanket patterns, and complete `.vscode` ignores.
- It verifies `.env.example`, `.env.template`, and the safe client-secret example through their exact negation rules.
- It resolves every synthetic path beneath the Git root before checking it.
- It checks every tracked path with `--no-index` and reports the exact path and matching rule if a tracked file would become ignored.

`scripts/Test-RepositoryBaseline.ps1` invokes the policy validator directly and also launches a fresh Windows PowerShell 5.1 process using the supported standalone `-File` mode. The regression check uses the validator's absolute path, verifies the child-process exit code, and reports captured output if the process fails. The same checks run locally and in the existing SHA-pinned GitHub Actions repository-baseline workflow.

## Local Evidence

| Validation | Result |
|---|---|
| Original pre-commit ignore-policy checkpoint | Pass — 107 synthetic paths: 67 ignored and 40 trackable; 13 tracked paths checked; no conflict |
| Original pre-commit repository-baseline checkpoint | Pass — 13 checks passed, 0 failed |
| Explicit PowerShell parser validation | Pass — 3 scripts parsed, 0 failed |
| `git diff --check` | Pass |
| Tracked-file ignore conflict | None at the local validation checkpoint |
| Filesystem mutation by policy tests | None; all checked paths were synthetic |
| Current standalone invocation without `RepositoryRoot` | Pass — exit code 0; 107 synthetic paths: 67 ignored and 40 trackable; 15 tracked paths checked with no conflict |
| Current standalone invocation with explicit `RepositoryRoot` | Pass — exit code 0; 107 synthetic paths: 67 ignored and 40 trackable; 15 tracked paths checked with no conflict |
| Current direct PowerShell invocation | Pass — 107 synthetic paths: 67 ignored and 40 trackable; 15 tracked paths checked with no conflict |
| Current tracked-file repository baseline | Pass — 14 checks passed, 0 failed, including the new standalone child-process regression |

## Original Implementation Cycle

| Gate | Evidence |
|---|---|
| Original commit | `3f9a9a920d2ef1ef233e8f5f2b55bae75f5deab9` |
| Task-branch push | Explicitly confirmed by the user; pull request #5 contains the task commit |
| Pull-request merge | [Pull request #5](https://github.com/avivperets26/3DModels-Package-Builder/pull/5) merged into `main` as `cab7c3cbf803bf8f1c6187c2ee18dc5f08717988` |
| Pull-request CI | [Repository baseline run 30003215780](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30003215780) succeeded |
| Original `main` CI | [Repository baseline run 30003275017](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30003275017) succeeded |
| CI exception | None |

## Corrective Standalone-Invocation Cycle

| Gate | Evidence |
|---|---|
| Corrective commit | `235c952a06951fa21e9b18b72a1ac69ce45e3487` |
| Corrective push | Explicitly confirmed by the user |
| Corrective pull request | None existed; the corrective commit was merged directly into `main` |
| Direct-main merge | `835916065f38b735ae31b83092dea989298c0d0e` |
| Corrected `main` CI | [Repository baseline run 30004427880](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30004427880) succeeded |
| User confirmation | The user explicitly confirmed the corrective push, direct merge, and successful CI on 2026-07-23 |
| CI exception | None |

## Completion Bookkeeping Publication Checkpoint

| Gate | Evidence |
|---|---|
| Completion bookkeeping commit | `ba15b8b268562c9b00ec08a5d3e26446cdf928ca` |
| Publication method | Another direct merge into `main`; no final pull request existed |
| Direct-main merge | `f33da4d8664e1467916abdfb497360f6df50efa0` |
| Published `main` CI | [Repository baseline run 30004923229](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30004923229) succeeded |

This documentation follow-up will be published through the required final pull request, providing `pull_request` CI coverage for the corrected validator.

Every PB-0004 acceptance and completion gate is satisfied. PB-0004 is `[x]` and 🟢 **DONE**, has been removed from Active Work, and appears exactly once in the Completion Log.
