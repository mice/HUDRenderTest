## Context

The prior generation experiment froze a Release custom UGUI APK and quantified probe overhead. Stock UGUI functional tests passed previously, but its reusable validation worktree is stale. The user requests stock scene evidence, scoped allocation evidence through Profiler, and precise RC/coverage follow-up without advancing the whole milestone.

## Goals / Non-Goals

**Goals:** establish real stock dependency behavior, functional equivalence, repeated device timing and a separately bounded stock Profiler allocation observation. Root reviews the output of at most three delegated agents: Terra for local tooling, Sol for protocol/source analysis, Luna for execution.

**Non-Goals:** production optimization, new runtime allocation counters, budget changes, new validation projects, milestone closure or release approval. This working-tree experiment does not establish exact RC validation.

## Decisions

1. Reuse the frozen custom APK only after auditing its complete input inventory against current sources; document-only differences are recorded. Refresh the existing owned stock worktree and restore byte-identical ordinary source/assets/settings. Only UGUI dependency entries and the Editor provenance helper may differ; preserve other tool dependencies. Compare actual resolved and compiled UGUI identity with APK and Player metadata. A forced fallback on custom UGUI is insufficient.
2. Reuse the existing Release build pipeline without production changes. Freeze APK SHA, build GUID, source fingerprint, source inventory and resolved VertexHelper SHA separately for each variant. Review expected whole-inventory differences rather than requiring equal aggregate fingerprints.
3. Register A(custom)/B(stock)/B/A/B/A/A/B before measurement. Both use all probes off, 256 holders, repository Sprite3/TMP6 resources, three textures, Slim/query reuse, Rotating, seed12345, 60 FPS, 60 warmup, 3000 sampled and two drain frames per mode: 9006 frames/run and 72048 total. Keep historical native landscape 2340x1080 and check actual device display plus every Unity Screen/camera sample. Compare per-run statistics and nearest-rank p95/p99; sum actual two-frame Burst windows before estimating their distribution.
4. Use separate Editor Mono Main Thread live Profiler windows with positive controls for scoped GC evidence. Reuse the proven 30-step capture, two lead callbacks, a single continuous run and 906 sampled/drain scopes. Preserve raw capture and live exports, tool/source/DLL identity and scope exclusions. Do not infer device IL2CPP, other threads or full-application bytes from this observation. Remove the temporary helper before build freezing.
5. Keep Unity validation, Profiler and builds outside device timing; never launch or close this task's Editor between formal device runs. Initial validation, builds and two rejected Profiler attempts precede timing; a completed bounded Profiler retry follows the entire device session and restoration, with another read-only restoration check afterward. Use the Unity SDK ADB client, separated stderr, at most three logged attempts for pure reads, and no automatic mutation retries. Save and restore the actual starting APK, request, nine settings and file inventory; stop the app afterward. Preserve any incomplete/rejected groups separately.

## Risks / Trade-offs

- Dependency resolution or stale workspace changes can confound results → fresh provenance, original-byte sync, per-file comparison and compiled/APK/Player cross-checks.
- Profiler overhead changes timing → keep it in a separate Editor diagnostic phase with explicit scope limitations.
- Thermal/order effects and host ADB interruption → fixed balanced order, per-run thermal/display checks, complete-group acceptance and restoration in finally.
- Reusing the custom binary includes older documentation bytes → retain original inventory and prove executable inputs unchanged; disclose identity differences.
- Stock fallback may allocate → report observed scoped samples/bytes without adding hot-path counters or converting an observation into an unsupported zero-allocation claim.

## Migration Plan

No production migration. All temporary capture helpers are removed and device state restored. Retain local evidence and publish only specifications/final conclusions; leave PERF-04/M5, target-device scoped bytes and exact RC gates open where evidence is absent.

## Open Questions

None blocking protocol registration. Actual performance and scoped allocations remain empirical outcomes.
