# ADR-0012: Quality Toolchain and Thresholds

## Status

Accepted

## Date

2026-07-24

## Context

Package Builder's release decision requires more than compilation and smoke tests. The normative baseline requires criterion-level traceability, complete test layers and product-target fixtures, coverage, mutation testing, performance budgets, accessibility and usability evidence, security and supply-chain checks, installer lifecycle validation, package integrity, and a fail-closed evidence evaluator.

Every required tool must have a free local or self-hosted workflow, be version-pinned, operate from Visual Studio Code and repository scripts, and keep project-owned state beneath `C:\Dev\PackageBuilder`. Paid SaaS and paid IDE features cannot be mandatory.

`docs/QUALITY_AND_RELEASE_GATES.md` is the normative source for the exact 68 stable requirement IDs. This ADR records the selected toolchain categories and thresholds without creating alternate requirement definitions.

## Decision

Adopt the following evidence categories and permanent thresholds:

- At least 90% line coverage and 85% branch coverage overall.
- 100% branch coverage for security validation, path handling, naming, manifest validation, and package-integrity code.
- Approved mutation-score thresholds for critical validation and security components, with no unapproved surviving high-risk mutant.
- User-approved numeric time, memory, project-disk, temporary-space, and regression budgets for small, medium, and large fixtures.
- Deterministic offline unit, contract, integration, end-to-end, UI, regression, installer, upgrade, failure-recovery, hostile-input, engine-fixture, and clean import or reopen tests.
- Accessibility automation and representative first-time-user evidence for critical workflows.
- Warning-free release builds, dependency-vulnerability, secret, static-analysis, licence, SBOM, installer-lifecycle, and package-integrity evidence.

Use pinned no-cost tools that fit the approved stack: Coverlet-compatible coverage collection and a free report generator; Stryker.NET or an approved no-cost equivalent for mutation testing; BenchmarkDotNet or an approved equivalent plus end-to-end resource measurement; Windows UI Automation with a permissively licensed driver such as FlaUI; and pinned no-cost audit, secret-scan, static-analysis, licence, and SBOM tools. Later PB tasks approve exact versions and configurations before enforcement.

Percentages and tool results supplement rather than replace criterion-level requirements-to-tests evidence. The same required gates must be runnable locally or on no-cost self-hosted infrastructure; hosted mirrors are optional.

The canonical release blockers are REL-001 through REL-008 in `docs/QUALITY_AND_RELEASE_GATES.md`; missing, stale, unreadable, contradictory, or failing evidence blocks release. Git and remote operations remain under explicit user control through `AGENTS.md`.

## Alternatives Considered

- Use only xUnit results and code coverage. This omits test strength, engines, accessibility, performance, security, installation, and package integrity.
- Require a paid hosted quality platform. That conflicts with the free local or self-hosted workflow.
- Set no numeric thresholds until release. The permanent coverage thresholds are already normative; performance and mutation numbers follow their measured approval tasks.
- Treat warnings, vulnerabilities, or missing evidence as advisory. The approved release model is fail-closed.

## Consequences and Trade-offs

- Quality claims require broad, reproducible evidence tied to the release commit and tool lock.
- The portfolio has significant runtime and maintenance cost, especially engine, UI, installer, mutation, and benchmark suites.
- Tool versions, exclusions, baselines, budgets, and exceptions require explicit review and retained evidence.
- Some exact tool selections remain deliberately deferred to the PB task that installs and proves them.
- No single metric can override an unmapped requirement or failing acceptance criterion.

## Migration or Evolution Considerations

Promote tool versions through pinned, reviewed updates. Preserve trend history and approved exclusions. Threshold changes require written technical justification and explicit user approval; new evidence categories require traceability and fail-closed integration.

## Implementation Status and Follow-up Work

Acceptance records the architecture direction; it does not indicate that implementation is complete. Current foundation CI supplies build, formatting, lint, repository, and smoke-test checks only. PB-1801 through PB-1815 implement the complete portfolio, approve mutation and performance numbers, and build the final evaluator.

## Related Documentation

- [Project rules](../../AGENTS.md)
- [Technology stack and architecture](../TECH_STACK_AND_ARCHITECTURE.md)
- [Implementation backlog](../IMPLEMENTATION_BACKLOG.md)
- [Quality and release gates](../QUALITY_AND_RELEASE_GATES.md)
