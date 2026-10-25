# Changelog

## 0.1.2 — Unreleased

Package: `com.uidatarender`. Intended Git tag: `v0.1.2`; no local release tag has been created. Remote publication, release commit, and release date remain unverified.

### Features

- Add opt-in `UIImage.BeginGenerationBatch(root)` scopes to reuse Canvas/ancestor/root-matrix queries during stable-hierarchy batches while reading image inputs and child transforms on each generation. The Sample can disable per-holder stage instrumentation independently of whole-update timers. Local regression: 150 EditMode and 29 PlayMode tests passed. Four Mi 10 comparison runs show 128-holder full-refresh p95 1.2971/1.2310 ms for query reuse versus 1.9776/1.9853 ms for live queries with identical diagnostic settings; the scoped 1.5 ms p95 target is met, while broader RC acceptance remains open. Burst/SIMD generation is deferred, not implemented.

- Establish the package version `0.1.2` and its roadmap/changelog release traceability. The canonical active roadmap and TODO are `Documentation~/roadmap.md` and `Documentation~/todo.md`; release requirements are specified in `openspec/specs/package-release-governance/spec.md` at the repository root. The governance change is archived under `openspec/changes/archive/2026-10-20-establish-package-governance`.

### Fixes

- Close batch-cache geometry/cold-warm output coverage and Sprite-backed Managed/Slim equivalence; add explicit Sprite/fallback workload selection with configuration, path evidence and state/resource restoration. Regression: 147 EditMode and 28 PlayMode tests passed. Four device runs demonstrate equivalent Sprite paths without a reliable overall speedup. Four completed optimization changes and the unaccepted performance phase are archived under `openspec/changes/archive/2026-10-21-*`; PERF-04 remains open and follow-up bucket scheduling is exploratory.

- Remove per-vertex Color32 boxing in Slim FillToDrawData/FillWithMatrix while preserving the all-zero color fallback and geometry output. Regression: 144 EditMode and 27 PlayMode tests passed. Four matched Mi 10 runs show Partial regeneration median reduced by 56.1–59.7% and Burst-refresh by 23.3–26.6%; overall performance/release acceptance remains open. Evidence: [Slim fill color check](../openspec/changes/archive/2026-10-21-optimize-slim-fill-color-check/evidence.md).

- Validate BuildMesh stage aggregation, nested accounting, stale-result clearing and warmed diagnostics-disabled allocation on Managed/Slim. The profiling specs are synchronized, and both profiling changes are archived under `openspec/changes/archive/2026-10-20-profile-buildmesh-stages` and `2026-10-20-profile-simple-quad-write`. Closure regression: 142 EditMode and 27 PlayMode tests passed.
- Restore opt-in Managed/Slim quad-write substage timing, clear stale diagnostic results, and distinguish unavailable samples from measured zero durations in HUD RC v2 reports. Local correctness/allocation/export validation and eight Xiaomi 10 comparison runs are recorded in `openspec/changes/archive/2026-10-20-profile-simple-quad-write/evidence.md`. Fine-grained timing materially increases measured write cost; overall performance and release acceptance remain open.
- Avoid repeated texture-version reads for stable null-Sprite Images and reuse the holder target Transform during mesh generation, preserving texture lookup when a Sprite is assigned later. Historical regression and device evidence: [null texture cache](../openspec/changes/archive/2026-10-20-optimize-uiimage-null-texture/evidence.md). Overall performance acceptance and candidate CI remain open; this is not an isolated performance-gain claim.

### Release rules

- Candidate versions use incrementing, non-reused `0.1.2-rc.N` versions and matching `v0.1.2-rc.N` tags.
- Each candidate must record its exact commit, validation evidence, and changelog entry; a failed candidate is followed by a new RC.
- Formal `v0.1.2` publication requires an accepted RC and explicit user confirmation. No tag or release is created by this governance update.

## Historical release status

The original 0.1.0 entry below is preserved. Its tag and publication status have not been verified; completing that mapping is part of the package governance change.

## 0.1.0

- Extracted the UI data renderer from the HUD sample project into `com.uidatarender`.
