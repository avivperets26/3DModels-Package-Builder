# Architecture Decision Record Index

Architecture Decision Records preserve important Package Builder decisions, their alternatives, and their trade-offs. The product plan and normative quality gates remain authoritative requirements; ADRs explain selected architecture without expanding product scope.

## Status Convention

- **Proposed** — under review and not yet accepted.
- **Accepted** — approved as the current architecture direction.
- **Deprecated** — retained for history but no longer recommended.
- **Superseded** — replaced by a later ADR that links back to the earlier record.

Acceptance records the architecture direction; it does not indicate that implementation is complete. Each ADR therefore records separate implementation status and follow-up work.

## Initial ADR Inventory

1. [ADR-0001: .NET 10 LTS and WPF](ADR-0001-dotnet-10-and-wpf.md) — **Accepted**
2. [ADR-0002: External Engine Workers](ADR-0002-external-engine-workers.md) — **Accepted**
3. [ADR-0003: JSON File Worker Protocol](ADR-0003-json-file-worker-protocol.md) — **Accepted**
4. [ADR-0004: Immutable Staging and Atomic Promotion](ADR-0004-immutable-staging-and-atomic-promotion.md) — **Accepted**
5. [ADR-0005: Latest Approved Stable Engine Policy](ADR-0005-latest-approved-stable-engine-policy.md) — **Accepted**
6. [ADR-0006: SQLite Build History](ADR-0006-sqlite-build-history.md) — **Accepted**
7. [ADR-0007: Compiled-in Adapters for Version 1](ADR-0007-compiled-in-adapters-for-v1.md) — **Accepted**
8. [ADR-0008: Marketplace Requirements Profiles](ADR-0008-marketplace-requirements-profiles.md) — **Accepted**
9. [ADR-0009: Requirements Traceability and Release Evidence](ADR-0009-requirements-traceability-and-release-evidence.md) — **Accepted**
10. [ADR-0010: Accessible Guided Dry-run Workflow](ADR-0010-accessible-guided-dry-run-workflow.md) — **Accepted**
11. [ADR-0011: Threat Model, Secrets, and Network Consent](ADR-0011-threat-model-secrets-and-network-consent.md) — **Accepted**
12. [ADR-0012: Quality Toolchain and Thresholds](ADR-0012-quality-toolchain-and-thresholds.md) — **Accepted**
13. [ADR-0013: Installer, Portable Distribution, and Lifecycle Safety](ADR-0013-installer-portable-and-lifecycle-safety.md) — **Accepted**

## Evolution Rules

- Preserve an accepted ADR when the implementation is incomplete; update its implementation section or link a follow-up task without rewriting the decision as delivered functionality.
- Record a materially changed decision in a new sequential ADR and mark the earlier record superseded when appropriate.
- Preserve deliberately deferred selections. In particular, installer and update technology remains a PB-1612 decision.
- Keep links to the [product plan](../Package_Builder_Plan.md), [architecture](../TECH_STACK_AND_ARCHITECTURE.md), [backlog](../IMPLEMENTATION_BACKLOG.md), and [quality gates](../QUALITY_AND_RELEASE_GATES.md) current.
- Validate the inventory with [`scripts/Test-ArchitectureDecisionRecords.ps1`](../../scripts/Test-ArchitectureDecisionRecords.ps1).
