# Slim fill color comparison evidence

## Cause and bounded change

The old `UIMeshDataX.FillToDrawData` and `FillWithMatrix` called
`Color32.Equals(default(Color32))` once per vertex. The Android IL2CPP output
contains `Box(Color32...)` and `ValueType_Equals` in both functions; see
[baseline generated code](evidence/baseline-il2cpp-fill.txt).
Geometry storage is ordinary managed arrays. Rebuilding a batch fills all its
meshes even during Partial updates, multiplying this per-vertex cost.

Both methods now use a shared four-byte zero check. All-zero RGBA still maps
to opaque white; every other color, including transparent colored values and
opaque black, remains unchanged. Array access, transforms, UVs, index rebasing
and destination List operations are unchanged. The [fixed generated
code](evidence/fixed-il2cpp-fill.txt) contains no `Box` or `ValueType_Equals`
in either fill function or its color helper.

## Correctness and allocation regression

- `UT_MESH_029` covers both fill methods, all-zero and individual nonzero
  channels, nonzero shared geometry offsets, seeded output lists, UV channels,
  destination index rebasing, translation and a nontrivial matrix.
- `UT_MESH_030` warms each method, preallocates all output lists, and positively
  calibrates a current-thread GC.Alloc recorder. Across 256 calls per method,
  the old code produces 2,048 allocation events for each method; the fixed code
  produces zero observed events. This is a bounded Editor allocation result.
- The intentional baseline failure is preserved in
  [baseline-editmode.xml](evidence/baseline-editmode.xml).
  Fixed regression: [EditMode 144/144](evidence/editmode-results.xml),
  [PlayMode 27/27](evidence/playmode-results.xml), package layout passed and
  test governance 171/171 passed. Source and result hashes are in
  [local-validation.json](evidence/local-validation.json).

## Device comparison and scope

The old/new/new/old comparison uses the same Mi 10 and Release IL2CPP ARM64
Vulkan workload: 256 holders × 3 quads, Partial 10%, Burst every 30 frames,
seed 12345, 60 warm-up and 600 sampled frames per mode, target 60 FPS,
Slim enabled and fine quad-write timestamps disabled.

The baseline APK is byte-identical to the prior `quad-v2-slim-off.apk`.
[Build provenance](evidence/android-builds.json) preserves both APK hashes,
source manifests and all source differences. The only changed runtime file is
`UIMeshDataX.cs`; Sample runtime, scene/settings and dependencies match.
Other manifest differences concern tests, test records and changelog.

Both builds use a dirty working tree based on `3a6f553`; these results do not
constitute a release candidate, whole-scene zero-GC proof, coverage result,
clean consumer validation or Jenkins acceptance.

The four-run comparison is complete: [device results](evidence/android-results.md),
[summary](evidence/android-summary.json) and [raw reports](evidence/android-reports.zip).
All 7,200 frames and paired CSVs passed configuration, workload and provenance
checks. Partial merge-fill median fell from 1.7185–2.0383 ms to
0.2277–0.2293 ms; regeneration fell by 56.1–59.7%. Burst-refresh regeneration
fell by 23.3–26.6%. Retain the fix; PERF-04 remains open. Next work returns to
cached-plan output-equivalence coverage and UIImage generation.
