# Third-Party Dependency Notices

This record documents direct runtime dependencies introduced by Package Builder tasks. Exact
resolved direct and transitive versions and NuGet content hashes remain enforced by each
project's tracked `packages.lock.json`.

| Package | Relationship | Approved version | Licence | Purpose |
|---|---|---:|---|---|
| [JsonSchema.Net](https://www.nuget.org/packages/JsonSchema.Net/9.3.0) | Direct runtime dependency of `PackageBuilder.Contracts` | 9.3.0 | [MIT](https://github.com/json-everything/json-everything/blob/master/LICENSE) | Offline JSON Schema Draft 2020-12 evaluation |

JsonSchema.Net 9.3.0 resolves JsonPointer.Net 7.0.1, Json.More.Net 3.0.1, and Humanizer.Core
3.0.10 transitively in the current lock file. NuGet restore is locked; production builds must use
`--locked-mode`. These dependencies are permissively licensed and require no paid IDE, hosted
service, subscription, telemetry, or network access at application runtime.
