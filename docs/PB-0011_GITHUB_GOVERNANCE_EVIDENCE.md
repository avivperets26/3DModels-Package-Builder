# PB-0011 GitHub Governance Evidence

**Task:** PB-0011 — Add pull-request, issue, ownership, and dependency-update configuration
**Branch:** `chore/PB-0011-github-governance`
**Lifecycle:** 🟢 **DONE**
**Evidence date:** 2026-07-24
**Official documentation review date:** 2026-07-24

## Scope and Current State

PB-0011 adds review-only repository governance for public issues, optional pull requests, code ownership, dependency-update proposals, and present-day security reporting. It changes no GitHub repository setting and creates no dependency-update pull request.

PB-0010 was synchronized exactly once before this implementation. Its final task commit `eaf8846df7bf4bb8edc82d8407da8c1a61130231` was merged through PR #11 as `b7396bf6b557da26df2f2d08a70c6f6d1b1a3796`; PR run 30077559953 and required `main` run 30077718661 succeeded; and the user explicitly confirmed completion on 2026-07-24 without an exception.

PB-0011 was published from final task commit `02491ce01e32559c2b41ce886f5595c286677555`, merged through [pull request #12](https://github.com/avivperets26/3DModels-Package-Builder/pull/12) as `5b37b3c8081d246c03eabe8dc3099b1a99f31ca1`, and validated by successful [PR workflow run 30080298582](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30080298582) and required [main workflow run 30080304495](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30080304495). The user explicitly confirmed the commit, push, merge, green required `main` CI, and completion on 2026-07-24. No CI, completion, or quality exception was used. The PB-0012 rollover records PB-0011 as `[x]` / 🟢 **DONE**, removes it from Active Work, and adds its one Completion Log row.

## Official GitHub Documentation Basis

Only current official GitHub documentation was used for GitHub behavior:

| Topic | Reviewed official source | Applied decision |
|---|---|---|
| Issue and pull-request templates | [About issue and pull request templates](https://docs.github.com/en/communities/using-templates-to-encourage-useful-issues-and-pull-requests/about-issue-and-pull-request-templates) | Use stable Markdown issue templates in `.github/ISSUE_TEMPLATE` and one `.github/pull_request_template.md`. |
| Issue template chooser and Issue Forms | [Configuring issue templates](https://docs.github.com/en/communities/using-templates-to-encourage-useful-issues-and-pull-requests/configuring-issue-templates-for-your-repository) | Use `config.yml`; do not use Issue Forms because GitHub still labels them public preview and no preview exception is approved. |
| CODEOWNERS | [About code owners](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-code-owners) | Place CODEOWNERS under `.github`, define a default owner, and explicitly own `/.github/`. |
| Dependabot v2 | [Dependabot options reference](https://docs.github.com/en/code-security/reference/supply-chain-security/dependabot-options-reference) | Monitor NuGet and GitHub Actions at `/`, weekly, against `main`, with explicit open-PR limits. |
| Public-repository secret scanning | [Secret scanning detection scope](https://docs.github.com/en/code-security/reference/secret-security/secret-scanning-scope) | For public repositories, secret scanning runs automatically for free. |
| Secret-scanning exclusion file | [Excluding folders and files from secret scanning](https://docs.github.com/en/code-security/how-tos/secure-your-secrets/customize-leak-detection/exclude-folders-and-files) | `.github/secret_scanning.yml` only configures paths to exclude; no file or exclusion is added. |
| Security policy | [Adding a security policy](https://docs.github.com/en/code-security/how-tos/report-and-fix-vulnerabilities/configure-vulnerability-reporting/add-security-policy) | Add a minimal root `SECURITY.md` with supported-version and reporting guidance. |
| Safe reporting without a verified channel | [Coordinated disclosure of security vulnerabilities](https://docs.github.com/en/code-security/concepts/vulnerability-reporting-and-management/coordinated-disclosure) | A public issue may ask only for a private contact method and must contain no vulnerability details. |

## Governance Configuration

### Pull Requests

`.github/pull_request_template.md` records PB-task or Dependabot identity, summary and scope, requirements/tests mapping, validation, documentation impact, and review checks for UX/accessibility, performance, security, containment, licensing, and public-repository safety. It states that pull requests are optional and cannot authorize automatic merge, publication, tags, releases, deployments, marketplace submission, or GitHub-setting changes.

### Public Issues

The stable Markdown templates are:

- `.github/ISSUE_TEMPLATE/bug_report.md`
- `.github/ISSUE_TEMPLATE/feature_request.md`
- `.github/ISSUE_TEMPLATE/config.yml`

Both templates have stable YAML front matter and warn against posting vulnerability details, credentials, private keys, private assets, unredacted logs, or personal data. The chooser disables blank public issues and links to the repository security policy. No Issue Form `.yml` template is present.

### Ownership

`.github/CODEOWNERS` assigns `@avivperets26` to all repository content and explicitly assigns `/.github/` to the same owner. This routes review ownership only. It does not claim that branch protection, required code-owner approval, or another GitHub setting is enabled.

### Dependency Updates

`.github/dependabot.yml` uses configuration version 2 and exactly two supported ecosystems:

- NuGet at `/`.
- GitHub Actions at `/`.

Both run weekly, target `main`, and allow at most five open version-update pull requests per ecosystem. Dependabot proposals require user review and manual merge. No Renovate configuration, private registry, credential, secret, automerge, publication, paid service, or unsupported ecosystem is configured.

### Secret Scanning

GitHub documents that public repositories receive secret scanning automatically for free. GitHub also documents `.github/secret_scanning.yml` as a path-exclusion file. Because PB-0011 approves no exclusion, no such file, fake path, empty syntax, test secret, or custom pattern is added.

PB-1611 remains responsible for future pinned local and CI dependency, licence, vulnerability, and secret scanning. PB-0011 does not pre-implement that task.

### Security Reporting

`SECURITY.md` prohibits sensitive public reports and states that no private vulnerability-reporting channel or private contact address is documented or verified as of the review date. It invents no email address. If no private method is visibly available, a reporter may post only a request to establish private contact, with no vulnerability detail or sensitive material. The complete vulnerability-response procedure remains later work.

## Automated Validation

`scripts/Test-GitHubGovernance.ps1` is dependency-free and compatible with Windows PowerShell 5.1 and PowerShell 7. It validates:

1. Required files and exact supported locations.
2. Stable Markdown issue-template front matter and chooser configuration.
3. Pull-request review sections and optional/manual publication policy.
4. CODEOWNERS syntax, default ownership, and the real `.github/` path.
5. Dependabot v2 ecosystems, roots, weekly schedules, `main` targets, and bounded PRs.
6. Absence of secret-scanning exclusions and competing Renovate configuration.
7. The current safe-reporting limitation in `SECURITY.md`.
8. Absence of credential-shaped examples, personal paths, mandatory-PR claims, and unsupported automatic capability claims.
9. PB-0011 evidence, completed lifecycle, Active Work removal, and single Completion Log entry.

`scripts/Test-RepositoryBaseline.ps1` runs the validator in-process and through standalone Windows PowerShell 5.1.

## Acceptance-Criterion Traceability

| PB-0011 criterion | Automated evidence |
|---|---|
| Optional PR template and complete review checklist | GitHub governance validator pull-request-template check |
| Stable issue templates; no preview Issue Forms | Exact-location and issue-template checks |
| Default and `.github/` ownership | CODEOWNERS check |
| Dependabot v2 for root NuGet and Actions | Dependabot semantic checks |
| Bounded weekly proposals targeting `main` | Dependabot schedule, target, and limit checks |
| No private registries, credentials, automerge, paid service, or second bot | Dependabot and competing-bot checks |
| No secret-scanning exclusions | Filesystem and reviewable-Git-set absence checks |
| Minimal honest security policy | `SECURITY.md` safe-reporting check |
| No sensitive or unsupported template content | Governance prohibited-content check |
| Baseline integration and PowerShell compatibility | In-process and standalone repository-baseline checks |
| PB-0011 completion evidence and one-merge rollover state | Governance lifecycle check and repository baseline |

## Validation Results

| Validation | Current result |
|---|---|
| `scripts/Test-GitHubGovernance.ps1` in PowerShell 7 | Pass; 9 checks passed, 0 failed. |
| Standalone Windows PowerShell 5.1 governance validation | Pass; 9 checks passed, 0 failed. |
| `scripts/Test-ContributionDocumentation.ps1` | Pass; 11 checks passed, 0 failed, including the governance section, command, and local links. |
| `scripts/Test-RepositoryBaseline.ps1 -RequireTrackedFiles` | Pass; 25 checks passed, 0 failed. |
| `scripts/Invoke-CoreCi.ps1` | Pass; all nine stages passed in the final run in 1 minute 16.022 seconds. |
| Exact SDK and locked restore | Pass; repository-local .NET SDK `10.0.302`; all projects were up to date under locked restore. |
| Release build | Pass; 15 projects, 0 warnings, 0 errors. |
| Formatting and lint | Pass; `dotnet format --verify-no-changes`, Ruff `0.15.22` lint, and Ruff formatting checks passed. |
| Baseline smoke tests | Pass; 4 discovered, 4 passed, 0 failed, 0 skipped; source-candidate nonmutation check passed. |
| Stable template structure and links | Pass; stable front matter, required headings, chooser structure, Markdown hierarchy, and local/external links validated. |
| CODEOWNERS and Dependabot rules | Pass; exact owner and paths, v2 ecosystems, root directories, weekly schedules, bounded PR counts, and `main` targets validated. |
| Secret and prohibited content | Pass; no secret-scanning exclusion, Renovate configuration, credential-shaped example, personal path, prohibited file, mandatory-PR claim, or unsupported automatic capability claim. |
| PowerShell parsing | Pass for every reviewable PowerShell script in the repository baseline. |
| `git diff --check` | Pass in the repository baseline and core pipeline. |
| Task, dependency, lifecycle, Active Work, and Completion Log | Pass at task-branch validation; PB-0011 had one Active Work row, remained `[ ]` / 🟡 **PROCESS**, and had no Completion Log row before publication. Its confirmed publication evidence and PB-0012 rollover state are recorded above. |
| PB-0013 preservation | Pass; its Active Work row and complete task-definition block match `HEAD` across 15 compared lines. |

## External State and Remaining Gates

No GitHub repository setting was changed or inferred. Private vulnerability reporting, branch protection, required code-owner reviews, Dependabot execution, dependency-update pull requests, and remote secret-scanning alert state remain unverified external settings or future events.

PB-0011 is logically complete and its permanent rollover bookkeeping is recorded on the PB-0012 branch. No PB-0011 completion gate remains. GitHub repository settings and future PB-1611 security scanning work remain outside PB-0011's verified scope.
