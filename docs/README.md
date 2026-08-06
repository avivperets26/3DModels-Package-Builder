# Package Builder Documentation

This index links the approved product, architecture, quality, workflow, and architecture-decision sources for Package Builder. Repository, core domain/contracts, contained infrastructure, tool-version management through PB-0310, the first WPF shell, and Blender worker work through PB-0405 are complete. PB-0406 texture, PB-0407 rig/weight, and PB-0408 animation inspection are active in one explicitly approved publication cycle. Separate glTF dependency graphs, normalization/export, packaging workflows, and the remaining desktop screens are not claimed as implemented.

## Primary Documentation

- [Project rules](../AGENTS.md)
- [Root project overview](../README.md)
- [Contribution workflow](../CONTRIBUTING.md)
- [Security reporting policy](../SECURITY.md)
- [Product and implementation plan](Package_Builder_Plan.md)
- [Technology stack and architecture](TECH_STACK_AND_ARCHITECTURE.md)
- [Implementation backlog](IMPLEMENTATION_BACKLOG.md)
- [Quality and release gates](QUALITY_AND_RELEASE_GATES.md)

## Architecture Decision Records

All initial ADRs have status **Accepted**. Acceptance records the architecture direction; it does not indicate that implementation is complete.

1. [ADR-0001: .NET 10 LTS and WPF](adr/ADR-0001-dotnet-10-and-wpf.md)
2. [ADR-0002: External Engine Workers](adr/ADR-0002-external-engine-workers.md)
3. [ADR-0003: JSON File Worker Protocol](adr/ADR-0003-json-file-worker-protocol.md)
4. [ADR-0004: Immutable Staging and Atomic Promotion](adr/ADR-0004-immutable-staging-and-atomic-promotion.md)
5. [ADR-0005: Latest Approved Stable Engine Policy](adr/ADR-0005-latest-approved-stable-engine-policy.md)
6. [ADR-0006: SQLite Build History](adr/ADR-0006-sqlite-build-history.md)
7. [ADR-0007: Compiled-in Adapters for Version 1](adr/ADR-0007-compiled-in-adapters-for-v1.md)
8. [ADR-0008: Marketplace Requirements Profiles](adr/ADR-0008-marketplace-requirements-profiles.md)
9. [ADR-0009: Requirements Traceability and Release Evidence](adr/ADR-0009-requirements-traceability-and-release-evidence.md)
10. [ADR-0010: Accessible Guided Dry-run Workflow](adr/ADR-0010-accessible-guided-dry-run-workflow.md)
11. [ADR-0011: Threat Model, Secrets, and Network Consent](adr/ADR-0011-threat-model-secrets-and-network-consent.md)
12. [ADR-0012: Quality Toolchain and Thresholds](adr/ADR-0012-quality-toolchain-and-thresholds.md)
13. [ADR-0013: Installer, Portable Distribution, and Lifecycle Safety](adr/ADR-0013-installer-portable-and-lifecycle-safety.md)

The [ADR index](adr/README.md) explains status and evolution conventions.

## Foundation Evidence

- [PB-0013 quality and release-gate evidence](PB-0013_QUALITY_RELEASE_GATES_EVIDENCE.md)
- [PB-0012 initial ADR evidence](PB-0012_INITIAL_ADRS_EVIDENCE.md)
- [PB-0011 GitHub governance evidence](PB-0011_GITHUB_GOVERNANCE_EVIDENCE.md)
- [PB-0010 contribution workflow evidence](PB-0010_CONTRIBUTION_WORKFLOW_EVIDENCE.md)
- [PB-0009 core CI evidence](PB-0009_CORE_CI_EVIDENCE.md)

## Domain Evidence

- [PB-0101 product identity and naming evidence](PB-0101_PRODUCT_IDENTITY_EVIDENCE.md)
- [PB-0102 product cases and targets evidence](PB-0102_PRODUCT_CASES_TARGETS_EVIDENCE.md)
- [PB-0103 source assets and textures evidence](PB-0103_SOURCE_ASSETS_TEXTURES_EVIDENCE.md)
- [PB-0104 material domain evidence](PB-0104_MATERIAL_DOMAIN_EVIDENCE.md)
- [PB-0105 rig and animation domain evidence](PB-0105_RIG_ANIMATION_DOMAIN_EVIDENCE.md)
- [PB-0106 set and collection domain evidence](PB-0106_SET_COLLECTION_DOMAIN_EVIDENCE.md)
- [PB-0107 publisher and marketplace profile domain evidence](PB-0107_PROFILE_DOMAIN_EVIDENCE.md)
- [PB-0108 build job domain evidence](PB-0108_BUILD_JOB_DOMAIN_EVIDENCE.md)
- [PB-0109 validation finding evidence](PB-0109_VALIDATION_FINDINGS_EVIDENCE.md)
- [PB-0110 product manifest schema evidence](PB-0110_PRODUCT_MANIFEST_SCHEMA_EVIDENCE.md)
- [PB-0111 profile schema evidence](PB-0111_PROFILE_SCHEMAS_EVIDENCE.md)
- [PB-0112 worker contract evidence](PB-0112_WORKER_CONTRACTS_EVIDENCE.md)
- [PB-0113 manifest/profile migration evidence](PB-0113_SCHEMA_MIGRATIONS_EVIDENCE.md)

## Infrastructure Evidence

- [PB-0201 configuration and path-root validation evidence](PB-0201_CONFIGURATION_PATHS_EVIDENCE.md)
- [PB-0202 safe archive inspection and extraction evidence](PB-0202_SAFE_ARCHIVE_EVIDENCE.md)
- [PB-0203 immutable source snapshot evidence](PB-0203_SOURCE_SNAPSHOT_EVIDENCE.md)
- [PB-0204 streamed artifact identity evidence](PB-0204_ARTIFACT_HASHING_EVIDENCE.md)
- [PB-0205 artifact-store layout and metadata evidence](PB-0205_ARTIFACT_STORE_EVIDENCE.md)
- [PB-0206 atomic release promotion evidence](PB-0206_ATOMIC_PROMOTION_EVIDENCE.md)
- [PB-0207 structured external process runner evidence](PB-0207_STRUCTURED_PROCESS_RUNNER_EVIDENCE.md)
- [PB-0208 process cancellation and cleanup evidence](PB-0208_PROCESS_CANCELLATION_EVIDENCE.md)
- [PB-0209 JSON Lines progress reader evidence](PB-0209_JSONL_PROGRESS_READER_EVIDENCE.md)
- [PB-0210 SQLite schema and migration evidence](PB-0210_SQLITE_SCHEMA_MIGRATIONS_EVIDENCE.md)
- [PB-0211 SQLite repository evidence](PB-0211_SQLITE_REPOSITORIES_EVIDENCE.md)
- [PB-0212 structured logging and correlation evidence](PB-0212_STRUCTURED_LOGGING_EVIDENCE.md)
- [PB-0213 persisted job orchestrator evidence](PB-0213_JOB_ORCHESTRATOR_EVIDENCE.md)

## Tool Discovery Evidence

- [PB-0301 tool-version and installation model evidence](PB-0301_TOOL_VERSION_MODELS_EVIDENCE.md)
- [PB-0302 Blender installation discovery evidence](PB-0302_BLENDER_INSTALLATION_DISCOVERY_EVIDENCE.md)
- [PB-0303 Unity installation discovery evidence](PB-0303_UNITY_INSTALLATION_DISCOVERY_EVIDENCE.md)

## Desktop Application Evidence

- [PB-1301 WPF shell, composition, navigation, and visual evidence](PB-1301_WPF_SHELL_EVIDENCE.md)

## Blender Worker Evidence

- [PB-0401 Blender worker shell evidence](PB-0401_BLENDER_WORKER_SHELL_EVIDENCE.md)
- [PB-0402 Blender scene utilities evidence](PB-0402_BLENDER_SCENE_UTILITIES_EVIDENCE.md)
- [PB-0403 Blender FBX import adapter evidence](PB-0403_BLENDER_FBX_IMPORT_EVIDENCE.md)
- [PB-0404 Blender GLB import adapter evidence](PB-0404_BLENDER_GLB_IMPORT_EVIDENCE.md)
- [PB-0405 geometry and transform inspection evidence](PB-0405_GEOMETRY_TRANSFORM_INSPECTION_EVIDENCE.md)
- [PB-0406 texture extraction and role inspection evidence](PB-0406_TEXTURE_ROLE_INSPECTION_EVIDENCE.md)
- [PB-0407 armature, skin, and weight inspection evidence](PB-0407_RIG_WEIGHT_INSPECTION_EVIDENCE.md)
- [PB-0408 action and animation inspection evidence](PB-0408_ANIMATION_INSPECTION_EVIDENCE.md)
- [PB-0409 automatic product-case inference evidence](PB-0409_PRODUCT_CASE_INFERENCE_EVIDENCE.md)
- [PB-0410 Blender naming normalization evidence](PB-0410_NAMING_NORMALIZATION_EVIDENCE.md)
- [PB-0411 unit, axis, pivot, and transform normalization evidence](PB-0411_TRANSFORM_NORMALIZATION_EVIDENCE.md)
- [PB-0412 helper cleanup and selection-safe export-set evidence](PB-0412_SCENE_CLEANUP_EVIDENCE.md)
- [PB-0413 material and image normalization evidence](PB-0413_MATERIAL_IMAGE_NORMALIZATION_EVIDENCE.md)
- [PB-0414 rig, Action, and baking normalization evidence](PB-0414_RIG_ANIMATION_NORMALIZATION_EVIDENCE.md)
- [PB-0415 normalized FBX export evidence](PB-0415_NORMALIZED_FBX_EXPORT_EVIDENCE.md)
- [PB-0416 normalized GLB export evidence](PB-0416_NORMALIZED_GLB_EXPORT_EVIDENCE.md)
- [PB-0417 Blender clean-reimport validation evidence](PB-0417_CLEAN_REIMPORT_EVIDENCE.md)
- [PB-0418 Blender failure and regression fixture evidence](PB-0418_BLENDER_REGRESSION_FIXTURES_EVIDENCE.md)

## Portable Target Evidence

- [PB-0501 portable naming evidence](PB-0501_PORTABLE_NAMING_EVIDENCE.md)
- [PB-0502 portable folder composer evidence](PB-0502_PORTABLE_FOLDER_COMPOSER_EVIDENCE.md)
- [PB-0503 portable texture copy and conversion evidence](PB-0503_PORTABLE_TEXTURES_EVIDENCE.md)
- [PB-0504 portable README generator evidence](PB-0504_PORTABLE_README_EVIDENCE.md)
- [PB-0505 deterministic FBX ZIP evidence](PB-0505_DETERMINISTIC_FBX_ZIP_EVIDENCE.md)
- [PB-0506 portable target validator evidence](PB-0506_PORTABLE_TARGET_VALIDATOR_EVIDENCE.md)

## Dependency and Licence Records

- [Third-party dependency notices](THIRD_PARTY_NOTICES.md)

Generated validation and build evidence belongs beneath ignored `artifacts` or `logs` directories. Tracked evidence must remain safe for the approved public repository.

## Documentation Validation

Run the dependency-free ADR validator with Windows PowerShell 5.1 or PowerShell 7:

```powershell
& .\scripts\Test-ArchitectureDecisionRecords.ps1
```

The repository baseline invokes the same validator in-process and through standalone Windows PowerShell 5.1.

Run the dependency-free permanent quality-baseline validator with either supported PowerShell:

```powershell
& .\scripts\Test-QualityAndReleaseGates.ps1
```

The repository baseline also invokes the quality validator in-process and through standalone Windows PowerShell 5.1.
