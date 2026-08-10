# ADR-0014: Shared Interactive Preview Experience

## Status

Accepted

## Date

2026-08-10

## Context

Unity and Unreal overview scenes must offer equivalent camera, lighting, item-selection, animation,
overlay, and accessibility behavior even though their input, UI, animation, and renderer APIs are
different. The desktop application must configure the same intent without becoming a third source
of interaction rules. PB-0906 already owns engine-neutral presentation roles and approved
dark-studio background and lighting tokens, while the animation domain already owns source clip
duration and loop metadata.

Copying defaults and state transitions into each engine would allow behavior to drift, make edge
states inconsistent, and risk changing packaged product transforms or animation import settings.
Sharing engine objects or UI frameworks would instead leak platform dependencies into the Domain.

## Decision

Define contract version 1 in `PackageBuilder.Domain.Preview` as immutable engine-neutral policy and
state types. The aggregate reuses PB-0906 background and lighting values and defines:

- bounded yaw/pitch orbit, camera-distance zoom, and reset, with no product transform or scale;
- normalized pointer and keyboard bindings for every shared action;
- bounded key-light direction adjustment and reset while colour and intensity remain presentation
  tokens;
- hide/show overlay state with a labelled focusable restore control;
- deterministic item Previous, Next, direct, and all-items states, including explicit wrapping and
  safe empty/single-item behavior;
- animation selection, play, pause/resume, replay, bounded scrub, preview-loop, current-time, and
  duration state, projecting duration and source loop defaults from `AnimationDefinition`;
- stable labels, accessible names/help, visible-focus requirement, and unique focus order.

Store the portable representation as strict schema-version-1 JSON in
`PackageBuilder.Contracts.Preview`. Serialization uses a fixed property order, preserves semantic
collection order, rejects duplicate or unknown properties, validates JSON Schema Draft 2020-12,
and revalidates Domain invariants. A versioned JSON fixture contains shared transition vectors for
engine-adapter conformance.

Unity, Unreal, and WPF translate the shared contract into their native APIs. They do not redefine
defaults or transitions. Preview loop selection and item visibility are transient presentation
state and never mutate packaged source animation or prefab assets.

## Alternatives Considered

- Keep independent Unity and Unreal controller behavior. This minimizes initial wiring but creates
  duplicate rules and makes cross-engine parity unprovable.
- Put Unity or Unreal types in the shared contract. This prevents the other engine and desktop UI
  from consuming the model and reverses the required dependency direction.
- Serialize engine-specific configuration only. This cannot provide one deterministic portable
  contract or shared test vectors.
- Treat accessibility as adapter-only presentation. Labels, focus order, keyboard equivalence, and
  hidden-overlay recovery would then drift between targets.

## Consequences and Trade-offs

- Engine adapters require explicit mapping code, but that code is limited to native API adaptation.
- Contract evolution must use a new version and schema rather than changing version-one semantics.
- Fixed action identities and state transitions make parity tests deterministic and offline.
- The Domain contains no renderer, scene, filesystem, WPF, Unity, or Unreal dependencies.
- Shared state cannot express arbitrary engine-only features unless a later cross-engine contract
  version approves them.

## Migration or Evolution Considerations

Version-one JSON remains readable and immutable. Additive or breaking interaction changes require
an explicit migration decision, a new schema/contract version where necessary, updated shared
vectors, and conformance work in every consuming adapter. Engine-only extensions remain isolated
and must not shadow a shared action or alter its semantics.

## Implementation Status and Follow-up Work

Acceptance records the architecture direction; it does not indicate that implementation is complete.
PB-0913 locally implements and validates the version-one Domain policies and state machines,
strict Contracts serialization/schema, accessibility descriptors, and shared vectors. PB-0709 and PB-0808 consume
them in Unity; PB-1116, PB-1205, and PB-1208 consume them in Unreal; PB-1312 consumes the contract
in the desktop preview review screen; PB-0914 owns cross-engine interaction and visual-parity
validation. Publication, required `main` CI, and explicit completion confirmation remain pending.

## Related Documentation

- [Product and implementation plan](../Package_Builder_Plan.md)
- [Technology stack and architecture](../TECH_STACK_AND_ARCHITECTURE.md)
- [Implementation backlog](../IMPLEMENTATION_BACKLOG.md)
- [PB-0913 implementation evidence](../PB-0913_INTERACTIVE_PREVIEW_CONTRACT_EVIDENCE.md)
- [Quality and release gates](../QUALITY_AND_RELEASE_GATES.md)
