# Test artifact cleanup

The user's 2026-09-10 instruction applies to all past, current and future PB tasks,
including completed-task reruns. AGENTS.md defines the permanent cleanup rule and the
engineering skill includes it in task startup and handoff. This audit tracks physical
artifact retention separately from task completion and test outcomes.

## Operating rule

- Remove owned disposable packages, archive extractions and temporary engine projects after
  success or failure. Retain only compact diagnostic evidence by default.
- Preserve source/golden fixtures, user assets, intended promoted releases, tools and shared
  caches. Check ownership and path containment; reject reparse points and active test processes.
- Record any manual retention with its exact path, purpose and cleanup follow-up. A completed
  task or an old statement that outputs were retained is not a permanent retention exception.
- At task start, review outstanding exceptions below. At handoff, verify cleanup and update this
  audit. Cleanup receipts describe later filesystem maintenance, not new passing test results.
- For Unity `artifacts/u/<8-hex-id>` runs, reuse `Remove-UnityTestArtifacts` from
  `scripts/UnityTestArtifacts.Common.ps1`. Do not broaden it into a repository-wide ZIP deletion.

## Historical evidence interpretation

Older evidence documents and copied result pointers describe files present when those tests
ran. After a run has a `cleanup-result.json` receipt, package/project paths in those historical
records are provenance only. Retained logs and structured reports remain the validation evidence;
regenerate disposable outputs for a new manual inspection and clean that rerun afterwards.
Completed tasks keep their DONE status and original Completion Log entries.

## Retrospective audit — 2026-09-10

The four current-scope runs were cleaned first, preserving reports and removing over 11 GB.
The follow-up cleaned all 63 older Unity run directories, removing 152,116,117,368 bytes.
All 67 Unity run directories (including the prior four) were verified to contain no disposable
project directories or generated package/model archives. Their root logs, inventories, diagnostic
reports and cleanup receipts remain. Copied evidence pointers were marked `artifactsRetained=false`;
the old portable `latest.txt` was annotated as historical as well.

Completed legacy cleanup: 8 Unity worker/product projects, 4 Blender runs, 5 portable manual
test workspaces and 2 generated FBX fixture copies (19 locations / 41 payloads). These removed
12,316,928,538 bytes. Nested worker result JSON/probe evidence, Blender expectations and portable
JSON metadata were copied out before deletion; logs, observations and reports were preserved.
Each location has a `cleanup-legacy-result.json` receipt. The one-time reviewed deletion manifest,
operation script and JSONL receipts are under
`artifacts/PB-0808-PB-0810/historical-cleanup/`.

This follow-up removed an additional **164,433,045,906 bytes (164.4 GB)** across 82 historical
test locations. Counts describe test runs/output locations, not PB tasks. Byte totals sum removed
file lengths from cleanup receipts; they are not a measurement of allocated filesystem space.
The consolidated result is `artifacts/PB-0808-PB-0810/historical-cleanup/summary.json`, with
`selected-runs.json`, `results.json`, `legacy-cleanup-plan.json` and
`legacy-cleanup-results.jsonl` preserving the exact scope and outcomes.

## Remaining exceptions

- The intended PB-0618 reference release under
  `artifacts/PB-0618/releases/run-20260808-143128-93cc3bc1/StoneArch` is preserved by the release
  exclusion: its two archives total 71,822 bytes. It is a retained release snapshot, not a
  temporary test workspace. Revisit retention only when that reference release is retired.
- `artifacts/PB-0601/official-template` is the downloaded template baseline, not a generated test
  clone. Source fixtures, shared caches, tools, coverage/build evidence and reports remain.
- No unresolved disposable-output cleanup remains in the audited locations. A final artifact scan
  outside the retained template and cleaned Unity runs found only the two reference-release
  archives above. No permanent test-output exemption is inferred from DONE status.

## Validation and publication

The 8 cleanup checks pass, including historical preparation-report preservation and link updates,
and the engineering skill validator passes using existing repository-local dependencies.
The full repository baseline passes all 35 checks for this follow-up. No .NET or engine product
implementation changed; previous product validation results are not relabeled as new test runs.

Changes remain on `feat/PB-0808-PB-0810-selector-portable-e2e`. Task counts remain 120 DONE,
3 IN PROGRESS, 0 BLOCKED and 141 BACKLOG (264 total, 144 remaining, 45.5%). Git publication is
pending; the permanent rules will reach fresh main-based task branches when this branch is merged.
The existing [scope handoff](PB-0808_PB-0810_SELECTOR_PORTABLE_EVIDENCE.md) contains the full changed
file list, suggested commit and manual publication commands. This follow-up reuses the cleanup
helper; the legacy operation is a one-time, explicitly reviewed artifact manifest rather than a
broader production deletion API.

## PB-0811 validation cleanup — 2026-09-10

Run `artifacts/u/76b6d8fc` passed the twelve-item portable/Unity collection and the full
product integration harness. Its `cleanup-result.json` records 11,184,592,470 bytes
of disposable output removed (11.18 GB summed file lengths). No directories or generated
packages remain in the run; compact reports, inventories, logs and measurements remain.
The source/golden fixture is retained under `tests/fixtures/portable/twelve-item-collection`.

The selector UI's test-launched Hub tree was identified by its exited Editor parent and
exact project path, then closed before deletion. The product harness now automates that
specific cleanup through `Stop-CompletedUnityTestHub`. Nine tests cover lifecycle cleanup,
evidence preservation and protection of unrelated/active processes and reparse points.

The earlier cleanup scope was published as task commit
`4beaa9507205e1937895152415c42ccc6eeecc06`, main merge
`55c4a0213ec20b347bf319804448d9e14a27ad48`, with successful main CI. Its publication-pending
notes above are historical. PB-0808–PB-0810 completion was recorded at this branch's start;
PB-0811 remains IN PROGRESS pending publication. See its
[evidence and handoff](PB-0811_TWELVE_ITEM_COLLECTION_EVIDENCE.md).
## PB-0901–PB-0903 documentation validation — 2026-09-10

PB-0811 was merged through PR #92 with successful main CI and its completion was rolled over.
The new documentation scope uses in-memory template/archive tests and disposed small configuration
workspaces. No disposable product packages or engine projects were retained. All 9 cleanup checks
passed as part of the 35-check repository baseline. Earlier publication-pending statements are
historical; source fixtures, intended releases and compact evidence remain preserved.

## PB-0907 still-image validation cleanup — 2026-09-11

Run `artifacts/u/83092df2` passed real five-view capture, repeated PNG hashes, final-material
pixel checks, helper exclusion, collision/traversal rejection and failure-state restoration.
The harness deleted its cloned Unity project and all full-size generated images in `finally`:
1,606,341,896 bytes removed. Only the log, receipt JSON, cleanup JSON and a 2400×270 contact sheet
remain. The contact sheet was visually inspected. No test package was created; the original FBX
fixture and tracked engine template were preserved. The new harness reuses the permanent cleanup
helper, including test-owned Hub cleanup, for both success and failure.

## PB-0908–PB-0910 media validation cleanup — 2026-09-11

The initial coverage capture run artifacts/u/a5e9fed2 removed its 1,607,850,000-byte cloned
project in finally. The connected capture → image checks → optimization → JSON report run
artifacts/u/23fd84f7 passed all five views and removed its 1,607,060,407-byte clone, including
all original/coverage PNGs and engine caches. Cleanup receipts report cleaned; both project
paths were verified absent. Only logs, receipts, a contact sheet, the connected report and TRX
remain. The contact sheet was visually inspected. Optimized images exist only in test memory.
No package archives or new persistent source/golden assets were created; the original fixture
and engine template remain preserved. The harness now optionally runs the full media pipeline
before its existing finally cleanup, including when that pipeline fails.

## PB-0911/PB-0912 diagnostic export cleanup — 2026-09-11

The support archive tests build and read ZIPs entirely in memory, including failure and
cancellation paths. HTML previews also use synthetic in-memory pixels. No disposable package,
extraction directory or engine project is created. Only an explicitly requested compact synthetic
HTML sample and test logs/TRX are retained in ignored artifacts/PB-0911. Source/golden fixtures
and prior intended releases remain untouched. The existing cleanup baseline continues to apply.
