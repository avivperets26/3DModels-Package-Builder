# STUDIO AVIV — Package Builder Hosted Converter Handoff

## How to use this handoff

Give this complete document to the ChatGPT project that is building the STUDIO AVIV Next.js website. It is the shared product and integration brief for an optional online 3D package converter.

This document is a design contract, not evidence that the hosted converter already exists. The website must keep the feature disabled or clearly labelled as a non-functional prototype until the Package Builder hosted API, storage, queue, workers, security controls, and end-to-end acceptance are implemented and approved.

## Project context

STUDIO AVIV is a Next.js and TypeScript storefront where Aviv can sell 3D models in addition to Fab.com. Package Builder is a separate Windows-oriented system currently being developed at:

- Repository: <https://github.com/avivperets26/3DModels-Package-Builder>
- Product name: Package Builder
- Local development root today: `C:\Dev\PackageBuilder`

The local path is developer configuration only. STUDIO AVIV must never depend on that path.

Package Builder is intended to take messy 3D source assets and produce deterministic, validated, sale-ready outputs. Its architecture uses .NET domain/application/contracts code plus external Blender, Unity, and Unreal workers. The engines are deliberately outside the desktop UI and core business logic.

## What Package Builder is designed to convert

The product recognizes five cases:

1. Static model: no rig and no animation.
2. Rigged model: a skeleton/skin but no animation.
3. Rigged and animated model.
4. Item set: related parts such as armor, helmet, gloves, and boots.
5. Item collection: multiple separate products such as a twelve-sword pack.

Depending on the manifest and available worker tiers, expected output can include:

- Normalized FBX and GLB files.
- Canonically named separate textures such as Albedo, Normal, Metallic, Roughness, Emission, Ambient Occlusion, Opacity, and Height.
- A deterministic FBX ZIP and product README.
- A Unity URP package with organized publisher/product folders, import settings, materials, meshes, prefabs, overview scenes, scripts, animation clips/controllers where applicable, documentation, and clean-reimport validation.
- An Unreal project/package with equivalent assets, materials, Blueprints, overview maps, animation data where applicable, documentation, and clean-reopen validation.
- Preview media and marketplace documentation.
- Machine-readable findings, logs, metrics, hashes, and validation reports.

The existing project is still under development. The website must derive enabled targets from the server's capability response; it must not assume every listed output is ready.

## Non-negotiable architecture

Do not run Blender, Unity, Unreal, or the complete Package Builder pipeline inside Next.js, a Server Action, an API route, the browser, or an ephemeral serverless filesystem.

Use this boundary:

```text
Browser
  -> STUDIO AVIV Next.js UI/BFF (small metadata and control requests)
  -> Package Builder hosted API (authorization and job management)
  -> durable queue
  -> isolated Blender / Unity / Unreal Windows workers
  -> object storage for quarantined inputs and validated outputs
  -> short-lived signed download returned to the authorized browser

Browser
  -> direct/resumable object-storage upload using short-lived scoped authorization
```

An optional later mode may use a user-operated local Package Builder agent. The website would create and monitor the job, while the authenticated agent runs the user's locally installed engines and uploads only validated results.

## Responsibility split

### STUDIO AVIV browser and Next.js application

- Present the converter and explain supported inputs, outputs, limits, retention, privacy, expected duration, and any price before submission.
- Authenticate the user through the site's approved identity system.
- Request short-lived upload authorization from the Package Builder API.
- Upload large files directly and resumably to approved object storage.
- Collect target selection and manifest/product metadata without duplicating Package Builder validation rules.
- Display server-provided product-case inference, review questions, progress, findings, artifacts, expiry, and deletion state.
- Allow cancel, safe retry, download, and delete when the API permits them.
- Never accept or display a successful result solely because an HTTP request succeeded; use the typed job state and blocking findings.
- Never expose storage credentials, worker commands, internal paths, engine activation data, or unrestricted signed URLs.

### Package Builder hosted API

- Authenticate identities and authorize tenant ownership on every job, upload, event, artifact, cancel, retry, and delete operation.
- Issue least-privilege, short-lived, object-specific upload and download grants.
- Validate versioned request and manifest contracts on the server.
- Create idempotent jobs and persist their state, findings, audit events, retention, and ownership.
- Publish work to a durable queue based on declared worker capability and approved engine version.
- Return typed progress and terminal results.
- Enforce quotas, rate limits, concurrency, expiry, deletion, and feature availability.

### Isolated workers

- Claim jobs using durable leases so only one writer can promote a result.
- Download immutable source snapshots into disposable contained workspaces.
- Run only pinned approved executables with literal arguments and no command construction from user input.
- Enforce CPU, memory, disk, file-count, extracted-size, duration, and cancellation limits.
- Deny implicit network access and prevent imported scripts or plugins from executing unless a separately approved workflow requires them.
- Emit versioned progress, structured findings, resource metrics, logs, hashes, and artifacts.
- Promote outputs only after target validation and clean reimport/reopen acceptance.
- Clean or retain failed workspaces according to an explicit redacted diagnostic policy.

## Recommended website route and user journey

Suggested route: `/tools/3d-package-converter`.

The page should provide this flow:

1. **Choose files** — drag/drop or browse for FBX, GLB, supported archives, and textures. Show accepted types and account-specific limits before selection.
2. **Upload safely** — use direct resumable upload with visible progress, pause/retry where supported, integrity verification, and a cancel/delete action.
3. **Inspect** — show that files are quarantined and inspected. Do not call them safe or accepted until the API says so.
4. **Review product** — display inferred static/rigged/animated state, assets, textures, materials, rigs, actions, naming proposals, and any ambiguity. Sets and collections require explicit manifest confirmation when inference is ambiguous.
5. **Choose outputs** — enable only server-advertised capabilities. Explain Blender/portable, Unity, and Unreal separately, including version and estimated resource/time or price.
6. **Confirm** — show a concise build summary, retention/deletion policy, terms/licence declaration, AI disclosure fields where required, and total price if the hosted service is paid.
7. **Build** — display the durable stage timeline, elapsed time, queue state, cancel availability, and actionable findings.
8. **Review results** — separate blocking errors, warnings, and informational findings. Never bury a failed target inside an overall green state.
9. **Download or delete** — list hashes, sizes, formats, engine versions, expiry, validation status, and short-lived downloads. Offer immediate deletion and confirmation.

The approved Package Builder job stages are:

```text
Queued
Preflight
Inspecting
AwaitingReview
Normalizing
BuildingTargets
RenderingPreviews
Validating
PackagingMarketplace
CleanReimport
Completed | Failed | Cancelled
```

The API contract will define wire tokens. Do not invent permanent TypeScript string values independently; generate or review them from the approved OpenAPI/schema source.

## UX and accessibility requirements

- Use a calm guided workflow with clear primary actions, visible current stage, preserved form input, and no surprise destructive action.
- Support responsive keyboard-only use, correct labels/roles, visible focus, screen readers, high contrast, reduced motion, and 200% text scaling.
- Explain technical findings in plain language with consequence and corrective action. Raw stack traces are diagnostic details, not the main error.
- Show upload/build limits and expected retention before the user commits time or money.
- Make cancel, retry, delete, and expiry behavior explicit.
- Distinguish upload progress, queue wait, engine processing, validation, and download readiness.
- Do not use a single indefinite spinner for a long conversion.
- Preserve a job URL so a signed-in user can safely leave and return.
- Use polling initially if it is simpler and reliable; add Server-Sent Events or WebSockets only behind the same typed progress abstraction.

## Work the STUDIO AVIV project can safely do now

The website team can implement the interface before hosted workers exist if it follows these constraints:

- Add a disabled-by-default feature flag such as `PACKAGE_BUILDER_CONVERTER_ENABLED=false`.
- Define a provider interface such as `PackageBuilderClient` and a strictly labelled mock implementation for Storybook/tests/local UI development.
- Keep API types generated from or checked against versioned Package Builder OpenAPI/JSON Schema when those contracts are delivered.
- Build the route, step layout, responsive states, accessibility behavior, file-selection UX, review screens, progress timeline, findings table, artifact list, deletion confirmation, and unavailable-capability states.
- Use synthetic public-safe fixtures only. Do not upload real customer assets to an unapproved backend.
- Ensure mock mode has a permanent visual/test marker and cannot be enabled in production accidentally.
- Do not build an independent converter, naming engine, rig detector, material classifier, or package composer in TypeScript. Those rules belong to Package Builder.

Suggested TypeScript boundary:

```ts
export interface PackageBuilderClient {
  getCapabilities(signal?: AbortSignal): Promise<ConverterCapabilities>;
  createJob(request: CreateConversionJobRequest, idempotencyKey: string): Promise<ConversionJob>;
  authorizeUpload(jobId: string, file: UploadFileDescriptor): Promise<UploadAuthorization>;
  completeUpload(jobId: string, uploadId: string, checksum: string): Promise<ConversionJob>;
  getJob(jobId: string, signal?: AbortSignal): Promise<ConversionJob>;
  cancelJob(jobId: string): Promise<ConversionJob>;
  retryJob(jobId: string, idempotencyKey: string): Promise<ConversionJob>;
  authorizeDownload(jobId: string, artifactId: string): Promise<DownloadAuthorization>;
  deleteJob(jobId: string): Promise<void>;
}
```

This is an interface sketch, not the final wire contract. Keep it behind an adapter and replace it when PB-1902 publishes the authoritative contract.

## Proposed API surface for planning only

The final route names belong to PB-1902, but the website may plan around these operations:

- `GET /v1/converter/capabilities`
- `POST /v1/conversion-jobs`
- `POST /v1/conversion-jobs/{jobId}/uploads`
- `POST /v1/conversion-jobs/{jobId}/uploads/{uploadId}/complete`
- `GET /v1/conversion-jobs/{jobId}`
- `GET /v1/conversion-jobs/{jobId}/events`
- `POST /v1/conversion-jobs/{jobId}/review`
- `POST /v1/conversion-jobs/{jobId}/cancel`
- `POST /v1/conversion-jobs/{jobId}/retry`
- `POST /v1/conversion-jobs/{jobId}/artifacts/{artifactId}/download`
- `DELETE /v1/conversion-jobs/{jobId}`

Every mutation should accept or derive an idempotency key. Every resource operation must check authenticated ownership. Error responses should use stable finding/error codes and safe explanations, never internal exceptions or paths.

## Security, privacy, and abuse requirements

- Treat every uploaded byte and filename as hostile.
- Enforce compressed size, extracted size, expansion ratio, file count, nesting, canonical destination, extension, content, image dimension, path length, and total-job limits before engine execution.
- Reject traversal, rooted paths, alternate data streams, device names, reparse/symlink escapes, duplicate destinations, executable/script/plugin content, corrupt data, and unsupported formats according to versioned policy.
- Quarantine uploads and make them unavailable for public download.
- Do not execute source-provided Blender scripts, Unity packages/editor code, Unreal plugins, macros, binaries, or arbitrary commands.
- Separate tenants at the API, database, queue, storage-key, worker-workspace, cache, logs, and download layers.
- Encrypt transport and configured storage, rotate secrets, and use managed workload identity or equally scoped credentials where available.
- Redact tokens, cookies, signed URLs, customer filenames where unnecessary, private paths, and asset content from logs and analytics.
- Provide retention and deletion controls with verifiable cleanup of source, staging, cache references, outputs, and derived previews.
- Apply per-user and system-wide rate, storage, concurrency, duration, and cost limits before work is scheduled.
- Maintain abuse, incident, vulnerability, backup/restore, worker-outage, and data-deletion runbooks before launch.

## Engine licensing and operating-cost warning

The architecture is technically possible, but hosted Unity and Unreal conversion cannot be treated as merely deploying another JavaScript service.

- Blender hosting is the likely first online engine tier, subject to sandbox and capacity validation.
- Unity and Unreal need dedicated compatible workers, current authoritative licence/activation/automation review, and an approved operating model.
- The website must query server capabilities and hide or clearly disable unavailable targets.
- Never accept money for a target unless capacity, price, cancellation/refund behavior, and licensing are approved.
- Hosting engines, storage, bandwidth, malware scanning, databases, queues, observability, and downloads can cost money. These costs must not become a requirement for using the local Package Builder application.

The optional local-agent design can reduce hosted engine cost while preserving the website experience, but it needs strong enrollment, scoped credentials, capability reporting, command restrictions, updates, revocation, and user-consent UX.

## Cross-project source of truth

- Package Builder owns conversion behavior, schemas, worker protocol, target validation, and artifact semantics.
- STUDIO AVIV owns its storefront, customer identity/session integration, converter presentation, web accessibility, billing presentation if approved, and website deployment.
- The two projects integrate only through versioned contracts. Neither project imports the other's internal implementation assemblies or source folders.
- Contract compatibility tests and retained fixtures must run in both projects before enabling a new API version.
- Status pages and marketing copy may claim only capabilities returned by the production API and backed by passing release evidence.

## Launch acceptance checklist

- [ ] Hosted architecture, threat model, privacy, licensing, and cost boundaries are approved.
- [ ] Versioned API/event schemas and a typed website client are published and compatibility-tested.
- [ ] Authentication plus per-resource tenant authorization negative tests pass.
- [ ] Direct resumable upload, quarantine, integrity checks, and deletion pass.
- [ ] Durable job, lease, idempotency, cancellation, retry, and recovery tests pass.
- [ ] Every exposed engine worker passes isolation, hostile-input, resource-limit, and clean-output validation.
- [ ] Short-lived downloads, expiry, retention, deletion, quotas, rate limits, and cost controls pass.
- [ ] The converter page passes responsive, keyboard, screen-reader, focus, contrast, reduced-motion, and failure-recovery tests.
- [ ] Logs/metrics/traces are redacted and operational/incident runbooks are exercised.
- [ ] End-to-end tests prove upload, review, conversion, progress, findings, download, and deletion for every advertised target.
- [ ] Production feature flag remains off until explicit user launch approval.

## Instructions to the STUDIO AVIV ChatGPT project

Use this document as the integration source of truth. First inspect the STUDIO AVIV repository rules, current Next.js architecture, authentication, design system, storage choices, testing stack, deployment target, and existing routes. Then create a repository-specific plan and backlog for the website portion only.

Do not implement or claim a live converter unless approved backend contracts and endpoints exist. It is safe to implement a feature-flagged, mock-backed, accessible UI and typed adapter boundary now. Keep all decisions that require Package Builder API, worker infrastructure, engine licensing, pricing, retention, privacy, or production security explicitly blocked until evidence and approval are supplied.

Update STUDIO AVIV documentation whenever the integration design changes. Preserve separation of concerns, search for existing reusable components before adding new ones, avoid duplicated domain rules, keep functions/classes focused, and add tests at the correct layer for every behavior.
