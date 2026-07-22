# Package Builder — Quality and Release Gates

**Document status:** Normative quality baseline
**Project:** Package Builder
**Repository:** `C:\Dev\PackageBuilder`
**Last reviewed:** 2026-07-22

## 1. Purpose and Authority

This document defines the permanent user-experience, testing, performance, security, installation, engineering-quality, and release requirements for Package Builder. The product plan defines product behavior, the architecture defines system design, and the implementation backlog assigns delivery work. This document defines the evidence required before a PB task or product release can be described as complete.

The terms **must**, **must not**, **required**, and **blocked** are normative. A passing build, test count, or coverage percentage never overrides an unmet requirement. Claims such as “best practice,” “secure,” “fast,” or “production ready” require cited, reproducible evidence.

All work remains subject to `AGENTS.md`, including single-root containment, free-tooling, Visual Studio Code, user-controlled Git operations, and user confirmation of PB completion gates.

## 2. UX and UI Requirements

- **UX-001 — Design system:** The desktop application must use one consistent, documented, accessible design system for layout, spacing, typography, colours, controls, status, validation, and destructive-action treatment.
- **UX-002 — Accessibility:** Critical workflows must support keyboard-only operation, screen readers, high contrast, scalable text, visible focus states, meaningful accessible names, and predictable focus order.
- **UX-003 — Guided workflow:** Primary workflows must guide first-time users with sensible defaults and progressive disclosure of advanced settings.
- **UX-004 — Dry run:** File-changing and package-generating operations must provide a dry-run preview of planned inputs, names, paths, actions, outputs, warnings, and estimated resource use before execution.
- **UX-005 — Progress and cancellation:** Long-running work must display current stage, progress where measurable, elapsed time, and a safe cancellation control.
- **UX-006 — Actionable errors:** User-facing errors must identify what failed, the affected asset or step, the consequence, and a practical correction. A raw stack trace must never be the primary user-facing error.
- **UX-007 — Recovery:** User input must survive failures where safe, and retry/resume actions must preserve source integrity and explain what will be repeated.
- **UX-008 — Usability validation:** Representative first-time users must validate the critical setup, inspect, configure, dry-run, build, diagnose, retry, and results-review workflows against approved success criteria.
- **UX-009 — UI automation:** Critical desktop workflows must have deterministic automated UI tests, including keyboard and accessibility-critical paths.

## 3. Complete Requirements Testing

- **TEST-001 — Requirement mapping:** Every normative requirement and every PB acceptance criterion must map to at least one test. Approved manual or documentary verification may supplement but must never replace the required test.
- **TEST-002 — Traceability matrix:** The repository must maintain a requirements-to-tests traceability matrix containing requirement ID, source, owner, concrete test ID, fixture, evidence location, and current status, plus any supplementary verification ID where applicable.
- **TEST-003 — Test layers:** The test portfolio must include unit, contract, integration, end-to-end, UI, regression, installer, upgrade, and failure-recovery testing.
- **TEST-004 — Product/target matrix:** All five product cases must be tested for portable, Unity, and Unreal outputs wherever the target applies.
- **TEST-005 — Golden fixtures:** Representative, legally usable golden fixtures must cover static, rigged, animated, set, and collection products.
- **TEST-006 — Hostile and boundary inputs:** Tests must include corrupted, incomplete, malicious, unusually large, deeply nested, long-path, Unicode, and resource-pressure inputs.
- **TEST-007 — Coverage measurement:** Line and branch coverage must be measured, trended, and reported for production code.
- **TEST-008 — Overall thresholds:** Overall automated test coverage must be at least 90% line coverage and 85% branch coverage.
- **TEST-009 — Critical thresholds:** Security validation, path handling, naming, manifest validation, and package-integrity code must maintain 100% branch coverage.
- **TEST-010 — Coverage exclusions:** Generated-code or other coverage exclusions require written technical justification and explicit user approval. Exclusions must remain visible in the traceability and coverage reports.
- **TEST-011 — Mutation testing:** Critical validation and security components must pass approved mutation-score thresholds established from an initial measured baseline. Surviving high-risk mutants block release until killed or explicitly reviewed and approved by the user.
- **TEST-012 — Deterministic/offline tests:** Tests must be deterministic and must not require internet access unless explicitly classified as network integration tests. Network tests must be isolated from the default offline suite.
- **TEST-013 — Evidence over percentages:** Passing coverage or mutation percentages alone must never be treated as proof that requirements or acceptance criteria are satisfied.

## 4. Performance Requirements

- **PERF-001 — Budgets:** Small, medium, and large approved fixtures must have numeric elapsed-time, peak-memory, peak-disk, and temporary-space budgets for each applicable pipeline stage and complete build.
- **PERF-002 — Repeatable benchmarks:** Versioned, repeatable benchmarks must record machine profile, tool versions, fixture hashes, warm-up policy, sample count, variance, and regression thresholds.
- **PERF-003 — Streaming:** Large files and archives must be streamed or processed incrementally rather than loaded completely into memory unless a measured, approved exception exists.
- **PERF-004 — Bounded work:** Concurrency must be bounded. Long-running .NET work must propagate `CancellationToken`; worker processes must receive equivalent cancellation; all long-running work must enforce idle and total timeouts and verified cleanup.
- **PERF-005 — Correct caching:** A cache may be introduced only after correctness, content identity, invalidation, concurrency, corruption recovery, and version compatibility are tested.
- **PERF-006 — Copy minimization:** The system must avoid unnecessary copies of FBX, GLB, textures, archives, and engine projects while preserving immutable-source and containment guarantees.
- **PERF-007 — Build metrics:** Every build report must record stage durations, total duration, peak process memory, peak project-owned disk and temporary-space use, and bytes read/written.
- **PERF-008 — Evidence-led optimization:** Performance changes must cite benchmark evidence and must not trade away correctness, determinism, security, accessibility, or source safety.

Numeric budgets are approved through PB-1808. Until approved budgets exist for an applicable workflow, that workflow is not release-ready.

## 5. Security Requirements

- **SEC-001 — Threat model:** Maintain a versioned threat model covering untrusted archives, FBX/GLB models, textures, scripts, engine projects, tool downloads, plugins, generated packages, and external-process boundaries.
- **SEC-002 — Exploit prevention:** Prevent and test path traversal, ZIP bombs, decompression abuse, symlink/reparse-point escapes, command injection, argument confusion, filename collisions, and unsafe process invocation.
- **SEC-003 — Archive preflight:** Validate compressed size, expected/extracted size, expansion ratio, file count, nesting, extension policy, duplicate destinations, and canonical destination before extraction.
- **SEC-004 — Embedded scripts:** Never execute scripts or executable content embedded in imported assets or archives.
- **SEC-005 — External tools:** Run external tools with the least privilege practical, explicit arguments, isolated contained working directories, bounded timeouts, cancellation, and cleanup.
- **SEC-006 — Secrets:** Never store tokens, credentials, or private keys in source code, logs, manifests, test fixtures, generated documentation, or generated packages.
- **SEC-007 — Redaction:** Logs, reports, support bundles, diagnostics, and user-facing errors must redact secrets and sensitive paths according to a tested policy.
- **SEC-008 — Download verification:** Managed official downloads must be pinned and verified using vendor checksums and digital signatures when available; verification evidence must be retained beneath the project root.
- **SEC-009 — Dependency pinning and SBOM:** Pin direct and transitive dependencies as far as the ecosystem permits and generate a machine-readable software bill of materials for releases.
- **SEC-010 — Security scanning:** Run no-cost dependency-vulnerability, secret, and static-analysis scanning locally and in the approved CI path.
- **SEC-011 — Warning policy:** Compiler and approved analyzer warnings are errors in production projects and release builds. Suppressions require scoped written justification.
- **SEC-012 — Consent for communication:** Do not add telemetry, uploads, cloud processing, update communication, or other external communication without explicit user consent and documented disable/offline behavior.
- **SEC-013 — Vulnerability process:** Document private vulnerability reporting, triage severity, response targets, dependency-update review, emergency patching, and disclosure procedures.

## 6. Installation and Usability Requirements

- **INSTALL-001 — Delivery forms:** Provide a simple installer and, where technically practical, a portable distribution. Any impracticality must be documented with evidence and user approval.
- **INSTALL-002 — Privilege:** Avoid administrator access unless a specific component genuinely requires it; explain and test every elevation boundary.
- **INSTALL-003 — Prerequisite checker:** Check .NET, Blender, Unity, Unreal, disk space, permissions, project-root containment, and required modules before a build begins.
- **INSTALL-004 — Licence control:** Never silently install an engine, accept a third-party licence, or imply licence eligibility on the user's behalf.
- **INSTALL-005 — First run and repair:** Provide guided first-run setup, contained tool discovery, actionable missing-prerequisite messages, and documented repair paths.
- **INSTALL-006 — Safe uninstall:** Clean uninstall must preserve user projects, source assets, generated packages, release artifacts, and any other data the user has not explicitly chosen to remove.
- **INSTALL-007 — Lifecycle tests:** Test fresh installation, repair, supported upgrade, downgrade prevention, uninstall, retained-data behavior, and failed/interrupted lifecycle operations.
- **INSTALL-008 — Diagnostic export:** Provide an in-application diagnostic report that the user can review and export, with secrets and sensitive paths redacted.
- **INSTALL-009 — Development containment:** During development, every Package Builder file and all project-owned state must remain beneath `C:\Dev\PackageBuilder`.
- **INSTALL-010 — Free editor workflow:** Required development, build, test, run, debug, diagnostic, and packaging workflows must remain usable with free tooling and Visual Studio Code.

## 7. Engineering Quality Requirements

- **ENG-001 — Compiler baseline:** Enable nullable reference types, deterministic builds, continuous-integration build semantics, and strict supported .NET analyzers.
- **ENG-002 — Warnings:** Treat compiler and analyzer warnings as errors in production projects and release configurations.
- **ENG-003 — Domain isolation:** Domain logic must remain independent from WPF, Blender, Unity, Unreal, persistence, filesystem implementations, and marketplace adapters.
- **ENG-004 — Explicit contracts:** Use typed, versioned contracts, dependency injection at composition boundaries, and explicit result/error types for expected failures.
- **ENG-005 — Decisions:** Record important architectural, security, compatibility, dependency, installation, privacy, and quality decisions using ADRs.
- **ENG-006 — Review checklist:** Code review must explicitly cover correctness, mapped tests, UX/accessibility, performance evidence, security/threat-model impact, containment, licences, and documentation.
- **ENG-007 — Evidence-based claims:** Documentation, UI, reports, releases, and marketing must not claim “best practice,” “secure,” “fast,” or “production ready” without corresponding current evidence.

## 8. Release-Blocking Gates

A release is blocked when any of these conditions is true:

- **REL-001 — Traceability:** A normative requirement or PB acceptance criterion has no mapped test, regardless of any supplementary manual or documentary verification.
- **REL-002 — Test failure:** A required automated test, manual verification, clean reimport/reopen, or engine fixture validation fails or is missing.
- **REL-003 — Coverage:** Line, branch, critical-code branch, or approved mutation thresholds are not met, or an exclusion lacks user approval.
- **REL-004 — Vulnerability:** A critical or high vulnerability remains without a time-bounded, explicitly user-approved exception.
- **REL-005 — Performance:** An approved time, memory, disk, or regression budget is exceeded without an explicitly user-approved exception.
- **REL-006 — Accessibility:** An accessibility-critical or keyboard-only workflow fails.
- **REL-007 — Installation:** Required installation, repair, upgrade, downgrade-prevention, uninstall, privilege, or retained-data validation fails.
- **REL-008 — Package integrity:** A generated package fails content integrity, validation-report consistency, clean engine import/reopen, or unexpected-content scanning.

Release evaluation is fail-closed: missing, stale, unreadable, or contradictory evidence is a blocking result. Exceptions must name the requirement, risk, scope, approver, expiry, and follow-up task. Coverage exclusions and PB completion still require explicit user approval under `AGENTS.md`.

## 9. Requirements-to-Tests Traceability Matrix

PB-0013 owns this permanent baseline and its cross-document consistency. The matrix is maintained at individual requirement and PB acceptance-criterion granularity; PB-1801 owns the complete criterion-level inventory and validator. The initial permanent-requirement implementation ownership is:

| Requirement IDs | Backlog owner | Required planned evidence |
|---|---|---|
| UX-001–UX-003 | PB-1802 | Design-system specification, component tests, guided-workflow acceptance tests |
| UX-004–UX-007 | PB-1804 | Dry-run, progress, error, input-preservation, cancellation, retry, and recovery tests |
| UX-008–UX-009 | PB-1803 | First-time usability study and deterministic UI/accessibility automation |
| TEST-001–TEST-003 | PB-1801 | Criterion-level traceability matrix and test-portfolio audit |
| TEST-004–TEST-006 | PB-1805 | Five-case/three-target fixture matrix plus hostile, boundary, and recovery suites |
| TEST-007–TEST-010 | PB-1806 | Line/branch reports, threshold enforcement, trend report, approved-exclusion register |
| TEST-011 | PB-1807 | Mutation reports and reviewed surviving-mutant register |
| TEST-012–TEST-013 | PB-1805 | Repeated offline runs, isolated network-test classification, evidence-completeness review |
| PERF-001–PERF-002, PERF-007–PERF-008 | PB-1808 | Approved fixture budgets, benchmark reports, regression results, build-resource reports |
| PERF-003–PERF-006 | PB-1809 | Streaming, bounded-concurrency, cancellation, cache, corruption, and copy-count tests |
| SEC-001–SEC-004 | PB-1810 | Threat model and malicious archive/model/texture/script/engine-project tests |
| SEC-005–SEC-008, SEC-012 | PB-1811 | Process-isolation, timeout, redaction, secret, download-verification, consent, and offline tests |
| SEC-009–SEC-011, SEC-013 | PB-1812 | Dependency locks, SBOM, vulnerability/secret/static scans, warning-free builds, vulnerability procedure |
| INSTALL-001–INSTALL-010 | PB-1813 | Installer/portable decision, prerequisite checks, first-run, diagnostics, privilege, lifecycle, preservation, containment, and VS Code tests |
| ENG-001–ENG-007 | PB-1814 | Build configuration tests, dependency-boundary tests, ADR audit, review checklist, evidence-claim audit |
| REL-001–REL-008 | PB-1815 | Fail-closed release-gate tests for every blocking condition and evidence bundle |

Each final matrix row must include at least one concrete test ID rather than only a backlog ID. Supplementary manual or documentary verification IDs may also be recorded but cannot replace the test. A PB task cannot be marked complete until the user confirms its required commit, push, CI, and merge gates after the mapped evidence passes.

## 10. Test and Evidence Portfolio

Required evidence types include:

- Unit tests for pure policies, validation, naming, manifests, paths, and state transitions.
- Contract tests for schemas, worker protocols, version compatibility, and explicit error results.
- Integration tests for contained filesystem operations, archives, persistence, processes, downloads, and tool adapters.
- End-to-end tests for all five product cases and every applicable target.
- UI and accessibility tests for critical first-run and build workflows.
- Regression tests for corrected defects, tool upgrades, marketplace-profile changes, and visual output.
- Installer and upgrade tests on clean and previously installed environments.
- Failure-recovery tests for cancellation, timeouts, process crashes, power/interruption simulations where practical, corrupt caches, and partial lifecycle operations.
- Security tests derived from the threat model.
- Performance benchmarks with approved fixture sizes and machine profiles.

Generated evidence belongs beneath `artifacts` or `logs` and remains outside Git. Small schemas, test source, approved baselines, traceability records, and legally approved fixtures may be source-controlled in their documented locations.

## 11. Performance Budget Record

PB-1808 must approve numeric values for each applicable stage and fixture class using this record:

| Field | Required value |
|---|---|
| Fixture class | Small, medium, or large |
| Fixture identity | Version and SHA-256 |
| Machine profile | CPU, memory, storage, OS, engine/tool versions |
| Stage/target | Named pipeline stage, portable, Unity, Unreal, or complete build |
| Maximum elapsed time | Numeric duration and allowed variance |
| Maximum peak memory | Numeric working-set/private-byte limit and measurement method |
| Maximum project disk | Numeric peak and final byte limits |
| Maximum temporary space | Numeric contained temporary-space limit |
| Regression threshold | Numeric percentage and minimum absolute change |
| Samples | Warm-up and measured-run counts |
| Approver and date | Explicit user approval record |

## 12. Release Evidence Bundle

Every release candidate must produce a contained, reviewable evidence bundle with:

- Exact commit and pinned tool/dependency versions.
- Requirements-to-tests matrix with no unmapped required row.
- Test, coverage, mutation, analyzer, vulnerability, secret-scan, static-analysis, licence, and SBOM results.
- Five-case target/fixture results and clean reimport/reopen evidence.
- Accessibility and representative-user evidence.
- Performance budget and regression results.
- Installer/portable, privilege, prerequisite, repair, upgrade, downgrade-prevention, uninstall, and retained-data results.
- Package hashes, integrity checks, unexpected-content scan, validation reports, and generated-artifact inventory.
- All approved exceptions with expiry and follow-up work.

The release gate must be runnable with the documented free local or self-hosted workflow. Hosted services may mirror the checks but must not be the only way to obtain required evidence.
