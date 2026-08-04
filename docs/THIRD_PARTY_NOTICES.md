# Third-Party Dependency Notices

This record documents direct runtime dependencies introduced by Package Builder tasks. Exact
resolved direct and transitive versions and NuGet content hashes remain enforced by each
project's tracked `packages.lock.json`.

| Package | Relationship | Approved version | Licence | Purpose |
|---|---|---:|---|---|
| [JsonSchema.Net](https://www.nuget.org/packages/JsonSchema.Net/9.3.0) | Direct runtime dependency of `PackageBuilder.Contracts` | 9.3.0 | [MIT](https://github.com/json-everything/json-everything/blob/master/LICENSE) | Offline JSON Schema Draft 2020-12 evaluation |
| [Microsoft.Data.Sqlite](https://www.nuget.org/packages/Microsoft.Data.Sqlite/10.0.10) | Direct runtime dependency of `PackageBuilder.Infrastructure` | 10.0.10 | [MIT](https://github.com/dotnet/efcore/blob/main/LICENSE.txt) | Serverless SQLite connections, transactions, and consistent backups |
| [SQLitePCLRaw.lib.e_sqlite3](https://www.nuget.org/packages/SQLitePCLRaw.lib.e_sqlite3/2.1.12) | Direct security pin for the native runtime used by `PackageBuilder.Infrastructure` | 2.1.12 | [Apache-2.0](https://licenses.nuget.org/Apache-2.0) | Replaces the vulnerable transitive 2.1.11 native SQLite library |
| [CommunityToolkit.Mvvm](https://www.nuget.org/packages/CommunityToolkit.Mvvm/8.4.2) | Direct runtime dependency of `PackageBuilder.App.Wpf` | 8.4.2 | [MIT](https://github.com/CommunityToolkit/dotnet/blob/main/License.md) | Observable view-model infrastructure without placing application policy in WPF |
| [Microsoft.Extensions.Hosting](https://www.nuget.org/packages/Microsoft.Extensions.Hosting/10.0.10) | Direct runtime dependency of `PackageBuilder.App.Wpf` | 10.0.10 | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) | Local dependency injection and desktop host lifetime composition |

JsonSchema.Net 9.3.0 resolves JsonPointer.Net 7.0.1, Json.More.Net 3.0.1, and Humanizer.Core
3.0.10 transitively in the current lock file. NuGet restore is locked; production builds must use
`--locked-mode`. These dependencies are permissively licensed and require no paid IDE, hosted
service, subscription, telemetry, or network access at application runtime.

Microsoft.Data.Sqlite 10.0.10 resolves its managed provider and bundle transitively. Package
Builder directly pins `SQLitePCLRaw.lib.e_sqlite3` 2.1.12 because the default 2.1.11 native
dependency is affected by a high-severity advisory. The locked restore and vulnerability audit
must reject any regression to the vulnerable native version.

CommunityToolkit.Mvvm 8.4.2 and Microsoft.Extensions.Hosting 10.0.10 are used only by the local
desktop presentation composition. They add no telemetry, remote service, paid subscription, cloud
processing, or runtime network requirement. Exact transitive dependencies and hashes remain locked
in `src/PackageBuilder.App.Wpf/packages.lock.json`.
