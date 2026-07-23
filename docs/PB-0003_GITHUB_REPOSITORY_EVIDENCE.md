# PB-0003 GitHub Repository Evidence

- **Task:** PB-0003 — Establish the approved public GitHub repository and push `main`
- **Lifecycle:** `[x]` — 🟢 **DONE**
- **Documented branch:** `chore/PB-0003-github-remote`
- **Verification date:** 2026-07-23
- **Completion date:** 2026-07-23
- **Initial verified `main` checkpoint:** `2a520bbb2d17245756ca392883ba5a6916f60fef`
- **Final task commit:** `eecd16ba1906af4c36906eaed7b99ce67f5150a4`
- **Pull request:** [#4](https://github.com/avivperets26/3DModels-Package-Builder/pull/4)
- **Merge commit:** `aa0b82b7a2e7880f5d6c57a5399d30e3391912cc`
- **Approved repository:** [avivperets26/3DModels-Package-Builder](https://github.com/avivperets26/3DModels-Package-Builder)
- **Successful PR workflow:** [Repository baseline run 29998957170](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/29998957170)
- **Successful final `main` workflow:** [Repository baseline run 29999066840](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/29999066840)

This document records read-only local Git, remote-ref, GitHub repository, GitHub content, and GitHub Actions evidence for PB-0003. The acceptance-state checks and every completion gate passed: the task commit was pushed, its pull-request workflow succeeded, pull request #4 merged it into `main`, the final `main` workflow succeeded, and the user explicitly confirmed the commit, push, CI, pull-request merge, and final `main` CI.

No CI exception was used. No repository setting, visibility, remote URL, branch configuration, repository name, or GitHub setting was changed during the evidence audit or this completion bookkeeping.

## 1. Repository identity and settings

| Check | Verified result |
|---|---|
| Repository identity | `avivperets26/3DModels-Package-Builder` |
| Canonical URL | `https://github.com/avivperets26/3DModels-Package-Builder` |
| Visibility | Public |
| Archived | No |
| Default branch | `main` |
| Remote `HEAD` | `refs/heads/main` |

GitHub repository metadata and the read-only remote `HEAD` symref agree that `main` is the default branch.

## 2. Origin and branch consistency

| Check | Verified result |
|---|---|
| Origin fetch URL | `https://github.com/avivperets26/3DModels-Package-Builder.git` |
| Origin push URL | `https://github.com/avivperets26/3DModels-Package-Builder.git` |
| `branch.main.remote` | `origin` |
| `branch.main.merge` | `refs/heads/main` |
| Local `main` upstream | `origin/main` |
| Local `main` | `2a520bbb2d17245756ca392883ba5a6916f60fef` |
| Local `origin/main` | `2a520bbb2d17245756ca392883ba5a6916f60fef` |
| Read-only remote `main` | `2a520bbb2d17245756ca392883ba5a6916f60fef` |
| Read-only remote `HEAD` | `2a520bbb2d17245756ca392883ba5a6916f60fef` |

Bidirectional ancestry checks between local `main` and `origin/main` passed, proving equality rather than only one-way ancestry. The remote configuration already satisfies the approved HTTPS policy; SSH is not required and no remote change is justified.

Those local and remote-ref values describe the initial acceptance checkpoint. Final GitHub evidence shows task commit `eecd16ba1906af4c36906eaed7b99ce67f5150a4` merged through pull request #4 into `main` as `aa0b82b7a2e7880f5d6c57a5399d30e3391912cc`. Completion bookkeeping did not fetch, move, or modify any local or remote ref.

## 3. Required files on `main`

The initial verified `main` checkpoint contains all twelve required repository-baseline files:

- `.github/workflows/repository-baseline.yml`
- `.gitignore`
- `AGENTS.md`
- `docs/IMPLEMENTATION_BACKLOG.md`
- `docs/PB-0001_ENVIRONMENT_BASELINE.md`
- `docs/PB-0002_REPOSITORY_BASELINE.md`
- `docs/Package_Builder_Plan.md`
- `docs/QUALITY_AND_RELEASE_GATES.md`
- `docs/TECH_STACK_AND_ARCHITECTURE.md`
- `global.json`
- `scripts/Enter-PackageBuilderEnvironment.ps1`
- `scripts/Test-RepositoryBaseline.ps1`

At the initial acceptance checkpoint, local `main`, `origin/main`, and read-only remote `main` resolved to the same commit, so the locally inspected tree was the tree published as remote `main`. GitHub content evidence independently confirmed that the workflow and PB-0002 evidence document were present on `main`. Final pull-request and workflow evidence then confirmed integration of the PB-0003 evidence document into `main`.

## 4. PB-0002 dependency and workflow

PB-0002 remains `[x]` and 🟢 **DONE** in the backlog and Completion Log. Its final task commit `0b1700e4d999069ef7372fcc0ba0e6971789b8e5` and direct-main integration commit `86ac34ac61f1cb729e59fc0c7c10ffd772b2ee2a` are valid commits and ancestors of the verified `main` checkpoint.

The remote `main` workflow:

- is named `Repository baseline`;
- runs on pushes and pull requests to `main`;
- uses a free `windows-latest` runner;
- grants read-only repository contents permission;
- pins `actions/checkout` to immutable commit `9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0`;
- disables persisted checkout credentials; and
- invokes `scripts/Test-RepositoryBaseline.ps1` with `-RequireTrackedFiles`.

At the initial acceptance checkpoint, run `29959167858`, event `push`, run number `2`, completed successfully; its `Validate repository baseline` job and every recorded job step concluded successfully. For the PB-0003 task commit, [pull-request workflow run 29998957170](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/29998957170) succeeded. After pull request #4 merged into `main`, [final main workflow run 29999066840](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/29999066840) also succeeded. No CI exception was used.

## 5. Public-repository safety

The repository baseline validation and targeted Git inspection verify that the current tracked tree contains:

- no detected credential, token, private-key, or credential-bearing URL signature;
- no user-profile or other personal filesystem path;
- no prohibited executable, library, archive, model, engine-package, or generated binary extension;
- no tracked `tools`, `downloads`, `logs`, `runtime-data`, or `artifacts` content;
- no tracked .NET build output or Visual Studio per-user state;
- no Unity, Unreal, or Blender generated asset/cache directory;
- no tracked path outside `C:\Dev\PackageBuilder`;
- no reparse point, Git symlink, submodule, or unexpected special Git mode; and
- no missing reachable Git object or reachable-history integrity error.

These checks apply to the tracked repository baseline and PB-0003 task commit. They do not replace the broader supply-chain and secret-scanning work assigned to later security tasks.

### Local validation results

| Validation | Result |
|---|---|
| `scripts/Test-RepositoryBaseline.ps1 -RequireTrackedFiles` on the completion-bookkeeping candidate | 12 passed, 0 failed |
| `git diff --check` on the completion-bookkeeping candidate | Passed |
| Initial branch, upstream, URL, ref-equality, ancestry, required-file, PB-0002 ancestry, prohibited-path, prohibited-extension, and personal-path checks | All passed |

## 6. Repository-name discrepancy

| Source | Name |
|---|---|
| Original plan | `package-builder` |
| Approved actual repository | `3DModels-Package-Builder` |
| Current decision | Unresolved |

The discrepancy remains explicitly documented in the product plan, backlog, and this evidence record. PB-0003 does not rename the GitHub repository and does not silently rewrite the original planned name. Resolving the difference requires a separate user decision and separately authorized work.

## 7. Completion state

| Gate | Final evidence |
|---|---|
| Task commit | `eecd16ba1906af4c36906eaed7b99ce67f5150a4` |
| Task-branch push | Explicitly confirmed by the user; pull request #4 and its workflow contain the task commit |
| Pull-request CI | [Run 29998957170](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/29998957170) succeeded |
| Pull-request merge | [Pull request #4](https://github.com/avivperets26/3DModels-Package-Builder/pull/4) merged into `main` as `aa0b82b7a2e7880f5d6c57a5399d30e3391912cc` |
| Final `main` CI | [Run 29999066840](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/29999066840) succeeded |
| User confirmation | The user explicitly confirmed commit, push, CI, pull-request merge, and final `main` CI on 2026-07-23 |
| CI exception | None |

Every PB-0003 acceptance and completion gate is satisfied. PB-0003 is `[x]` and 🟢 **DONE**, has been removed from Active Work, and appears exactly once in the Completion Log.

The planned `package-builder` versus actual `3DModels-Package-Builder` repository-name discrepancy remains explicitly unresolved. It is a separate decision and no rename, remote modification, or GitHub setting change is part of PB-0003 completion.
