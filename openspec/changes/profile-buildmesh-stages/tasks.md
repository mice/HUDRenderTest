## 1. Stage instrumentation

- [x] 1.1 Add a reusable allocation-free `BuildMesh` stage timing result and opt-in diagnostics switch.
- [x] 1.2 Instrument setup/resize, mesh-data initialization, draw-target dispatch, and `DoGenerate`/transform stage totals in `DataPrefabHolder<T>`.
- [x] 1.3 Add aggregate stage fields to `HudRcPerformanceRunner` frame samples, CSV, and JSON summaries while preserving existing fields.

## 2. UIImage direct path split

- [x] 2.1 Split eligible Simple Image generation into explicit sprite data, drawing dimensions, quad write, and transform helpers in `UIImage`.
- [x] 2.2 Keep complex Image configurations on the UGUI fallback and add equivalence tests for direct and fallback output.

## 3. Verification and evidence

- [x] 3.1 Add EditMode tests for stage accounting and zero-allocation diagnostics-disabled behavior.
- [x] 3.2 Run package/layout/EditMode validation and update governance records.
- [ ] 3.3 Build and run the Xiaomi 10 Release IL2CPP APK, compare stage medians/p95 and percentage shares, and record evidence plus the next optimization target.
