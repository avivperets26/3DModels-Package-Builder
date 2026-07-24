# ADR-0004: Immutable Staging and Atomic Promotion

## Status

Accepted

## Date

2026-07-24

## Context

Package Builder processes untrusted and potentially large source assets through multiple tools. Original inputs must never be modified, failed or cancelled work must never appear as a release, and a worker crash must not corrupt an already completed release. All project-owned state must remain beneath the single project root, `C:\Dev\PackageBuilder`.

## Decision

Copy accepted inputs into an immutable, hashed source snapshot inside a contained job. Every worker writes only to its assigned staging or template-clone directory. Final artifacts remain staged until all requested validation, package-integrity, and clean reimport or reopen checks pass.

After final reports and manifests are written, promote the complete release directory atomically into `artifacts/Builds`. Never expose partial or failed output as a successful release. Validate source, staging, temporary, cache, log, and output paths before reading, creating, deleting, moving, or replacing project-owned files.

Hard links may be used for a source snapshot only when safety is proven; otherwise inputs are copied. Cache reuse cannot bypass immutable-source, validation, containment, or exact-version rules.

## Alternatives Considered

- Modify or normalize original downloads in place. This violates source safety and makes recovery and reproducibility unreliable.
- Write directly into the final release directory. Users could observe or consume partial output after a crash or failed validation.
- Use the user profile, a sibling data directory, or the system temporary directory. This violates the approved single-root containment policy.
- Use hard links unconditionally. A later write through a link could modify the original source.

## Consequences and Trade-offs

- Source integrity and completed releases are protected from worker failure and cancellation.
- Hashes and immutable snapshots support reproducibility, diagnosis, and cache identity.
- Staging needs additional disk space and cleanup policy.
- Atomic promotion requires the staging and final location to support an atomic directory move and requires collision and interruption tests.
- Copy minimization must be evidence-led and may not weaken source safety.

## Migration or Evolution Considerations

Introduce safe copy optimizations only after streaming, link behavior, cache identity, corruption recovery, and filesystem semantics are tested. Preserve source hashes and promotion evidence across storage-layout migrations.

## Implementation Status and Follow-up Work

Acceptance records the architecture direction; it does not indicate that implementation is complete. Immutable snapshots, safe archives, staging management, resource guards, cache behavior, and atomic promotion remain PB-0201 through PB-0215 and E15/E18 work.

## Related Documentation

- [Project rules](../../AGENTS.md)
- [Product and implementation plan](../Package_Builder_Plan.md)
- [Technology stack and architecture](../TECH_STACK_AND_ARCHITECTURE.md)
- [Quality and release gates](../QUALITY_AND_RELEASE_GATES.md)
