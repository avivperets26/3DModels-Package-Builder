# ADR-0008: Marketplace Requirements Profiles

## Status

Accepted

## Date

2026-07-24

## Context

Portable, Unity, and Unreal targets describe deliverables. Marketplaces describe changing archive, folder, media, documentation, listing, and supported-version rules for already validated target artifacts. Fab is the first planned marketplace, but its rules can change independently of Package Builder and engine releases.

Version 1 publishing remains a deliberate manual action. The product must not automatically upload or submit listings, and marketplace communication must not become a mandatory network or paid-service dependency.

## Decision

Represent marketplace requirements in independently versioned profiles with adapter identity, profile version, effective date, official source links, required and optional targets, media constraints, archive rules, folder and naming validators, documentation and disclosure requirements, and supported engine-version ranges.

Keep marketplace adapters outside the domain and engine target implementations. A build records the exact requirements-profile version used. Candidate profile updates pass structure, media, documentation, engine, and five-case compatibility validation before promotion. Fab is the first profile, not a hard-coded core assumption.

## Alternatives Considered

- Hard-code current Fab rules in target builders. This would mix marketplace policy with engine artifact creation and require core changes whenever Fab changes.
- Treat an engine target and marketplace package as the same adapter. One target can serve several marketplaces, and one marketplace can require several targets.
- Fetch current marketplace rules during every build. This would undermine offline reproducibility and make an external service a required dependency.
- Automatically upload approved output. Version 1 explicitly preserves manual publishing and user control.

## Consequences and Trade-offs

- Marketplace rules can evolve without redesigning the core or engine workers.
- Completed builds remain reproducible because they retain the exact profile identity and sources.
- Profiles require maintenance, review dates, candidate testing, and promotion evidence.
- Cached rules can become stale, so the UI and reports must disclose profile version and effective date.
- The adapter validates presentation and packaging; it does not create meshes, rigs, materials, or engine-native assets.

## Migration or Evolution Considerations

Add future marketplaces through new compiled adapters and versioned profiles. Migrate profile schemas explicitly, retain old profiles needed to reproduce completed builds, and require approval before a candidate becomes the default.

## Implementation Status and Follow-up Work

Acceptance records the architecture direction; it does not indicate that implementation is complete. No Fab requirements profile, updater, validator, release composer, or upload capability is claimed as implemented. E10 and PB-1610 own that work, and manual marketplace publication remains outside automated release composition.

## Related Documentation

- [Product and implementation plan](../Package_Builder_Plan.md)
- [Technology stack and architecture](../TECH_STACK_AND_ARCHITECTURE.md)
- [Implementation backlog](../IMPLEMENTATION_BACKLOG.md)
- [Quality and release gates](../QUALITY_AND_RELEASE_GATES.md)
