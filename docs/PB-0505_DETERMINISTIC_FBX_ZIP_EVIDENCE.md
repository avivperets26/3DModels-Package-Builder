# PB-0505 Deterministic FBX ZIP Evidence

## Status

- Canonical and publication branch: `feat/PB-0505-deterministic-fbx-zip`
- Lifecycle: 🟢 **DONE**
- Date: 2026-08-06

PB-0505 shares the explicitly user-approved publication cycle with PB-0506. Each task retains an
independent acceptance boundary, canonical branch, production API, evidence, lifecycle state, and
eventual Completion Log row. The exception creates no precedent.

## Implemented Boundary

`PortableFbxArchiveBuilder.CreateAsync(...)` accepts the exact manifest-derived flat-FBX layout,
one caller-owned readable stream per planned artifact, and one empty caller-owned read/write/seek
destination. It rejects missing, extra, duplicate, null, unreadable, length-mismatched, hash-
mismatched, cancelled, and I/O-failing inputs without publishing a partial archive.

Entries are emitted in the layout's ordinal order beneath the canonical flat-FBX folder. Every
entry uses `/`, `CompressionLevel.NoCompression`, zero external attributes, UTF-8 entry names, and
the ZIP-compatible UTC timestamp `1980-01-01T00:00:00Z`. Source length and SHA-256 are verified
while streaming through a bounded 64 KiB buffer. The receipt records ordered entry identities,
the exact archive identity, and a compression-independent logical identity.

No source stream is disposed or modified by the builder. The output is rewound after success and
cleared after a structured failure. No new package, network call, external archiver, or paid tool
was introduced.

## Acceptance Mapping

| Acceptance criterion | Automated evidence |
|---|---|
| Archive order is controlled | reversed source input produces the exact layout order |
| Archive timestamps are controlled | every ZIP entry and receipt uses the fixed UTC timestamp |
| Contents match the manifest | exact artifact IDs, byte lengths, SHA-256 values, and entry names are verified |
| Repeated builds are logically identical | repeated archives are byte-identical and have equal exact/logical digests |
| Failure is atomic and safe | missing/extra/duplicate/altered sources, cancellation, and I/O faults clear output |

## Local Validation

- Focused portable-target suite: 187 passed, 0 failed, 0 skipped.
- All 18 instrumented PB-0505/PB-0506 production classes: 100% line and branch coverage in ignored
  `artifacts/PB-0505-PB-0506/coverage-final-2`.
- Full Core CI: all nine stages passed in 3 minutes 46.8 seconds.
- Complete solution tests: 2,254 passed, 0 failed, 0 skipped across six test projects.
- Debug and Release solution builds: 17 projects, zero warnings and zero errors.
- Repository baseline: 29 passed, 0 failed.
- Locked restore, .NET/Ruff formatting, PowerShell parsing, task graph, Markdown links,
  secret/prohibited-content checks, history integrity, and `git diff --check`: passed.
- NuGet audit: no vulnerable direct or transitive package reported across all 17 projects.

## Publication and Completion

- Combined task commit: `38d82cc00572f506c3dc2cf67f996d64e50e64dd`.
- Integration: [pull request #65](https://github.com/avivperets26/3DModels-Package-Builder/pull/65).
- `main` merge: `725c5d21fa5d28342d62946b4ac93184a33656f9`.
- Required [main workflow run 31123410839](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/31123410839): successful for the exact merge.
- User confirmation and rollover date: 2026-08-06.
- Exception boundary: the approved combined publication branch affected topology only; no CI,
  quality, or completion exception was used, and no precedent was created.
