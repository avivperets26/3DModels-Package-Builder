# PB-0911 / PB-0912 — HTML reports and support bundles

## Scope and lifecycle

The user selected both tasks together. Branch `codex/PB-0911-PB-0912-reports-support` was created
after fetch, main checkout, fast-forward pull and clean equality with origin/main at
`0c950f0667b43c7a8f2d09423b3bc0351264a650`. The exact branch exception is recorded in AGENTS,
the engineering skill and tested branch policy. PB-0908–PB-0910 were rolled to DONE once with
task/merge commits, successful main CI and user confirmation in the Completion Log.

Both current tasks remain IN PROGRESS. The user authorized commit, push and merge after accepting
the local validation handoff. Publication is underway; exact task/merge commits and main CI results
will be retained in `artifacts/PB-0911-publication.json` and recorded at the next branch rollover.
Counts: 264 total, 133 DONE, 2 IN PROGRESS, 0 BLOCKED, 129 BACKLOG, 131 remaining (50.4%).

## Acceptance and API

| Task / requirement | Implementation | Evidence |
| --- | --- | --- |
| PB-0911 status, blockers, warnings, suggested fixes | `HtmlValidationReportGenerator` uses canonical PB-0910 status and findings | All four states, warning-only completion, blocking warning, suggested action/source, empty sections |
| PB-0911 metrics, artifacts, versions | Strict report projection; exact Int64 bytes; invariant double formatting; unknown counters labelled | Exact value above 2^53, culture, versions, hashes and unknown metrics tests |
| PB-0911 offline previews | Explicit preview bytes/ID; matching role, size, SHA-256; owned snapshot; trusted decode and PNG re-encode | Real JPEG → pixels → embedded PNG exact decoded-pixel round trip; bad hash/size/role/count/duplicate/oversize/malformed rejection |
| PB-0911 safe readable document | Reviewed embedded Scriban template, HTML escaping, no scripts/loaders/remote resources, restrictive CSP | UTF-8/no BOM/LF, Unicode, literal template/XSS payloads, private prose omission, deterministic rendering |
| PB-0912 diagnostic bundle | `SupportBundleGenerator` and `ZipSupportBundleArchiveWriter` | Five manifest cases; exact six entries, deterministic bytes, fixed timestamps, inventory sizes and SHA-256, strict report/version round trips |
| PB-0912 privacy and log isolation | Shared export redactor; manifest summary allowlist; strict per-job JSONL parser | Source paths/assets/names/branding absent; credential/quoted-tail/email/token/URL/path tests; cross-job, duplicate, unknown, truncated, non-UTC and malformed records rejected |
| PB-0912 bounds, failure and cleanup | Owned in-memory documents/archive; cancellation; stable expected errors | Invalid inputs/UTF-8/inventory/document limits, writer failure, cancellation; no file writes or disposable package workspaces |

Compose `HtmlValidationReportGenerator` with the existing trusted `IPreviewImageCodec` (the
Windows host uses `WindowsPreviewImageCodec`). Pass a typed `BuildValidationReport` and optional
explicit `ValidationReportPreview` values. The returned document offers text and strict UTF-8 bytes.
Rendering does not read artifact paths. Missing previews are explicitly labelled, not errors.
Images are limited to 32 entries, 3,000,000 encoded bytes each, 1920×1080 decoded pixels, and
8,000,000 total re-encoded PNG bytes. These are HTML resource quotas, not marketplace approval.
Report limits remain PB-0910; rendered text has a 20-million-character cap.

Compose `SupportBundleGenerator` with that HTML generator and the ZIP adapter. Pass the selected
job's report, manifest JSON and structured job-log JSONL explicitly. The caller owns the association
between job and manifest; manifests have no job ID. Manifest schema must match the report build
lock, and every log record must match the report job ID. Empty job logs are accepted. General
application logs, raw engine logs and arbitrary attachments are deliberately not accepted.

The ZIP contains only `manifest-summary.json`, `versions.json`, `job.log.jsonl`, `validation.json`,
`validation.html`, and `bundle-inventory.json`. The manifest summary is labelled as a summary,
not a reusable product manifest: it contains schema/case/targets/counts/rig presence and explains
omitted fields. No source references, names, branding, marketplace-private data or model/texture
bytes are copied. Support HTML contains no images. Inventory hashes cover the other five entries;
the result also includes the SHA-256 of the complete ZIP. No upload or filesystem output is performed.

Job logs cap input/output at 4 million characters, 2,048 lines and 600,000 characters per line;
existing event/property bounds also apply. Each ZIP document caps at 8 MB and total uncompressed
content at 16 MB. Expected validation/encoding failures return no partial bundle; cancellation
propagates. Repeated bytes are verified on the current .NET/Windows runtime, not promised across
future compression or codec versions.

## Redaction policy and reuse audit

`DiagnosticReportProjection` is shared by HTML and support export. It validates through the
existing report serializer, sanitizes original decoded prose, then revalidates sanitized JSON
and its references. Unsafe identifiers fail closed instead of silently breaking references.
`SensitiveDiagnosticValueRedactor.RedactForExport` extends the shared local redactor without
changing existing local logging behavior. Sensitive keys and prose containing credentials,
authorization, private-key markers or source filenames are omitted as whole fields. Absolute
paths, relative paths and existing user-profile placeholders are omitted; common bare provider tokens, emails
and network URLs are removed. Omitting a whole credential-bearing field also removes quoted
secret tails that may remain after earlier local redaction. This intentionally sacrifices some
diagnostic prose. It is a tested policy, not a universal classifier for arbitrary personal prose
or unlabelled custom secrets. Callers must supply diagnostic messages and opaque IDs only.

Reused: PB-0901 strict Scriban runtime (extracted into `ReviewedTemplateRenderer`), PB-0910 report
validation/status, PB-0109 findings, build-lock versions, manifest reader, structured-event rules,
duplicate JSON safeguards, SHA-256 contracts and the existing image codec. No dependency or
project-reference additions. Infrastructure depends only on Contracts; application policy remains
in Application, never copied into WPF or ZIP adapters.

Intentional separate JSON projections: export log serialization has stricter redaction and
mandatory per-job identity, while the existing local sink also handles application logs. This is
not a new logging wire contract; both reuse event validation. PB-0912 owns compatibility tests.
ZIP creation uses the standard archive primitive; the portable package builder's source-file
staging lifecycle and the untrusted ZIP extraction service are not repurposed for diagnostic
encoding. The fixed inventory removes arbitrary archive entry paths entirely.

## Validation record

- Locked restore succeeded with .NET 10.0.302; no dependency or lock-file changes.
- Release solution build: zero warnings/errors (`artifacts/PB-0911-final-build.log`).
- Full seven-project suite: 2,587 passed, zero failures/skips; source snapshot unchanged
  (`artifacts/test-results/PB-0911/summary.json` and per-project TRX). This adds 57 scope tests:
  36 export-contract cases and 21 application/real-adapter delivery cases, including credentials
  embedded in quoted JSON. The full suite was rerun after that export-policy refinement.
- Formatting verification found three naming issues in tests only. They were renamed after the
  full suite finished, followed by a rebuild and rerun of the affected 21 delivery tests.
- The public-content scan rejected literal synthetic private-key/home-path markers in a test.
  Those dummy inputs are constructed from constant fragments; the resulting test values are
  unchanged. No real credentials or personal paths were present, and no scanner rule was relaxed.
  Rebuild and all 36 export-contract tests passed after that fixture-only change.
- Ruff lint passed; all 51 Python files were already formatted. `git diff --check` and exact
  backlog-summary verification passed. Full .NET formatting verification at severity info passed
  (`artifacts/PB-0911-format-verify-final.log`).
- Final repository baseline: all 35 checks passed, zero failures, including public content,
  architecture/dependencies, exact branch-scope isolation, lifecycle accounting, Git integrity
  and nine test-artifact cleanup regressions (`artifacts/PB-0911-baseline-final.log`).
- Optional browser visual inspection was attempted but the browser URL security policy rejected
  the local HTML file. No bypass or alternate browser was attempted. The synthetic sample is
  retained at `artifacts/PB-0911/validation.html` for manual review; visual inspection is unverified.
- Package ZIPs and decoded previews exist only in test memory, disposed on success/failure.
  No engine projects or source fixtures were created or modified. Compact HTML/TRX/logs remain.

## Changed files and handoff

- Application: diagnostics HTML/projection/support generators and embedded template;
  shared reviewed-template renderer and existing README engine delegation; template resource entry.
- Contracts: support inventory/writer boundary, structured job-log export, shared export redactor.
- Infrastructure: fixed-inventory ZIP encoder.
- Tests: `DiagnosticExportTests` and `DiagnosticDeliveryTests`.
- Governance: AGENTS, engineering skill, branch policy/baseline tests, backlog and prior completion rollover.
- Documentation: plan, architecture, quality gates, index, cleanup audit, prior evidence and this file.

Suggested commit: `feat: add HTML validation reports and redacted support bundles`.
After reviewing the diff, the user may run:

```powershell
Set-Location C:\Dev\PackageBuilder
git status --short
git diff --check
git add AGENTS.md docs scripts/TaskBranchPolicy.Common.ps1 scripts/Test-RepositoryBaseline.ps1 skills/package-builder-engineering/SKILL.md src tests
git diff --cached --stat
git diff --cached
git commit -m "feat: add HTML validation reports and redacted support bundles"
git push -u origin codex/PB-0911-PB-0912-reports-support
```

No new desktop screen is claimed: the report/export services are ready for the later diagnostics
UI to compose. Commit/push/merge, successful main CI and user confirmation remain required before
recording DONE at the next branch rollover.
