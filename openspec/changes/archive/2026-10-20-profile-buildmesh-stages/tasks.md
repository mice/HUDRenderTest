## 1. Stage instrumentation

- [x] 1.1 Add a reusable allocation-free `BuildMesh` stage timing result and opt-in diagnostics switch.
- [x] 1.2 Instrument setup/resize, mesh-data initialization, draw-target dispatch, and `DoGenerate`/transform stage totals in `DataPrefabHolder<T>`.
- [x] 1.3 Add aggregate stage fields to `HudRcPerformanceRunner` frame samples, CSV, and JSON summaries while preserving existing fields.

## 2. UIImage direct path split

- [x] 2.1 Split eligible Simple Image generation into explicit sprite data, drawing dimensions, quad write, and transform helpers in `UIImage`.
- [x] 2.2 Keep complex Image configurations on the UGUI fallback and add equivalence tests for direct and fallback output.

## 3. Verification and evidence

- [x] 3.1 Add EditMode tests for stage accounting and zero-allocation diagnostics-disabled behavior (UT_PREF_020/021 close the 2026-10-20 evidence audit).
- [x] 3.2 Run package/layout/EditMode validation and update governance records for the completed accounting/allocation coverage.
- [x] 3.3 Build and run the Xiaomi 10 Release IL2CPP APK, compare stage medians/p95 and percentage shares, and record evidence plus the next optimization target.

## Closure — 2026-10-20

8/8 complete. Current regression: EditMode 142/142, PlayMode 27/27, governance 169/169 and layout passed. All 14,400 preserved device frames satisfy stage containment; 137 Runtime/runner source hashes still match the sampled APK source manifest. See [evidence](evidence.md). Next optimization investigation: Slim merge-fill; PERF-04 remains open.
