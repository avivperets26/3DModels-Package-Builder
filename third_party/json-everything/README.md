# Independently compiled JSON validation libraries

Package Builder compiles these libraries from MIT-licensed source. It does not restore or
redistribute the publisher's JsonSchema.Net, JsonPointer.Net or Json.More.Net NuGet binaries.
No subscription or revenue eligibility is required for this source distribution.

| Library | Source version | Assembly name |
| --- | --- | --- |
| JsonSchema.Net | 9.4.0 | JsonSchema.Net |
| JsonPointer.Net | 7.0.2 | JsonPointer.Net |
| Json.More.Net | 3.0.1 | Json.More |

## Provenance and licence

All three come from upstream commit
`399f198431f65cf6896fe6038f833ef6d0b27a39` in
[json-everything/json-everything](https://github.com/json-everything/json-everything/tree/399f198431f65cf6896fe6038f833ef6d0b27a39).
The original [MIT licence and copyright notice](upstream/LICENSE) are retained.
The [source manifest](source-manifest.json) records each imported file's SHA-256 after the
sole transformation, CRLF-to-LF normalization. The pinned GitHub source ZIP has SHA-256
`4fe5fcc230fe5fa81bff6b63379990f44b0ac94904d33048878a4e4c6e48593e`.

The publisher's [binary EULA](https://github.com/json-everything/json-everything/blob/399f198431f65cf6896fe6038f833ef6d0b27a39/OSMFEULA.txt)
distinguishes independently compiled MIT source from its prebuilt binaries. We use the former.
These unsigned assemblies retain upstream namespaces and assembly versions for compatibility;
their informational versions explicitly identify the Package Builder MIT build and source commit.
They are not official upstream signed binaries or packages.

Humanizer.Core 3.0.10 remains a NuGet dependency, centrally pinned and content-hash locked. Its
package declares MIT, copyright (c) .NET Foundation and Contributors, with no licence acceptance
requirement. It is not one of the publisher's JSON binary packages.

## Build and deployment

Use the repository-pinned .NET SDK 10.0.302 and normal commands from the repository root:

```powershell
. .\scripts\Enter-PackageBuilderEnvironment.ps1
& .\scripts\Test-JsonSchemaDistribution.ps1
dotnet restore .\PackageBuilder.sln --locked-mode
dotnet build .\PackageBuilder.sln --configuration Release --no-restore
dotnet publish .\src\PackageBuilder.Cli\PackageBuilder.Cli.csproj --configuration Release --no-restore --output .\artifacts\publish\cli
```

No extra bootstrap, source download, package feed, paid IDE or external service is needed.
Initial NuGet restore needs nuget.org for the remaining permissive dependencies; builds and
schema evaluation use local source and resources. Publish the complete output, including
`ThirdPartyNotices/json-everything/LICENSE`, `source-manifest.json`, and this README.
The normal framework-dependent output requires the matching .NET runtime on the destination.
Engine installation and licensing are separate from these libraries.

## Deliberate build differences

- Three reviewed SDK projects replace upstream build/pack targets. They build only `net10.0`,
  matching Package Builder; other upstream target frameworks are not claimed or shipped.
- No upstream signing key, prebuilt DLL, package, source generator, download target or post-build
  documentation copier is imported. PolySharp is unnecessary for these sources on .NET 10;
  SourceLink is build metadata, and provenance is supplied by the checked-in manifest instead.
- Compile exclusions and meta-schema resources match the upstream JsonSchema project.
  English diagnostic resources are included, matching the previously used base package.
  Optional localization satellites are not distributed.
- The upstream source is preserved instead of rewritten to project style. Application analyzers
  remain unchanged; only these vendor projects disable .NET analyzers. Compiler warnings still
  fail the build. Formatting commands exclude `third_party`; source hashes guard that boundary.
- Deterministic compilation and a fixed path map remove checkout locations from vendor compiler
  inputs. These projects are not packable; redistribution is through application publish output.

## Updating the source

The dependency-maintenance owner must review the new source licence and transitive binary terms,
pin an immutable commit and archive checksum, compare imported files, and regenerate the manifest.
Import only the reviewed C# files, meta-schema JSON files, English resource and MIT licence.
Never import upstream build targets or signing material automatically. Review version metadata,
resource/compile exclusions and Humanizer's pin with every update. Update the inventory check
intentionally if the source file count changes.

Then refresh and review all affected lock files, run the distribution and negative-fixture checks,
the full core pipeline, a clean build/reproducibility check and application publish verification.
Update dependency notices, architecture and task evidence. Dependabot proposals for the removed
publisher JSON packages must be superseded by this reviewed source-update workflow.
