# ADR-0007: Compiled-in Adapters for Version 1

## Status

Accepted

## Date

2026-07-24

## Context

Package Builder separates the domain and application layers from Blender tooling, portable targets, Unity, Unreal, and marketplace-specific behavior. Version 1 needs independently testable adapters, but loading arbitrary third-party assemblies would introduce signing, trust, compatibility, dependency, and execution risks before an extension security policy exists.

Publisher names such as `AvivPeretsFBX` are configuration values. Engine targets and marketplace profiles must remain distinct, and platform-specific behavior must stay behind the portable, Unity, Unreal, and marketplace adapter boundaries.

## Decision

Compile version 1 adapters with Package Builder and register them through dependency injection at composition boundaries. Define adapter interfaces in the contracts layer while keeping implementations in dedicated projects:

- Blender as a normalization/tool adapter.
- Portable, Unity, and Unreal as target adapters.
- Fab and future stores as marketplace adapters.

Do not load arbitrary third-party DLL adapters in version 1. Keep publisher identity, folder roots, documentation text, branding, and naming policy in validated configuration rather than hard-coding a publisher into an adapter.

## Alternatives Considered

- Load third-party assemblies from a plugin directory. This is deferred until signing, compatibility, isolation, update, and security policies are designed and tested.
- Put Unity, Unreal, and Fab logic directly in the core. This would couple the domain to vendor APIs and marketplace rules.
- Use runtime marketplace scripts from imported packages. Executing untrusted input scripts violates the security baseline.
- Hard-code the initial publisher or Fab rules. This would prevent reusable publisher profiles and independently versioned marketplace requirements.

## Consequences and Trade-offs

- Adapter code is reviewed, version-pinned, and tested with the application release.
- Domain and application code can depend on stable interfaces rather than vendor implementations.
- Adding or updating an adapter requires a Package Builder build and release.
- Arbitrary extension ecosystems are unavailable in version 1, reducing flexibility while avoiding an unapproved code-loading trust boundary.
- Configurable publisher profiles remain independent from adapter implementation.

## Migration or Evolution Considerations

A future plugin model requires a separate ADR covering signatures, trust, permissions, compatibility, isolation, update policy, failure behavior, and public-repository safety. Existing compiled adapters should continue to implement the same inward-facing contracts.

## Implementation Status and Follow-up Work

Acceptance records the architecture direction; it does not indicate that implementation is complete. The project skeleton reflects adapter boundaries, but adapter behavior, dependency registration, publisher-profile resolution, architecture tests, and any future plugin policy remain later PB tasks.

## Related Documentation

- [Project rules](../../AGENTS.md)
- [Product and implementation plan](../Package_Builder_Plan.md)
- [Technology stack and architecture](../TECH_STACK_AND_ARCHITECTURE.md)
- [Implementation backlog](../IMPLEMENTATION_BACKLOG.md)
