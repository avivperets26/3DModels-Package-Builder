# PB-1010 static release requirements review — 2026-09-15

Candidate: `profiles/marketplaces/requirements/fab-2026-09-15.1.json`.
Historical revisions remain immutable. The user approved continuing the review and real release
validation on the existing combined branch. This does not authorize Git publication or a live Fab listing.

## Size interpretation

The [June 10, 2025 forum reply](https://forums.unrealengine.com/t/request-for-uploading-large-file-package-on-fab-store/2539119)
states a 6 GB upload ceiling. It corroborates, but does not edit, the ambiguous `6B` in the
[format summary](https://dev.epicgames.com/documentation/en-us/fab/asset-file-format-and-structure-requirements-in-fab).
The candidate explicitly labels the decimal 6,000,000,000-byte exchange limit as Package Builder
policy based on that interpretation. The source allowlist admits this exact discussion only.
Additional files retain 6 GB each / three files, and 3D previews retain the strict 500 MB bound.
No 2 GB legacy rule or RAR/7z permission was imported.

## Applicability and coverage

The [technical requirements](https://www.fab.com/o/technical-requirements), revision March 17, 2026,
were read using the expanded General, DCC/Exchange, Additional and Unity sections. The candidate is
restricted in the shared target resolver to static 3D-model FBX/Unity selections; other cases need
a further reviewed profile. Unreal remains unsupported by the composer.

| Source clauses | Enforcement or review boundary |
| --- | --- |
| Fab 1.1–1.7 | Publisher support, identity and commercial decisions remain human checklist steps. |
| Fab 1.8 | Listing data, dependencies and AI choices are generated; language, accuracy, tags, maturity and promotions require human review. |
| Fab 2.1–2.3 | Portable format/archive validation; actual imported geometry and representative views; human visual/scale review. |
| Fab 3.1 | Planned additional-file relevance; maturity remains human review. |
| Fab 5.1–5.2 | Delegated Unity review below; dependency versions and description/link checklist. |

The [Unity submission guidelines](https://assetstore.unity.com/publishing/submission-guidelines),
revision May 20, 2026, were reviewed for this static package. Mapping:

| Clauses | Coverage |
| --- | --- |
| 1.1, 1.3 | Existing package validator, clean import, pinned Editor/pipeline, size bounds, dependencies and overview scene. |
| 1.2, 1.4–1.6 | Rights, notices, restrictions, safety and AI declarations remain explicit human checks. No service/UPM submission. |
| 2.1–2.3 | Exact inventory, safe paths, duplicate/unused detection, archive policy and generated offline documentation. |
| 2.4 | Measured static geometry, prefab/material/texture inspection and captured views; artistic suitability remains human review. Rig, animation, sprite and audio clauses are outside this candidate. |
| 2.5 | Existing preview scripts, compilation and Play-mode checks; no exported editor worker. Later Editor-version conditions are not claimed tested. |
| 2.6–2.8, 3–5 | Template/audio/UPM-specific requirements are inapplicable; platform, listing accuracy and publisher support remain human checks. |

`technical-review=verified` means the applicable rules have been reviewed and mapped. It does not
mean an arbitrary product meets them, that subjective review is automated, or that Fab has approved
a listing. Every manual-upload checkbox stays unchecked.

## Validation boundary

`FabStaticReleaseCandidateTests` imports the new revision into a disposable SQLite database,
runs compatibility cases, rejects unapproved use and then explicitly approves the test-local pin.
Normal CI covers these contracts using synthetic artifacts. The live harness supplies a separate
pointer and requires a distinct `realEngineRun` receipt before claiming PB-1010 acceptance.

`Invoke-FabStaticReleaseIntegration.ps1` reuses the existing portable build, Unity product export,
clean import, package validator, GPU capture, shared media optimizer, README generator, profile
updater and release composer. Blender independently reimports the extracted delivered FBX.
The bridge checks matching source hashes between Blender, the archive and clean Unity import.
It verifies every final manifest entry hash. Test outputs are disposed; compact evidence is retained.
The profile is not installed as the user's current production profile.

Final results and any discovered gaps are recorded in
[combined evidence](PB-1008_PB-1010_FAB_RELEASE_EVIDENCE.md).
