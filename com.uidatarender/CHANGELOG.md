# Changelog

## 0.1.2 — Unreleased

Package: `com.uidatarender`. Intended Git tag: `v0.1.2`; no local release tag has been created. Remote publication, release commit, and release date remain unverified.

### Features

- Add experimental shared-Quad damage-text instancing, visual/fixed/realtime comparison, separate draw/lifecycle stages, preregistered same-APK capture, JSON/CSV reports and independent validity checks. Mi 10 Release/IL2CPP/ARM64/Vulkan completed 13 ABBA groups, 52 runs and 187,450 sampled frames. Continuous-lifecycle outer Update CPU favored A in this workload; GPU/full-frame gains remain unverified. The change is stage-archived at 21/22 tasks, with GPU acceptance carried to DAMAGE-03. See [final conclusions](../openspec/changes/archive/2026-10-28-compare-damage-text-renderers/archive-record.md).

- Add opt-in local-XY Float16 storage, queried 68/64 B layouts and preparation-only capability/precision fallback. Device precision passed 32 cases/640 points: 24 FP16 cases and 8 correct Float32 fallbacks. FP16 reduced theoretical vertex capacity by 5.9% but increased lifecycle outer Update CPU by about 0.0327 ms; Float32 stays default. Art acceptance remains open.

- Add opt-in native Vulkan timestamp calibration and bounded capture with immutable commands, direct frame binding and query drain checks. Calibration passed all 1,680 required frames; a separate 3,600-frame diagnostic verified the capture path. Ten visual groups/40 images matched with instrumentation off/on. Formal instrumentation-impact and GPU comparison runs remain deferred because device thermal status was 3. Local regression passed 190 EditMode, 61 D3D11 PlayMode, 251 governed records and 90.0% UIDataRender coverage. Raw evidence and build intermediates remain local; Jenkins is outside this task's scope.

- Add opt-in fixed-atlas damage text with immutable 2–8 digit events, three styles, pixel-space GPU animation, persistent ordered mesh buckets, atomic publication/recovery and deterministic Clip playback. The Sample includes playback controls, three fixed recordings and traceable functional reports. First-phase validation passed 185 EditMode, 39 D3D11 PlayMode, 224 governed records and 89.4% UIDataRender line coverage. See the [playback contract](Documentation~/DamageTextPlayback.md) and archived `2026-10-26-add-damage-text-playback` specifications. Business ECS remains not-integrated.

- Add explicit holder snapshot queues and opt-in 2×128 two-frame rotation, atomic front/back publication, resource barriers and ordered Built-in camera draws. Local validation: 161 EditMode and 31 graphical PlayMode tests passed. Six matched Mi 10 runs show rotating update p95 1.3555/1.3413 ms versus 2.5087/2.4424 ms for two same-frame buckets; requests complete within one additional frame and converge to identical geometry. The local 1.5 ms p95 goal passes; a 5.3688 ms peak, slightly higher two-frame total work and unavailable Android allocation counters remain explicit. See [bucket evidence](../openspec/changes/archive/2026-10-25-add-holder-bucket-scheduling/evidence/bucket-results.md).

- Add opt-in `UIImage.BeginGenerationBatch(root)` scopes to reuse Canvas/ancestor/root-matrix queries during stable-hierarchy batches while reading image inputs and child transforms on each generation. The Sample can disable per-holder stage instrumentation independently of whole-update timers. Local regression: 150 EditMode and 29 PlayMode tests passed. Four Mi 10 comparison runs show 128-holder full-refresh p95 1.2971/1.2310 ms for query reuse versus 1.9776/1.9853 ms for live queries with identical diagnostic settings; the scoped 1.5 ms p95 target is met, while broader RC acceptance remains open. Burst/SIMD generation is deferred, not implemented.

- Establish the package version `0.1.2` and its roadmap/changelog release traceability. The canonical active roadmap and TODO are `Documentation~/roadmap.md` and `Documentation~/todo.md`; release requirements are specified in `openspec/specs/package-release-governance/spec.md` at the repository root. The governance change is archived under `openspec/changes/archive/2026-10-20-establish-package-governance`.

### Fixes

- Preserve bit-exact registered sample times across Unity JSON numeric roundtrips, reject coherent checksum/format/time corruption offline, defer final report allocation beyond sampled frames, and abort invalidated resources/configurations. Restore Sample lock-file inclusion required by clean validation and add `-WithGraphics` to the clean runner so graphical gates execute without weakening skip handling.

- Keep damage-text glyph orientation and upward motion consistent with GPU projection when rendering into textures; validate partial viewport visibility and expired resident glyph removal. The local validation script now supports `-WithGraphics` for actual D3D11 PlayMode acceptance.

- Close batch-cache geometry/cold-warm output coverage and Sprite-backed Managed/Slim equivalence; add explicit Sprite/fallback workload selection with configuration, path evidence and state/resource restoration. Regression: 147 EditMode and 28 PlayMode tests passed. Four device runs demonstrate equivalent Sprite paths without a reliable overall speedup. Four completed optimization changes and the unaccepted performance phase are archived under `openspec/changes/archive/2026-10-21-*`; PERF-04 remains open; subsequent bucket scheduling delivery is recorded above.

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
