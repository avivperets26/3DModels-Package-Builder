# ADR-0006: SQLite Build History

## Status

Accepted

## Date

2026-07-24

## Context

The desktop application and CLI need durable local records for products, versions, jobs, state transitions, steps, artifacts, findings, installed tools, approved engine versions, requirements profiles, and settings. The required workflow is local-first and must not require a server, paid database, subscription, or hosted service. Large binary assets and generated packages already have a contained filesystem artifact store.

## Decision

Use SQLite through `Microsoft.Data.Sqlite` for local build-history metadata. Store large artifacts in the contained artifact store and reference them from SQLite by typed path, size, role, and SHA-256 identity.

Use versioned, transactional database migrations. Back up the database before an upgrade and test recovery. Keep the database and its backups beneath `C:\Dev\PackageBuilder\runtime-data`; they remain local project state and outside Git.

## Alternatives Considered

- Store all history in independent JSON files. This is inspectable but makes transactional state transitions, related queries, migrations, and concurrent reads more difficult.
- Require a client/server database. It adds installation, administration, network, cost, and availability burdens that the local workstation product does not need.
- Store binary artifacts inside SQLite. Large engine projects and packages would make backup, streaming, and artifact lifecycle management less practical.
- Keep history only in logs. Logs are diagnostic streams, not a normalized durable state model.

## Consequences and Trade-offs

- SQLite supplies transactions and structured local queries without a server dependency.
- Metadata and binary lifecycle remain separated, so the database stays smaller and artifact files can be streamed.
- Schema migration, backup, corruption recovery, and locking behavior require dedicated tests.
- Paths stored in metadata must be contained, portable within the project root where practical, and verified before use.

## Migration or Evolution Considerations

Keep repository interfaces in the application contracts so storage can evolve without entering the domain layer. Version every schema migration, retain backup evidence, and support explicit recovery. A different store would require an ADR and a tested migration path.

## Implementation Status and Follow-up Work

Acceptance records the architecture direction; it does not indicate that implementation is complete. No build-history database or repositories are claimed as implemented. PB-0210 and PB-0211 own the initial schema, migrations, persistence behavior, recovery, and tests.

## Related Documentation

- [Technology stack and architecture](../TECH_STACK_AND_ARCHITECTURE.md)
- [Implementation backlog](../IMPLEMENTATION_BACKLOG.md)
- [Quality and release gates](../QUALITY_AND_RELEASE_GATES.md)
