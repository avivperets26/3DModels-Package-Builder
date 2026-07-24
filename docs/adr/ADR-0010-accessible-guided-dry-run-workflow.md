# ADR-0010: Accessible Guided Dry-run Workflow

## Status

Accepted

## Date

2026-07-24

## Context

Package Builder coordinates complex, long-running, and sometimes ambiguous model-processing work. First-time users must understand detected product cases, naming, material roles, rig and animation choices, targets, engine versions, warnings, and resource implications before files are generated. Failure and cancellation must not erase reviewed input or expose partial output as success.

The desktop workflow has permanent keyboard, screen-reader, high-contrast, scalable-text, visible-focus, progress, error, and recovery requirements. Accessibility and usability are acceptance requirements rather than optional presentation polish.

## Decision

Use one documented accessible WPF design system and a guided setup-to-results workflow with sensible defaults and progressive disclosure. Critical workflows must support keyboard-only operation, screen readers, high contrast, scalable text, meaningful accessible names, predictable focus order, and visible focus.

Make dry run an application use case shared with the CLI, not a visual mock. Before a file-changing or package-generating operation, resolve the same manifest, canonical contained paths, proposed names, tool versions, actions, outputs, warnings, and resource estimates used by execution, without changing sources or generating target files.

During execution, expose current stage, measurable progress, elapsed time, safe cancellation, and correlation identity. Errors identify the failed step or asset, consequence, and practical correction instead of presenting a raw stack trace as the primary message. Preserve reviewed user input after recoverable failure and disclose what retry or resume will repeat.

## Alternatives Considered

- Provide only an advanced configuration form. This would expose complexity without guiding first-time users or controlling ambiguous decisions.
- Treat dry run as a static summary unrelated to execution. It could diverge from the real plan and provide false confidence.
- Add accessibility after feature completion. This would make keyboard, automation, semantics, focus, and layout expensive to retrofit and would violate the normative baseline.
- Show raw engine logs as the main error experience. Logs are retained diagnostics, not actionable user guidance.

## Consequences and Trade-offs

- Application services must model plans, progress, cancellation, errors, and retry safety independently of WPF.
- UI components and critical journeys require deterministic automation plus representative first-time-user validation.
- Progressive disclosure adds state and design-system work but reduces accidental misconfiguration.
- Dry-run identity and before/after hashes make unintended source or target changes testable.
- Preserving user input requires separation between reviewed configuration and transient job state.

## Migration or Evolution Considerations

Keep the design system versioned and reusable. Add new targets and advanced options through the same accessible patterns. Any future presentation technology must preserve application-level dry run, progress, cancellation, error, and recovery contracts.

## Implementation Status and Follow-up Work

Acceptance records the architecture direction; it does not indicate that implementation is complete. No production WPF workflow or dry-run capability is claimed as implemented. E13 and PB-1802 through PB-1804 own the design system, accessible journeys, automation, representative-user validation, and recovery behavior.

## Related Documentation

- [Product and implementation plan](../Package_Builder_Plan.md)
- [Technology stack and architecture](../TECH_STACK_AND_ARCHITECTURE.md)
- [Implementation backlog](../IMPLEMENTATION_BACKLOG.md)
- [Quality and release gates](../QUALITY_AND_RELEASE_GATES.md)
