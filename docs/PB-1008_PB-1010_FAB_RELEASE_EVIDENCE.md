# PB-1008–PB-1010 — Listing and portable/Unity release flow

## Completion rollover — 2026-09-16

PB-1008–PB-1010 are DONE. Task commit `bcde9230e05ec94e469572561db6523b73b84216`
merged into main `bcd696b1d9aa61ce97228849fbbd120cea34e844`; both jobs in
[main CI 34955099753](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/34955099753)
passed. The user accepted publication and approved the Unreal foundation scope. The backlog
recorded each completion once at the start of that next branch. The following handoff describes
historical implementation/publication states; its cleanup exceptions remain in the cleanup audit.

## Historical scope and lifecycle

User-approved branch: `codex/PB-1008-PB-1010-fab-release`, based on freshly fetched/pulled clean main
`491efe6175d2e0858146ba9701bfea70e297ab60` on 2026-09-14. Previous PB-1004/PB-1005/PB-1007 tasks
roll to DONE once with successful main CI and user confirmation. Each new task keeps its own status.
PB-1009 supports portable/Unity here; Unreal requests fail explicitly until PB-1006 and E11 exist.
On 2026-09-15, after reviewing local validation, the user authorized staging, committing, pushing
the task branch, merging and pushing main, and checking main CI. Record final commit/CI evidence
in the next task's normal completion rollover after successful CI and user confirmation.

| Task | Current implementation | Remaining acceptance |
| --- | --- | --- |
| PB-1008 | Listing JSON, explicit AI declarations, categories/platforms, derived formats/engine versions, dependency links and unchecked upload checklist; locally validated | Publication gates |
| PB-1009 | Approved exact-profile loading, selected validators, content-bound sources, deterministic versioned ZIP and manifest; locally validated | Publication gates |
| PB-1010 | Real static portable/Unity/media/docs release passed on 2026-09-15; 2,771 automated tests passed | Recorded cleanup exceptions and publication gates |

## Sources and reviewed continuation

On 2026-09-15 the user supplied the Fab forum discussion and approved continuing. The new
`2026-09-15.1` candidate records the corroborated 6 GB interpretation as explicit local policy,
and scopes the completed technical review to static FBX/Unity listings. The previous revisions
remain immutable. See the [review map](PB-1010_REQUIREMENTS_REVIEW.md). The earlier blocker below
describes the 2026-09-14 handoff; it is no longer the current reason to stop work.

### Historical 2026-09-14 review and blocker

Reviewed [Fab publishing guidance](https://dev.epicgames.com/documentation/en-us/fab/publishing-assets-for-sale-or-free-download-in-fab)
and [format requirements](https://dev.epicgames.com/documentation/en-us/fab/asset-file-format-and-structure-requirements-in-fab)
on 2026-09-14. The listing checklist covers product details, files, gallery/thumbnail, preview,
review submission and an explicit activation choice. The suggested short title is guidance, not a
false hard marketplace limit. Application text bounds and public-link checks are local policies.

The format page still prints an ambiguous `6B` exchange-format limit. The linked
[full technical requirements](https://www.fab.com/o/technical-requirements), dated March 17, 2026,
were subsequently readable through browser accordions after text extraction omitted their contents
and direct retrieval hit a JavaScript/cookie challenge. Sections 1, 2, 3 and 5 were inspected:
listing information needs accurate English, relevant categories/formats, dependency disclosures,
AI and maturity declarations; portable archives must use ZIP and match their selected format;
additional files must be relevant. Unity clause 5.2.a requires dependencies in the description and
links for external dependencies; include these when completing the manual checklist. Clause 5.1.a
also incorporates Unity Asset Store submission guidelines, which still need a complete mapped review.
These expanded clauses did not clarify the exchange-format byte limit.
The existing `other-format-bytes` and `technical-review` unresolved rules remain intact. No new
official bound is invented, no review is marked complete, and no production profile is auto-approved.
PB-1010 cannot be called Fab-ready or complete on this evidence. Obtain authoritative clarification
and complete the full review, then import/test/approve a profile through the existing updater and run
the real static release fixture. Synthetic fixture profiles are labelled test-only and stay in tests.

## Contracts and reuse audit

`FabListingChecklistGenerator` writes UTF-8 JSON with encoded text and a fixed plain-text checklist.
Publisher configuration is typed; the generative-AI choice is explicit and never inferred from generic
AI assistance. Formats and Unity versions derive from listing/build.lock. Categories/platforms are
user choices for manual review, not a hard-coded claim to exhaust the current Fab taxonomy.
Prices, licence, rights and final publisher submission remain manual; every checkbox starts unchecked.
User-supplied public listing text is not a substitute for human privacy/accuracy review.
URL checks are syntactic; the manual checklist requires verifying actual public accessibility.

`FabReleaseComposer` loads only the approved immutable pin, invokes existing portable/Unity/gallery
validators, checks documentation and media-quality observations, matches Unity dependency disclosures,
rejects unrequested or missing deliveries and preserves upstream findings. Observations and stream
openers are trusted in-process orchestration inputs, never supplier-authored JSON. Files must remain
contained and frozen while validating; streamed payloads are rehashed against their receipts.
The generated manifest lists exact entry names, lengths and SHA-256 values; its own hash is covered
by the final archive receipt. The profile JSON, build.lock, listing and checklist are included.

`VerifiedReleaseArchiveWriter` writes only explicit entry streams with fixed ZIP timestamps/attributes
and sorted paths. It owns/disposes opener streams, leaves staging open, and resets partial staging on
failure. If reset fails, the caller must discard staging. Existing `ArtifactStore` and
`AtomicArtifactPromotionService` handle persisted lifecycle and final publication; the integration
test exercises that path. No parallel promotion implementation or new dependency is introduced.
`ArtifactStreamTransfer` shares the portable writer's bounded streaming/hash primitive, and
`DeliveryPath` shares conservative naming across validators and the new writer. ZIP envelope framing
stays separate from the portable FBX layout because their inventory and receipt contracts differ.

Limits: no Unreal, 3D/video-only gallery or desktop UI; this is an API milestone. Gallery inputs are
capped at 32 MB before copying; each ZIP stream is bounded by its declared receipt. No throughput or
memory benchmark claim is made. The outer envelope is for local review/handoff, not a Fab extra-file upload.

## Historical 2026-09-14 validation and cleanup

The complete seven-project Release suite passed **2,766/2,766 tests**, zero failed/skipped, including
**32 new focused cases**. The solution build passed with zero warnings/errors; locked restore,
Ruff lint and Ruff formatting (51 files) passed. Source-stability verification passed throughout the
full suite. The integration fixtures use real ZIP/WIC encoding but synthetic model,
Unity inspection and media-quality observations. They are not a new real Editor export/import.
Disposable workspaces, including promoted synthetic archives, stay in per-test GUID directories under
`tests/PackageBuilder.App.Wpf.Tests/bin/Release/net10.0-windows/PB-1010` and are removed in disposal,
including constructor failures. Existing historical PB-1001 empty-directory exceptions remain unchanged.
Compact logs and receipts stay beneath ignored `artifacts/PB-1008` and `artifacts/test-results/PB-1008`.
The post-suite cleanup receipt confirms **zero remaining PB-1010 run directories and zero files**.
The historical PB-1001 exception still consists of 15 empty directories with zero files; no rejected
cleanup was retried. The generated archive, staging and artifact-store copies were all disposed.
The repository scanner initially flagged a deliberately fake user-information URL in a negative test.
That fixture now constructs its fake URL at runtime; scanner policy is unchanged. All 32 focused
cases passed again after this fixture-only correction. Production behavior was unchanged after the
complete suite. Final .NET formatting verification passed before the fixture correction, with an
additional verification of the changed fixture performed afterwards.
The final repository baseline passed **35/35 checks**, including the unchanged secret scanner and
nine cleanup cases. Its disposable synthetic outputs in `artifacts/u/161b4221` were removed;
logs/reports were preserved. Evidence: `artifacts/PB-1008/repository-baseline-final.log`.

## 2026-09-15 live release acceptance

The full existing Unity product integration passed, including static, rig, animation, item/set,
collection and twelve-item checks. The Fab harness then prepared distinct texture maps and offline
instructions, exported that fixture, and imported it into a new project (`s`). The clean import passed
with no findings. Inspection reused package validation, direct asset dependencies plus referenced
script compilation units, and five real GPU captures. The hero image was visually inspected: the
textured cube is framed and visible without missing-material pink or editor helpers.

Initial live attempts correctly failed: the test bridge supplied an archive root as a product key,
the old regression fixture reused identical texture bytes, and its initial usage scan missed transitively
referenced files. These were corrected in the fixture/bridge; production validators were not waived.
The prepared package was exported and clean-imported again before the successful release check.

Blender 5.0.0 independently imported the delivered FBX. Its SHA-256 matched both the portable ZIP entry
and the clean Unity source FBX. Unity 6000.3.10f1 supplied measured geometry, 18 package assets and
five gallery views. The shared media optimizer and generated README fed the actual composer with a
test-local approved profile pin. The final 13-entry archive passed every manifest-entry hash check,
with zero findings. This validates the static fixture, not a commercial product's subjective quality
or publisher approval. All manual checklist boxes remain unchecked.

- Candidate SHA-256: `b290d498d3b9752928c8b06e009e1a7451c7f4a4c2083a844069f6b40145767e`.
- Release receipt: 307,049 bytes; SHA-256 `512e0f575c381641fa738fb993ff1a3b118dd1e9409ef46eab28e08a0faaf7a2`.
- Live receipt: `artifacts/PB-1010/fab-release-result.json`; `realEngineRun=true`, `passed=true`.
- Detailed logs, observations, fresh-import result and TRX: `artifacts/u/4d77ed1c`.
- The composed envelope was disposed from memory. Engine projects and generated packages are
  subject to the [cleanup audit](TEST_ARTIFACT_CLEANUP.md); three portable folders have rejected deletions.

Reproduction (creates and cleans disposable engine projects by default):

```powershell
& ./scripts/Invoke-FabStaticReleaseIntegration.ps1
```

Normal CI runs five additional candidate approval/scope cases with synthetic inputs; it does not
claim the separate live acceptance receipt. The full seven-project Release suite passed
**2,771/2,771**, zero failed/skipped, with source-stability verification. Locked restore and the
solution build passed with zero warnings/errors. The initial formatting command incorrectly included
vendored sources; the documented command with `--exclude third_party` subsequently passed at
information severity, preserving vendor bytes.
The unit fixtures left zero run directories and zero files. Standard Unity cleanup removed
**12,814,964,824 bytes**, including the fresh import, exported packages and inspection captures;
its receipt is `artifacts/u/4d77ed1c/cleanup-result.json`. Paths into those removed projects are now
historical. The three portable exceptions remain as documented; no rejected removal was retried.

Ruff lint and formatting passed (52 files). The full repository baseline initially passed **33/35**
top-level checks: its two Unity inventory gates rejected the newly added test-only inspection worker.
Both explicit inventories now include that worker; targeted reruns passed **8/8 template checks**
and **10/10 worker checks**. The other 33 baseline checks passed, including secret/prohibited-content
scanning, dependency/branch/backlog/link validation, nine cleanup cases and Git integrity. This is
an initial full run plus two successful corrected-gate reruns, not a second full baseline run.
Evidence: `artifacts/PB-1010/repository-baseline-final.log`, `unity-template-final.log`,
`unity-worker-final.log`, `format-project-final.log` and `unit-tests-final.log`.
Only documentation/status and the two inventory allowlists changed after the full unit-test run.
The baseline's disposable cleanup fixture (`artifacts/u/1c86931d`) was also removed.

## Handoff

Changed files cover listing/composition APIs, shared artifact contracts and stream/path helpers,
the Infrastructure ZIP writer, portable helper reuse, focused fixtures/tests, Unity inventory checks,
the real-release PowerShell/Blender/Unity bridge, branch-policy checks,
AGENTS/engineering guidance and the backlog/plan/architecture/quality/cleanup evidence.
Suggested commit: `feat: compose reviewed Fab portable and Unity releases`.
All three tasks remain IN PROGRESS through their current acceptance/publication gates.
Suggested manual commands after reviewing exact files and authorizing publication:

```powershell
Set-Location C:\Dev\PackageBuilder
git status --short
git diff --check
# Stage only reviewed files; all three tasks remain IN PROGRESS until publication gates pass.
git commit -m "feat: compose reviewed Fab portable and Unity releases"
git push -u origin codex/PB-1008-PB-1010-fab-release
```
