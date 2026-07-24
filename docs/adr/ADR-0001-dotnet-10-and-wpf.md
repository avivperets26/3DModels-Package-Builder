# ADR-0001: .NET 10 LTS and WPF

## Status

Accepted

## Date

2026-07-24

## Context

Package Builder is planned as a local-first Windows workstation application whose main responsibilities are safe filesystem orchestration, typed manifests, external process control, build history, validation, and an accessible desktop workflow. The same application services must also support a command-line interface. The required development path must work from Visual Studio Code with free local tooling and must not depend on paid Visual Studio features.

The approved baseline already pins repository-local .NET SDK `10.0.302`, identifies .NET 10 as the approved LTS line, and defines C# 14 and a Windows-native desktop presentation layer.

## Decision

Use .NET 10 LTS and C# 14 for the core application, contracts, infrastructure, CLI, and Windows desktop application. Use WPF for the version 1 desktop presentation layer and MVVM to keep UI state separate from domain and application behavior. The WPF application and CLI will call the same application services.

Repository scripts and the `dotnet` CLI are the supported build, test, run, and debug path from Visual Studio Code. Paid Visual Studio and paid extensions may be optional conveniences but are not required dependencies.

## Alternatives Considered

- Use Avalonia for the initial desktop UI. This would improve cross-platform presentation options, but macOS and Linux are not current product requirements. Avalonia remains an evolution path if those requirements are approved.
- Use an embedded browser desktop shell. This would add a browser runtime without providing a current requirement that justifies it.
- Depend on paid Visual Studio designers or test tooling. This conflicts with the approved free Visual Studio Code workflow.

## Consequences and Trade-offs

- .NET provides the required process, filesystem, serialization, dependency-injection, and Windows desktop capabilities in one maintained platform.
- WPF aligns with the Windows-only version 1 scope and avoids an embedded browser runtime.
- WPF is not cross-platform. A future non-Windows UI would require a replacement presentation project, while the domain, application, contracts, infrastructure, workers, and CLI remain reusable.
- UI logic must remain outside the domain and application layers so the CLI and future presentation options behave consistently.

## Migration or Evolution Considerations

Keep presentation interfaces and composition boundaries explicit. If a non-Windows product requirement is approved, add or replace the presentation project without changing worker contracts or core use cases. Promote a later .NET LTS only through the approved version-pinning and validation process.

## Implementation Status and Follow-up Work

Acceptance records the architecture direction; it does not indicate that implementation is complete. The repository currently contains the pinned SDK and project skeleton. WPF composition, MVVM workflow, CLI behavior, accessibility, and product use cases remain assigned to later PB tasks, including E13, E14, and E18.

## Related Documentation

- [Product and implementation plan](../Package_Builder_Plan.md)
- [Technology stack and architecture](../TECH_STACK_AND_ARCHITECTURE.md)
- [Implementation backlog](../IMPLEMENTATION_BACKLOG.md)
- [Project rules](../../AGENTS.md)
