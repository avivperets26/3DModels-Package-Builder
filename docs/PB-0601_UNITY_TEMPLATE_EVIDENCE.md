# PB-0601 Versioned Unity Project Template Evidence

## Lifecycle

- Task: PB-0601 — Create versioned Unity project template.
- Branch: `feat/PB-0601-unity-template`.
- Status: `[x]` / 🟢 **DONE**.
- Started: 2026-08-06.
- Dependencies: PB-0303 and PB-0308 are complete.

PB-0601 is published, validated by later inclusive required `main` CI, explicitly confirmed, and
recorded complete during the PB-0607/PB-0608 rollover.

## Publication Checkpoint

- Task commit: `cce15e3b1cda012f15245c519aa354c1b85b85b8`.
- Integration: [PR #67](https://github.com/avivperets26/3DModels-Package-Builder/pull/67).
- Merge commit: `6915744820ae53fe9285b493c1d1ced3ede6e740` on 2026-08-06.
- GitHub created no workflow for the exact outage-time merge. Required later [main workflow run
  31180117662](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/31180117662)
  succeeded for descendant `e575365df6ee9b93648e65bea02394596ace52e6`, whose history and
  tree contain the unchanged PB-0601 template.
- The user explicitly confirmed the green merged state and requested rollover on 2026-08-07.
- No CI gate is waived; later inclusive post-merge CI supplies the missing outage-time execution
  evidence.

## Approved Version Pair

The template is versioned at `engine-templates/unity/6000.3` and pins:

- Unity Editor `6000.3.10f1`, revision `e35f0c77bd8e`.
- Universal Render Pipeline `17.3.0`.

The source settings were mechanically derived from the official 3D URP template bundled with the
installed Unity `6000.3.10f1` Editor. The bundled template archive identity observed during the
implementation audit was:

```text
com.unity.template.3d-cross-platform-17.0.14.tgz
SHA-256 B78FB4CD033D264B842F7F8B10C3E613F0DFC9A825C03CD7E91919521D8AE27E
```

The Editor-bundled `com.unity.render-pipelines.universal` package declared version `17.3.0`; its
audited `package.json` identity was:

```text
SHA-256 040CDFEC7F05101EEAEC0F097448CEA49681A874FB89FAE5A9F61F3D6408DC4B
```

The tracked package manifest deliberately retains only URP as a direct dependency. Optional IDE,
Collab, navigation, Input System, Timeline, visual-scripting, and tutorial packages from the source
template are excluded from this minimal worker foundation.

## Minimal Project Boundary

The template has exactly three top-level directories:

```text
Assets/
Packages/
ProjectSettings/
```

`Assets/Settings` contains only the official URP mobile/PC renderer assets, render-pipeline assets,
default volume profile, global settings, and their metadata. `Packages/manifest.json` pins URP.
`ProjectSettings` contains the official project settings plus the exact Editor revision.

Removed source-template content includes the sample scene, Readme/tutorial scripts and images,
layout, Input System action asset, and sample volume profile. Build settings contain no scene or
input-action reference. Generic Package Builder placeholder identity replaces Unity template
branding; no publisher identity is hard-coded.

The template contains no `Library`, `Temp`, `Logs`, `UserSettings`, `obj`, build output, Gradle
state, executable, script, scene, prefab, product model, or media file. Unity is never launched
against this tracked source template; later job execution must clone it into isolated staging first.

## Validation Contract

`scripts/Test-UnityProjectTemplate.ps1` is dependency-free and supports Windows PowerShell 5.1 and
PowerShell 7. It verifies:

1. Exact top-level roots.
2. Exact deterministic file inventory.
3. Exact Editor/revision and URP pins.
4. Complete URP project references.
5. Matching Unity asset GUID metadata.
6. Absence of samples, scripts, scenes, and stale identities.
7. Absence of Unity caches and generated output.
8. Public-safe UTF-8 text with LF endings.

The repository baseline invokes the same validator without requiring Unity, installing an engine,
contacting a registry, uploading artifacts, or writing outside the repository.

## Local Validation

- Standalone Unity template validator: 8 passed, 0 failed.
- Repository baseline: 30 passed, 0 failed, including the integrated Unity template validator.
- Full Core CI: all nine stages passed in 5 minutes 28.0 seconds.
- Release build: 17 projects, 0 warnings, 0 errors.
- Complete test suite: 2,268 passed, 0 failed, 0 skipped across six test projects.
- Locked restore, .NET formatting, Ruff lint/format, PowerShell parsing, Markdown links, task graph,
  public-safety scans, history integrity, and `git diff --check`: passed.
- Unity Editor open/import test: intentionally not run. The user has not authorized launching the
  externally installed Editor, and PB-0601 does not require activation or mutable external state.
- Manual visual test: not yet available. PB-0601 is a clean engine foundation with no scene or
  product; the first meaningful Unity visual checkpoint arrives after the overview scene and static
  product slice are implemented.

## Remaining Gates

None for PB-0601.
