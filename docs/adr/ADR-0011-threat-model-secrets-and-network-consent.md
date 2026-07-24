# ADR-0011: Threat Model, Secrets, and Network Consent

## Status

Accepted

## Date

2026-07-24

## Context

Package Builder will process untrusted archives, FBX and GLB files, textures, scripts or executable content, engine projects, plugins, managed downloads, external tools, and generated packages. The approved repository is public, so every tracked file is publicly visible. Project inputs and diagnostics may also contain private assets, credentials, personal paths, or customer data that must not leak into Git, reports, or packages.

The product is local and offline by default. Telemetry, uploads, cloud processing, update communication, and other outbound communication change privacy and trust boundaries and therefore require explicit user consent.

## Decision

Maintain a versioned threat model mapping each trust boundary and threat to controls and tests. Treat imported files and archives as untrusted; never execute embedded scripts or executables. Prevent and test path traversal, archive bombs and decompression abuse, symlink or reparse-point escape, duplicate canonical destinations, filename collisions, command injection, argument confusion, unsafe processes, and resource exhaustion.

Never place tokens, credentials, private keys, personal data, customer assets, private configuration, or unlicensed marketplace content in source control. Keep large downloads, engines, caches, logs, runtime state, generated packages, and private assets beneath ignored repository-local directories. Redact secrets and sensitive paths from logs, reports, diagnostics, support bundles, process records, and user-facing errors.

Pin managed dependencies and downloads and verify vendor checksums and digital signatures where available. Run external tools with explicit literal arguments, least practical privilege, contained isolated directories, bounded timeouts, cancellation, and verified cleanup.

Keep the application and default tests local and offline. Do not add telemetry, uploads, cloud processing, update communication, or other outbound communication without explicit user consent, purpose disclosure, and documented disable and offline behavior.

## Alternatives Considered

- Trust source assets because they are expected to be models. File formats, archives, engine projects, and plugins can still contain malicious or resource-exhausting content.
- Store secrets in manifests or repository configuration for convenience. The public repository and generated artifacts make that unacceptable.
- Enable telemetry or cloud processing by default. This conflicts with explicit consent, privacy, and offline operation.
- Execute imported helper scripts inside engine projects. Imported content is untrusted and cannot become executable merely because an engine supports scripts.

## Consequences and Trade-offs

- Security work is driven by an explicit threat model and repeatable hostile-input suites.
- Strict preflight and containment can reject inputs that engines might otherwise attempt to open; findings must be actionable.
- Offline defaults and consent boundaries limit automatic online convenience but protect privacy and reproducibility.
- Redaction, secret scanning, download verification, SBOM, dependency review, and vulnerability procedures require maintained evidence.
- Public-repository safeguards apply to documentation examples and small fixtures as well as source code.

## Migration or Evolution Considerations

Review the threat model when adding formats, plugins, engines, downloads, network features, marketplace communication, or diagnostic data. A new outbound feature requires documented purpose, data classification, consent, disable behavior, offline behavior, tests, and an updated threat model before implementation.

## Implementation Status and Follow-up Work

Acceptance records the architecture direction; it does not indicate that implementation is complete. Current foundation rules and repository validators cover a limited public-repository baseline. Full threat modeling, hostile-input controls, redaction, download verification, SBOM, local security scanning, consent enforcement, and vulnerability procedures remain E15, PB-1611, PB-1810, PB-1811, and PB-1812 work.

## Related Documentation

- [Project rules](../../AGENTS.md)
- [Security reporting policy](../../SECURITY.md)
- [Technology stack and architecture](../TECH_STACK_AND_ARCHITECTURE.md)
- [Quality and release gates](../QUALITY_AND_RELEASE_GATES.md)
