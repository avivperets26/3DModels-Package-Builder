# ADR-0009: Requirements Traceability and Release Evidence

## Status

Accepted

## Date

2026-07-24

## Context

Package Builder has normative product, UX, testing, performance, security, installation, engineering, and release requirements plus task-specific PB acceptance criteria. A build result, test count, review statement, coverage percentage, or mutation score cannot demonstrate by itself that each required behavior is satisfied.

Release decisions also depend on evidence from external engines, clean import or reopen tests, accessibility, performance, security, installation lifecycle, and package integrity. Missing, stale, unreadable, contradictory, or commit-mismatched evidence must not be interpreted as success.

## Decision

Maintain criterion-level requirements-to-tests traceability. Every normative requirement and every PB acceptance criterion maps to an owner, at least one concrete test ID, fixture, evidence location, and current status. Approved manual or documentary verification may supplement but never replace the required test.

Produce a commit- and tool-bound release evidence bundle containing the traceability matrix and all required test, coverage, mutation, benchmark, accessibility, usability, analyzer, vulnerability, secret, static-analysis, licence, SBOM, installer, package-integrity, and clean import or reopen results.

Evaluate releases fail-closed. Every `REL-001` through `REL-008` blocker in the quality gates prevents release when evidence fails or is missing. Exceptions require the requirement, risk, scope, explicit user approval, expiry, and follow-up task. The evaluator may inspect evidence but cannot commit, stage, push, merge, tag, create a pull request, publish, or release automatically; Git and remote actions remain under explicit user control.

## Alternatives Considered

- Treat passing CI or a test count as completion evidence. This can hide unmapped requirements and untested acceptance criteria.
- Use coverage percentage as the primary quality decision. Coverage measures execution, not requirement correctness or test strength.
- Rely only on review or manual checklists. Manual evidence can supplement but cannot replace repeatable tests.
- Allow release when evidence is unavailable. This converts uncertainty into an unsupported success claim.

## Consequences and Trade-offs

- Every completion and release claim can be traced to current concrete evidence.
- Evidence schemas, identities, freshness, exceptions, and storage require maintenance.
- The evidence portfolio is broader and slower than a build-only gate, especially for engines, UI, installers, and representative-user validation.
- Fail-closed behavior can delay a release when infrastructure or evidence is missing, which is the intended safety outcome.
- Large generated reports remain beneath ignored `artifacts` or `logs`, while small source-controlled schemas and traceability records remain public-repository safe.

## Migration or Evolution Considerations

Add new requirements and PB criteria to the matrix without reusing IDs. Version evidence schemas and migrate retained records explicitly. Preserve historical evidence while requiring the current release candidate to use current, matching evidence and approved exceptions.

## Implementation Status and Follow-up Work

Acceptance records the architecture direction; it does not indicate that implementation is complete. PB-1801 owns complete criterion-level traceability, PB-1815 owns the fail-closed evaluator and evidence bundle, and other E18 tasks own their evidence producers. Current foundation CI is not the complete release gate.

## Related Documentation

- [Project rules](../../AGENTS.md)
- [Implementation backlog](../IMPLEMENTATION_BACKLOG.md)
- [Quality and release gates](../QUALITY_AND_RELEASE_GATES.md)
- [Technology stack and architecture](../TECH_STACK_AND_ARCHITECTURE.md)
