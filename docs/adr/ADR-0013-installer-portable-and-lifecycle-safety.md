# ADR-0013: Installer, Portable Distribution, and Lifecycle Safety

## Status

Accepted

## Date

2026-07-24

## Context

Productization must provide simple setup, a portable form where technically practical, prerequisite detection, first-run guidance, repair, upgrades, downgrade prevention, diagnostics, and safe uninstall. Installation must not silently install Blender, Unity, or Unreal, accept vendor licences, imply licence eligibility, or delete user projects and generated outputs.

The architecture lists no-cost MSIX and permissively licensed Velopack as candidates, but the selection and evidence are explicitly assigned to PB-1612 after the desktop workflow and lifecycle requirements are sufficiently developed.

## Decision

Adopt the installation and lifecycle safety requirements now, while deferring installer and update technology selection to PB-1612. No installer or update technology is selected by this ADR.

The selected solution must:

- Have a free local build and test path and remain operable from Visual Studio Code.
- Provide a simple installer and a portable distribution where technically practical, or retain an evidenced user-approved exception.
- Avoid administrator access unless a specific component genuinely requires elevation, with every boundary explained and tested.
- Detect .NET, Blender, Unity, Unreal, required modules, contained paths, disk space, and permissions before builds.
- Never silently install engines, accept third-party licences, or imply vendor eligibility.
- Support guided first run, repair, supported upgrade, downgrade prevention, interrupted-operation recovery, and redacted diagnostics.
- Preserve user projects, source assets, generated packages, release artifacts, and all data not explicitly selected for removal during uninstall.
- Keep Package Builder project-owned state beneath `C:\Dev\PackageBuilder` for the approved development workspace.

Blender, Unity, and Unreal are not redistributed with Package Builder. Engine vendor licensing, eligibility, seat, and royalty conditions remain external responsibilities that the integration and documentation must disclose.

## Alternatives Considered

- Select MSIX immediately. PB-1612 owns the evidence-based comparison, including signing, updates, rollback, prerequisites, containment, and data preservation.
- Select Velopack immediately. The same PB-1612 comparison and licensing review is required before commitment.
- Provide an installer only and omit portable delivery without evidence. This conflicts with the normative requirement to provide a portable option where technically practical.
- Bundle engines or silently accept licences. This violates vendor and user control.
- Require administrator rights by default. Elevation is allowed only for a proven component need.
- Remove all project data during uninstall. User-owned and generated data must be preserved unless explicitly selected.

## Consequences and Trade-offs

- Lifecycle safety and evidence requirements guide productization without prematurely locking technology.
- PB-1612 must compare candidates against the complete approved criteria rather than selecting on packaging convenience alone.
- Supporting both installer and portable forms increases testing and documentation effort.
- Engine prerequisites remain separate, potentially large, licence-sensitive installations.
- Code signing may use an operator-supplied certificate, but purchasing one cannot become a local development or test prerequisite.

## Migration or Evolution Considerations

PB-1612 records the technology selection in a later ADR and links back to this requirements decision. Future installer or updater changes require migration, rollback, privilege, interruption, uninstall, and retained-data evidence across supported prior versions.

## Implementation Status and Follow-up Work

Acceptance records the architecture direction; it does not indicate that implementation is complete. No installer, updater, portable distribution, first-run flow, repair, upgrade, diagnostic export, or uninstall capability is claimed as implemented. PB-1612 selects technology; PB-1613 implements distribution; PB-1614 documents operation; and PB-1813 validates the complete lifecycle.

## Related Documentation

- [Project rules](../../AGENTS.md)
- [Product and implementation plan](../Package_Builder_Plan.md)
- [Technology stack and architecture](../TECH_STACK_AND_ARCHITECTURE.md)
- [Implementation backlog](../IMPLEMENTATION_BACKLOG.md)
- [Quality and release gates](../QUALITY_AND_RELEASE_GATES.md)
