# ADR-0003: JSON File Worker Protocol

## Status

Accepted

## Date

2026-07-24

## Context

External engine workers need a protocol that is versioned, inspectable, recoverable after a process exits unexpectedly, and usable across .NET, Blender Python, Unity C#, and Unreal Python. Progress must be streamable while the final request and result remain available for diagnosis and retry-safety decisions.

## Decision

Version 1 workers receive one versioned JSON request file path, emit one JSON object per line for progress, findings, and metrics, and write one versioned JSON result file before exit. Requests and results include protocol version, job identity, operation, contained paths, exact engine version, artifacts, hashes, findings, metrics, logs, and retry-safety information.

Unknown protocol versions fail explicitly. Expected worker failures cross the boundary as structured result data rather than relying on unhandled exceptions or unstructured log text.

## Alternatives Considered

- Use gRPC for version 1. It adds transport and generated-code complexity that is not required for local child processes; it remains a possible wrapper for a separately approved remote-build service.
- Use only standard input and standard output. A process crash could leave no durable final request or result record.
- Parse human-readable engine logs as the primary contract. Log formats are not stable typed interfaces and cannot reliably represent retry safety or artifact identity.
- Share engine-specific objects across process boundaries. Those objects are incompatible across the selected runtimes and would couple the core to vendor APIs.

## Consequences and Trade-offs

- Requests and results are easy to inspect, retain, validate, and replay in contained test jobs.
- JSON Lines provides incremental progress without making the final result dependent on the output stream remaining intact.
- Schemas and DTO compatibility must be tested, and unknown fields or versions need explicit policy.
- Filesystem access becomes part of the protocol boundary, so every path requires canonicalization and root-containment validation.

## Migration or Evolution Considerations

Evolve contracts through explicit protocol and schema versions with compatibility tests and migrations where approved. A future transport may carry the same logical contracts without changing the domain model. Do not silently reinterpret an unknown version.

## Implementation Status and Follow-up Work

Acceptance records the architecture direction; it does not indicate that implementation is complete. Request, progress, and result schemas; typed DTOs; JSON Lines parsing; cancellation semantics; and engine entrypoints remain assigned to PB-0112, PB-0209, and later worker tasks.

## Related Documentation

- [Technology stack and architecture](../TECH_STACK_AND_ARCHITECTURE.md)
- [Implementation backlog](../IMPLEMENTATION_BACKLOG.md)
- [Quality and release gates](../QUALITY_AND_RELEASE_GATES.md)
