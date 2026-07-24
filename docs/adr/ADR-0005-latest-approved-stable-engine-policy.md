# ADR-0005: Latest Approved Stable Engine Policy

## Status

Accepted

## Date

2026-07-24

## Context

Package Builder must adopt current production engine releases promptly while keeping builds reproducible and protecting users from an unverified upgrade. A version number being installed or numerically highest does not prove that it is stable, compatible with all five product cases, permitted by a marketplace profile, or suitable for the required modules.

Engine products have external licensing, eligibility, seat, and royalty conditions. Package Builder must not silently install a large engine, accept its licence, or imply eligibility for an operator.

## Decision

Use **Latest Approved Stable** as the default engine policy:

1. Discover vendor-identified production, Update, or LTS candidates.
2. Exclude alpha, beta, preview, experimental, and release-candidate versions from production defaults.
3. Require the requested modules and marketplace compatibility.
4. Run the five-case compatibility, material, preview, export, and clean reimport or reopen suite.
5. Promote a candidate only after the required suite passes and review is recorded.
6. Retain the prior approved version as Last Known Good when a newer candidate fails.

Every completed build records exact Package Builder, schema, worker, Blender, Unity, Unreal, and marketplace-profile versions. Version values shown in planning documents are examples or current pins, not permanent future defaults.

## Alternatives Considered

- Select the highest installed version. Installation alone provides no compatibility or stability evidence.
- Pin one engine family forever. This would prevent the approved prompt adoption of tested stable releases and marketplace changes.
- Select vendor preview releases for production. Preview versions are explicitly outside the approved production policy.
- Auto-install engines or accept licences. This would violate operator control and vendor licensing boundaries.

## Consequences and Trade-offs

- New production candidates do not become defaults without repeatable evidence.
- Failed upgrades fall back to a known compatible version rather than blocking all builds.
- Exact pins preserve reproducibility even as the default moves forward.
- Maintaining multiple engine families, templates, fixtures, and candidate evidence requires disk space and test time.
- Marketplace profiles may constrain an otherwise approved engine version.

## Migration or Evolution Considerations

Promote new version families through the documented lifecycle: Discovered, Installed, Candidate, Approved Latest or Rejected, and Last Known Good. Never downgrade an engine project in place; rebuild from normalized interchange sources for each target version.

## Implementation Status and Follow-up Work

Acceptance records the architecture direction; it does not indicate that implementation is complete. Engine discovery, official release catalogs, approval-state persistence, build locks, update guidance, candidate suites, and current stable Unreal installation remain E03, PB-1101, and E16 work.

## Related Documentation

- [Project rules](../../AGENTS.md)
- [Product and implementation plan](../Package_Builder_Plan.md)
- [Technology stack and architecture](../TECH_STACK_AND_ARCHITECTURE.md)
- [Implementation backlog](../IMPLEMENTATION_BACKLOG.md)
