# ADR-0015: Typed Documentation Templates

## Status

Accepted

## Date

2026-09-10

## Context

PB-0901–PB-0903 need repeatable common documentation from measured target results and configurable
publisher identity. The existing PB-0504 renderer contains portable case-specific formatting but
does not accept complete technical metrics. Domain profiles and strict versioned JSON already exist.

## Decision

Pin Scriban 7.4.0 in central NuGet configuration and transitive lock files. Its BSD-2-Clause
license permits the required no-cost local workflow. Preserve its license in third-party notices.
Use an Application-owned pure rendering service with one reviewed embedded UTF-8 template.
Only strings and explicitly projected ScriptObject/ScriptArray containers reach the engine.
Do not accept caller-supplied templates, expose CLR objects/delegates, install loaders, or expose
built-in functions. Escape Markdown and HTML metacharacters in data. Use strict missing-variable
and member behavior, bounded inputs/loops/output, per-render contexts and cancellation.

Resolve exact publisher references through the existing configuration I/O and schema/domain
contracts. A null selection uses the caller's configured default; an explicit unknown selection
fails. Branding remains a logical source reference and cannot trigger network/file execution.
Do not infer copyright years from the system clock or default AI disclosure to no assistance.

Shared sections consume typed target facts: delivered files/formats/versions, measured metre
dimensions, triangle/material/texture counts, scale, axes, pivot, tested engine/pipeline versions,
dependencies and usage. Required facts cannot be missing. No engine process is launched by rendering.

## Alternatives Considered

- Extending StringBuilder everywhere keeps formatting coupled to target adapters and duplicates
  common sections.
- Razor runtime compilation introduces executable C# and a larger authority boundary than needed.
- Stubble's MIT-licensed Mustache implementation is viable, but Scriban provides explicit runtime
  limits and actively maintained strict rendering APIs. The host still restricts template authority.

## Consequences and Trade-offs

Application now has one pinned pure text dependency; Domain and Contracts remain independent of it.
Portable references Application to consume common rendering and publisher prose. Template changes
require a reviewed application update. Markdown entities preserve literal user text when rendered;
raw Markdown source displays entities. Version and measurement accuracy depends on validated target
results supplied by the caller; the renderer is not an inspection engine.

## Migration or Evolution Considerations

PB-0904 owns case-specific variants. Preserve the earlier portable request overload for existing
case-aware consumers, sharing publisher prose now. The new measured overload returns the existing
portable document type for artifact composition. Future target workflows should supply measured
build data, not manufacture zero metrics to call the new overload. Publisher manager UI and broader
engine/naming profile defaults remain PB-1303 and their existing target configuration owners.

## Implementation Status and Follow-up Work

Acceptance records the architecture direction; it does not indicate that implementation is complete.
PB-0901, PB-0902 and PB-0903 share one explicitly approved branch and retain separate lifecycle
and acceptance records. See the [implementation evidence](../PB-0901_PB-0903_DOCUMENTATION_EVIDENCE.md).
No new engine clean-import claim is implied by documentation-only verification.

## Related Documentation

- [Product plan](../Package_Builder_Plan.md)
- [Architecture](../TECH_STACK_AND_ARCHITECTURE.md)
- [Backlog](../IMPLEMENTATION_BACKLOG.md)
- [Quality gates](../QUALITY_AND_RELEASE_GATES.md)
- [Scriban 7.4.0 release](https://github.com/scriban/scriban/releases/tag/7.4.0)
- [Pinned license](https://raw.githubusercontent.com/scriban/scriban/7.4.0/license.txt)
- [Runtime exposure model](https://scriban.github.io/docs/runtime/)
- [Runtime controls](https://scriban.github.io/docs/runtime/safe-runtime/)
