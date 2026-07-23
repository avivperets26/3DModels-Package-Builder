# PB-0004 Git Ignore Policy Evidence

- **Task:** PB-0004 — Add repository-safe `.gitignore` rules
- **Lifecycle:** `[ ]` — 🟡 **PROCESS**
- **Documented branch:** `chore/PB-0004-gitignore`
- **Verification date:** 2026-07-23
- **Project root:** `C:\Dev\PackageBuilder`

PB-0004 is locally implemented and remains open. No task commit, push, pull request, GitHub CI, merge, final `main` CI, or explicit user completion confirmation is recorded.

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

`scripts/Test-RepositoryBaseline.ps1` invokes the policy validator, so the same test runs locally and in the existing SHA-pinned GitHub Actions repository-baseline workflow.

## Local Evidence

| Validation | Result |
|---|---|
| `scripts/Test-GitIgnorePolicy.ps1` | Pass — 107 synthetic paths: 67 ignored and 40 trackable; 13 tracked paths checked; no conflict |
| `scripts/Test-RepositoryBaseline.ps1 -RepositoryRoot (git rev-parse --show-toplevel) -RequireTrackedFiles` | Pass — 13 checks passed, 0 failed, including PowerShell parsing, ignore policy, task/dependency/lifecycle state, Completion Log, repository safety, diff checks, and history integrity |
| Explicit PowerShell parser validation | Pass — 3 scripts parsed, 0 failed |
| `git diff --check` | Pass |
| Tracked-file ignore conflict | None at the local validation checkpoint |
| Filesystem mutation by policy tests | None; all checked paths were synthetic |

## Remaining Gates

PB-0004 remains `[ ]` and 🟡 **PROCESS** until the user completes and confirms the required commit, task-branch push, pull-request CI, merge into `main`, final `main` CI, and completion bookkeeping. It must remain absent from the Completion Log until those gates pass.
