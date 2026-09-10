# Third-Party Dependency Notices

This record documents direct runtime dependencies introduced by Package Builder tasks. Exact
resolved direct and transitive versions and NuGet content hashes remain enforced by each
project's tracked `packages.lock.json`.

| Package | Relationship | Approved version | Licence | Purpose |
|---|---|---:|---|---|
| [JsonSchema.Net source](../third_party/json-everything/README.md) | Source project referenced by `PackageBuilder.Contracts` | 9.4.0 | [MIT](../third_party/json-everything/upstream/LICENSE) | Offline JSON Schema Draft 2020-12 evaluation |
| JsonPointer.Net source | Source dependency of JsonSchema.Net | 7.0.2 | MIT, same source commit and notice | JSON Pointer resolution |
| Json.More.Net source | Source dependency of JsonPointer.Net | 3.0.1 | MIT, same source commit and notice | JSON primitives |
| [Humanizer.Core](https://www.nuget.org/packages/Humanizer.Core/3.0.10) | NuGet dependency of JsonPointer.Net source | 3.0.10 | MIT | English ordinal formatting |
| [Microsoft.Data.Sqlite](https://www.nuget.org/packages/Microsoft.Data.Sqlite/10.0.11) | Direct runtime dependency of `PackageBuilder.Infrastructure` | 10.0.11 | [MIT](https://github.com/dotnet/efcore/blob/main/LICENSE.txt) | Serverless SQLite connections, transactions, and consistent backups |
| [SQLitePCLRaw.lib.e_sqlite3](https://www.nuget.org/packages/SQLitePCLRaw.lib.e_sqlite3/3.53.3) | Direct security pin for the native runtime used by `PackageBuilder.Infrastructure` | 3.53.3 | [Public domain](https://www.nuget.org/packages/SQLitePCLRaw.lib.e_sqlite3/3.53.3/License) | Replaces the vulnerable transitive 2.1.11 native SQLite library |
| [CommunityToolkit.Mvvm](https://www.nuget.org/packages/CommunityToolkit.Mvvm/8.4.2) | Direct runtime dependency of `PackageBuilder.App.Wpf` | 8.4.2 | [MIT](https://github.com/CommunityToolkit/dotnet/blob/main/License.md) | Observable view-model infrastructure without placing application policy in WPF |
| [Microsoft.Extensions.Hosting](https://www.nuget.org/packages/Microsoft.Extensions.Hosting/10.0.11) | Direct runtime dependency of `PackageBuilder.App.Wpf` | 10.0.11 | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) | Local dependency injection and desktop host lifetime composition |

PB-0015 replaces all three publisher JSON binary packages with independently compiled MIT
source pinned to commit `399f198431f65cf6896fe6038f833ef6d0b27a39`. The publisher's binary
OSMFEULA terms are not used as the distribution path. No revenue eligibility or subscription is
assumed. Humanizer.Core's NuGet package declares MIT and no licence-acceptance requirement.
Normal build and publish output carries the upstream MIT copyright/permission notice, provenance
manifest and distribution README under `ThirdPartyNotices/json-everything`. Preserve that folder
when distributing the application. See [source distribution instructions](../third_party/json-everything/README.md).
The application continues to evaluate schemas offline. NuGet restores must use `--locked-mode`.

Microsoft.Data.Sqlite 10.0.11 resolves its managed provider and bundle transitively. Package
Builder directly pins `SQLitePCLRaw.lib.e_sqlite3` 3.53.3. The updated managed provider resolves the 2.1.12 bundle; the historical 2.1.11 native dependency had a high-severity advisory. The locked restore and vulnerability audit
must reject any regression to the vulnerable native version.

CommunityToolkit.Mvvm 8.4.2 and Microsoft.Extensions.Hosting 10.0.11 are used only by the local
desktop presentation composition. They add no telemetry, remote service, paid subscription, cloud
processing, or runtime network requirement. Exact transitive dependencies and hashes remain locked
in `src/PackageBuilder.App.Wpf/packages.lock.json`.


## September 2026 maintenance

PB-0014 reviews the five outstanding Dependabot proposals. Hosting and Microsoft.Data.Sqlite
are pinned to 10.0.11, Microsoft.NET.Test.Sdk to 18.9.0, and the native SQLite binary to 3.53.3.
The managed SQLitePCLRaw bundle/provider remains at the version resolved by Microsoft.Data.Sqlite;
the native library is directly pinned to 3.53.3. The inspected Windows x64 DLL is byte-identical to the previous 2.1.12 package (both report SQLite 3.53.3); this package change is not claimed as a newer Windows engine or an additional security fix. The publisher JsonSchema.Net 9.4.0 binary proposal was deferred; PB-0015 adopts its MIT source instead.
See [the review and validation evidence](PB-0014_DEPENDENCY_MAINTENANCE_EVIDENCE.md).
