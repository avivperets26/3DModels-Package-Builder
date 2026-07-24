# ADR-0002: External Engine Workers

## Status

Accepted

## Date

2026-07-24

## Context

Blender, Unity, and Unreal use different runtimes, native APIs, licensing conditions, memory profiles, project formats, and failure modes. Package Builder must use those native APIs, select exact approved engine versions, preserve logs, support timeouts and cancellation, and prevent an engine crash from corrupting the orchestrator or a completed release.

Unity and Unreal integrations must disclose their external licensing, eligibility, seat, and royalty conditions. Package Builder must not automate editor mouse clicks, silently accept licences, or assume that an operator is eligible for a vendor tier.

## Decision

Run Blender, Unity, and Unreal as external engine workers started by the .NET orchestrator:

- Blender uses its bundled Python in background mode.
- Unity uses a C# Editor assembly through batch mode and `UnityEditor` APIs.
- Unreal initially uses Unreal Python and editor scripting through command-line execution.

Each worker runs in an isolated contained job or engine-project clone, receives an explicit versioned request, produces structured progress and a result, and has separate logs, timeouts, cancellation, and cleanup. Executable, working, temporary, cache, and log paths must remain beneath `C:\Dev\PackageBuilder`. Engine installations and licences are external prerequisites governed by their vendors.

## Alternatives Considered

- Load engine assemblies into the desktop process. Their incompatible runtimes and editor lifecycles would undermine version selection, isolation, and reliable recovery.
- Automate editor UI clicks. UI automation is fragile and conflicts with the approved use of native engine APIs.
- Require a remote build service. That would violate the required local/offline path and introduce network, privacy, cost, and service dependencies.
- Use an Unreal C++ editor plugin immediately. Python is the approved initial path; a narrowly scoped C++ module is allowed only if required APIs prove unavailable or unreliable.

## Consequences and Trade-offs

- Engine crashes and memory pressure are isolated from the desktop process.
- Exact version selection, per-worker timeouts, cancellation, logs, and clean project clones become enforceable boundaries.
- Worker startup has overhead and requires versioned contracts plus careful process cleanup.
- Integration testing needs actual vendor engines and must respect their licensing and runner eligibility.
- No engine worker may write into original source files or another job's staging area.

## Migration or Evolution Considerations

Keep worker contracts independent of transport and engine implementation. A future remote execution service may wrap the same contracts only after explicit privacy, consent, security, and offline-behavior decisions. Add Unreal C++ only for evidenced API gaps.

## Implementation Status and Follow-up Work

Acceptance records the architecture direction; it does not indicate that implementation is complete. No Blender, Unity, or Unreal worker functionality is claimed by this ADR. Worker contracts, process execution, engine discovery, templates, integrations, and licensed fixture validation remain later PB tasks.

## Related Documentation

- [Product and implementation plan](../Package_Builder_Plan.md)
- [Technology stack and architecture](../TECH_STACK_AND_ARCHITECTURE.md)
- [Implementation backlog](../IMPLEMENTATION_BACKLOG.md)
- [Quality and release gates](../QUALITY_AND_RELEASE_GATES.md)
