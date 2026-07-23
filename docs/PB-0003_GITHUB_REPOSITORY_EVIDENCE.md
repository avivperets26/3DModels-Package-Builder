# PB-0003 GitHub Repository Evidence

- **Task:** PB-0003 — Establish the approved public GitHub repository and push `main`
- **Lifecycle:** `[ ]` — 🟡 **PROCESS**
- **Documented branch:** `chore/PB-0003-github-remote`
- **Verification date:** 2026-07-23
- **Verified `main` checkpoint:** `2a520bbb2d17245756ca392883ba5a6916f60fef`
- **Approved repository:** [avivperets26/3DModels-Package-Builder](https://github.com/avivperets26/3DModels-Package-Builder)
- **Latest verified workflow:** [Repository baseline run 29959167858](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/29959167858)

This document records read-only local Git, remote-ref, GitHub repository, GitHub content, and GitHub Actions evidence for PB-0003. The acceptance-state checks pass at the dated checkpoint, but PB-0003 remains open until its own documentation commit, task-branch push, CI, merge into `main`, and explicit user completion confirmation are evidenced.

No repository setting, visibility, remote URL, branch configuration, branch ref, or GitHub content was changed during this audit.

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

## 3. Required files on `main`

The verified `main` tree contains all twelve current repository-baseline files:

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

Because local `main`, `origin/main`, and read-only remote `main` resolve to the same commit, the locally inspected tree is the tree published as remote `main`. GitHub content evidence independently confirms that the workflow and PB-0002 evidence document are present on `main`.

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

The latest run for the verified `main` checkpoint is run `29959167858`, event `push`, run number `2`, with status `completed` and conclusion `success`. Its `Validate repository baseline` job and every recorded job step concluded successfully.

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

These checks apply to the tracked repository baseline and current PB-0003 documentation candidate. They do not replace the broader supply-chain and secret-scanning work assigned to later security tasks.

### Local validation results

| Validation | Result |
|---|---|
| `scripts/Test-RepositoryBaseline.ps1 -RequireTrackedFiles` | 12 passed, 0 failed |
| `scripts/Test-RepositoryBaseline.ps1` pre-staging candidate mode | 12 passed, 0 failed |
| Branch, upstream, URL, ref-equality, ancestry, required-file, PB-0002 ancestry, prohibited-path, prohibited-extension, and personal-path checks | All passed |
| `git diff --check` and `git diff --cached --check` | Passed |

## 6. Repository-name discrepancy

| Source | Name |
|---|---|
| Original plan | `package-builder` |
| Approved actual repository | `3DModels-Package-Builder` |
| Current decision | Unresolved |

The discrepancy remains explicitly documented in the product plan, backlog, and this evidence record. PB-0003 does not rename the GitHub repository and does not silently rewrite the original planned name. Resolving the difference requires a separate user decision and separately authorized work.

## 7. Completion state and remaining gates

The repository acceptance state is verified, but the PB task lifecycle is not complete. The following gates remain:

1. The user reviews and commits the PB-0003 documentation changes on `chore/PB-0003-github-remote`.
2. The user pushes that task branch.
3. PB-0003 GitHub CI passes for the pushed change.
4. The user merges the reviewed change into `main`.
5. Post-merge evidence confirms remote `main` and its Repository baseline workflow.
6. The user explicitly confirms PB-0003 completion.

Until then, PB-0003 stays `[ ]` and 🟡 **PROCESS** and remains outside the Completion Log.
