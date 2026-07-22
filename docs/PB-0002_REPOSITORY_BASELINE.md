# PB-0002 Repository Baseline

- **Task:** PB-0002 — Initialize the local Git repository and `main` branch
- **Lifecycle:** `[x]` — 🟢 **DONE**
- **Documented branch:** `chore/PB-0002-initialize-repository`
- **Verification date:** 2026-07-22
- **Dated `main` checkpoint:** `979c2a773ebe222343d5a3d2b4f72f383b532d60`
- **Dated post-audit remote `main` checkpoint:** `c75c119cfae7c8e9bfe4f2b0fea2fbd77575e028`
- **Bootstrap CI commit:** `0b1700e4d999069ef7372fcc0ba0e6971789b8e5`
- **Final direct `main` merge:** `86ac34ac61f1cb729e59fc0c7c10ffd772b2ee2a`
- **Completion date:** 2026-07-22

This document records the verified PB-0002 repository-bootstrap evidence. The original baseline and CI continuation satisfied their documented local, Git, GitHub CI, merge, and user-confirmation gates. PB-0002 completed on 2026-07-22 without a CI exception.

## 1. Pre-change repository state

The following read-only checks ran before any PB-0002 file was changed:

| Check | Result |
|---|---|
| Canonical workspace path | `C:\Dev\PackageBuilder` — exact match |
| `git rev-parse --show-toplevel` | `C:/Dev/PackageBuilder` — the same Windows path |
| Working tree | Clean; `git status --porcelain=v1 --untracked-files=all` returned no entries |
| Checked-out branch | `chore/PB-0002-initialize-repository` |
| Pre-change branch `HEAD` | `979c2a773ebe222343d5a3d2b4f72f383b532d60` |

The clean-tree result applies only to the pre-change checkpoint. The PB-0002 documentation edits intentionally make the final working tree non-clean until the user performs the authorized Git workflow.

### 1.1 Continuation checkpoint before bootstrap CI work

Read-only inspection before the bootstrap CI changes began on 2026-07-22 found:

| Check | Result |
|---|---|
| Working tree | Clean |
| Checked-out branch | `chore/PB-0002-initialize-repository` |
| Branch `HEAD` | `cb0748f9e7300f2122014bff5e9a130b47b3dc5d` |
| Configured upstream | `origin/chore/PB-0002-initialize-repository` at the same commit |
| Read-only remote branch | Present at `cb0748f9e7300f2122014bff5e9a130b47b3dc5d` |
| Prior pull request | [#3](https://github.com/avivperets26/3DModels-Package-Builder/pull/3), merged into `main` on 2026-07-22 |
| Prior merge commit | `c75c119cfae7c8e9bfe4f2b0fea2fbd77575e028` |
| Workflow runs on the task branch | Zero before the bootstrap workflow existed |

This proves that the earlier PB-0002 repository-audit documentation was committed, pushed, and merged. It does not provide the required CI pass or explicit user completion confirmation, so PB-0002 remained open for this approved bootstrap CI continuation.

## 2. Branches, remote, and history

Read-only local Git, remote-ref, GitHub repository-page, and GitHub API evidence produced these results on 2026-07-22:

| Check | Result |
|---|---|
| Local `main` exists | Pass; it resolved to checkpoint `979c2a773ebe222343d5a3d2b4f72f383b532d60` |
| `main` upstream | Pass; `origin/main` |
| Tracking configuration | `branch.main.remote=origin`; `branch.main.merge=refs/heads/main` |
| Local `origin/main` | Checkpoint `979c2a773ebe222343d5a3d2b4f72f383b532d60` |
| Read-only remote `main` | `git ls-remote` returned checkpoint `979c2a773ebe222343d5a3d2b4f72f383b532d60` |
| Fetch URL | `https://github.com/avivperets26/3DModels-Package-Builder.git` |
| Push URL | `https://github.com/avivperets26/3DModels-Package-Builder.git` |
| Approved repository identity | `avivperets26/3DModels-Package-Builder` |
| GitHub visibility | Public |
| GitHub default branch | `main` |
| PB-0001 baseline object | `1562abfef49071e83978e7573499d07e629b0c53` is a commit |
| PB-0001 baseline ancestry | Pass; the baseline is an ancestor of the dated `main` checkpoint |
| Reachable commit count | 9 |
| Root commits | One: the PB-0001 baseline commit |

No Git setting, remote, branch, or GitHub setting was changed.

## 3. Required tracked baseline

All files required by the pre-CI baseline were present in the index:

- `AGENTS.md`
- `global.json`
- `docs/Package_Builder_Plan.md`
- `docs/TECH_STACK_AND_ARCHITECTURE.md`
- `docs/IMPLEMENTATION_BACKLOG.md`
- `docs/QUALITY_AND_RELEASE_GATES.md`
- `docs/PB-0001_ENVIRONMENT_BASELINE.md`
- `scripts/Enter-PackageBuilderEnvironment.ps1`

The pre-change index contained nine files in total: the eight required files above plus `.gitignore`. The approved continuation added these files, which were tracked in bootstrap CI commit `0b1700e4d999069ef7372fcc0ba0e6971789b8e5`:

- `scripts/Test-RepositoryBaseline.ps1`
- `.github/workflows/repository-baseline.yml`

Additional baseline checks passed:

- `global.json` parsed as JSON, pinned SDK `10.0.302`, and set roll-forward to `disable`.
- `scripts/Enter-PackageBuilderEnvironment.ps1` parsed with zero PowerShell syntax errors.
- Every indexed path resolved beneath `C:\Dev\PackageBuilder`.
- No indexed path or file was a reparse point, Git symlink, submodule, or other special Git mode.
- No indexed file exceeded 1 MiB.

## 4. Index and content safety

The index contained none of the following:

- `tools`, `downloads`, `logs`, `runtime-data`, or `artifacts` content.
- `.NET` `bin` or `obj` output.
- Visual Studio per-user `.vs`, `.suo`, or `.user` state.
- Unity, Unreal, or Blender generated assets or caches.
- Executables, libraries, symbols, installers, archives, NuGet packages, source models, Blender files, Unity packages, Unreal assets, or other prohibited binary extensions.
- Paths outside the repository root.

A targeted signature scan covered all current tracked text, 24 unique reachable historical blobs, and seven local dangling blobs. It found no private-key headers, known GitHub/AWS/Slack/OpenAI/Google/Stripe token shapes, Azure storage keys, credential-bearing URLs, or quoted credential assignments. It also found no binary reachable-history blobs.

No tracked file contained an email address or a user-profile filesystem path. The two `C:\Dev\PackageBuilderData` matches in `AGENTS.md` are intentional prohibitions against that path, not active configuration. Git author/committer metadata contains two identity email values, one on a personal-mail domain and one on GitHub's domain; these are history metadata rather than tracked-file content. If the personal-mail identity is unintended for a public repository, remediation would require a separately authorized, destructive history rewrite and coordinated remote action, so PB-0002 does not change it.

The signature scan is a focused baseline check, not a substitute for the permanent secret-scanning work assigned later in the backlog.

## 5. Existing ignore coverage

`git check-ignore -v --no-index` confirmed that representative generated paths are ignored for:

- Repository-local `tools`, `downloads`, `logs`, `runtime-data`, and `artifacts`.
- Nested `.NET` `bin` and `obj` output.
- `.vs` state, `.suo` files, and `.user` files.

No `.gitignore` rule was added or expanded during PB-0002.

### Deferred to PB-0004

The following representative paths were not ignored and remain future PB-0004 work:

- Unity `Library`, `Temp`, and `UserSettings` content.
- Unreal `Intermediate`, `Saved`, and `DerivedDataCache` content.
- Blender/Python `__pycache__` and `.pyc` content.
- Local `.env` files.
- Signing-key files such as `.pfx`.

These gaps do not place such content in the current index. They must be handled by PB-0004 without expanding PB-0002's scope.

## 6. Repository-history integrity

| Check | Result |
|---|---|
| `git fsck --full --strict --no-dangling` | Pass; no reachable-object error |
| Reachable object enumeration | 47 object lines; zero missing objects |
| Reachable history | 9 commits with one root commit |
| Object-store garbage | Zero garbage bytes reported by `git count-objects -vH` |

The unrestricted strict `git fsck` also reported seven dangling blobs and five dangling trees from prior local operations. Dangling objects are unreachable and did not make the command fail. The dangling blobs were included in the targeted secret-signature scan, which returned no finding. No cleanup or pruning was performed.

## 7. Backlog and Markdown consistency

At the pre-completion checkpoint, the synchronized backlog validation produced these results:

- 243 task definitions and no duplicate task ID.
- Every documented task branch matched the allowed format and included its task ID.
- No unknown or self-referential explicit PB dependency.
- Checkbox and lifecycle-marker combinations were consistent.
- PB-0001 remained `[x]` and 🟢 **DONE**.
- PB-0002 was `[ ]` and 🟡 **PROCESS**, and appeared in Active Work.
- PB-0013 remained `[ ]`; its Active Work status remained 🟡 **PROCESS**.
- The Completion Log contained only PB-0001, exactly matching the only completed task.
- All 68 expected quality requirement IDs were present with no missing or unexpected ID.
- Tracked Markdown had balanced fenced-code markers, consistent table column counts, valid heading-level transitions, and no Unicode replacement character.
- One pre-existing unindented PB-1512 dependency line was corrected without changing its content or task status.

## 8. Bootstrap repository CI

PB-0002 now includes a deliberately narrow repository/documentation bootstrap workflow:

- `.github/workflows/repository-baseline.yml` runs for pull requests targeting `main` and pushes to `main`.
- It uses the free GitHub-hosted `windows-latest` runner and grants only read access to repository contents.
- `actions/checkout` v7.0.0 is pinned to its reviewed, verified immutable release commit `9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0`, uses full history, and does not persist credentials.
- The workflow passes `GITHUB_WORKSPACE` to `scripts/Test-RepositoryBaseline.ps1`; neither file assumes that GitHub checked out the repository at `C:\Dev\PackageBuilder`.
- CI uses `-RequireTrackedFiles`. Local pre-staging review includes tracked files plus untracked, non-ignored candidate files so Codex can validate changes without staging them.
- The script checks required files and the approved `global.json` pin; PowerShell parsing; Markdown headings, fences, tables, and local links; PB task IDs, dependencies, cycles, branches, lifecycle markers, and Completion Log synchronization; prohibited repository content; workflow pinning/scope; `git diff --check`; and reachable-history integrity.
- The workflow does not restore, build, format, or test the future .NET solution; install .NET, Blender, Unity, or Unreal; upload artifacts; add telemetry; publish; or require a paid service.

PB-0009 remains the owner of full solution restore, build, format checking, and automated test CI. Later backlog tasks own coverage and supply-chain gates. PB-0002 supplies only the minimum GitHub CI evidence needed to validate the repository bootstrap before that solution exists.

The local pre-staging command is:

```powershell
.\scripts\Test-RepositoryBaseline.ps1
```

The GitHub-equivalent tracked-file mode is:

```powershell
.\scripts\Test-RepositoryBaseline.ps1 -RepositoryRoot (git rev-parse --show-toplevel) -RequireTrackedFiles
```

On 2026-07-22, both the pre-staging validation and the final local completion-bookkeeping validation reported 12 passed checks and zero failed checks. The separate GitHub Actions evidence is recorded below.

## 9. Final completion evidence

| Gate | Final evidence |
|---|---|
| Original baseline commit | `cb0748f9e7300f2122014bff5e9a130b47b3dc5d` |
| Original baseline integration | [PR #3](https://github.com/avivperets26/3DModels-Package-Builder/pull/3) merged as `c75c119cfae7c8e9bfe4f2b0fea2fbd77575e028` |
| Bootstrap CI commit | `0b1700e4d999069ef7372fcc0ba0e6971789b8e5` on `chore/PB-0002-initialize-repository` |
| CI continuation integration | Merged directly into `main` as `86ac34ac61f1cb729e59fc0c7c10ffd772b2ee2a` |
| Pull-request scope | PR #3 contained the original baseline only; the CI continuation used a direct `main` merge and did not have a second PR |
| GitHub CI | [Repository baseline workflow run 29957972750](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/29957972750) — `success` for `86ac34ac61f1cb729e59fc0c7c10ffd772b2ee2a` |
| Local final validation | `scripts/Test-RepositoryBaseline.ps1 -RequireTrackedFiles` — 12 passed, zero failed |
| User confirmation | The user explicitly confirmed the commit, push, direct merge, and successful CI gates |
| CI exception | None used |
| Completion date | 2026-07-22 |

PB-0002 is `[x]` and 🟢 **DONE**. It is removed from Active Work and recorded exactly once in the backlog Completion Log. PB-0013 and every unrelated task retain their previous status.
