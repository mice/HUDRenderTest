## 1. Eligibility and mesh write contract

- [x] 1.1 Define and test the explicit `UIImage` standard Simple Image eligibility predicate, including unsupported Filled, Sliced, Tiled, custom, and null-sprite cases.
- [x] 1.2 Add the minimal application-layer mesh-data write helper needed to emit canonical four-vertex/six-index Image geometry for both managed and Slim mesh data.

## 2. UIImage implementation

- [x] 2.1 Implement the direct `UIImage.DoGenerate` fast path using sprite rect/UV/pivot data and preserve texture index and parent-relative transform behavior.
- [x] 2.2 Keep the existing UGUI extraction path as the fallback and add separate fast-path/fallback profiler markers plus a diagnostic switch.

## 3. Correctness coverage

- [x] 3.1 Add governed EditMode tests comparing fast path and forced fallback positions, UVs, colors, indices, texture index, and vertex counts.
- [x] 3.2 Add governed transform and unsupported-mode tests proving fallback behavior and parent-relative equivalence.

## 4. Validation and evidence

- [x] 4.1 Run package/layout and EditMode validation, including test governance synchronization.
- [ ] 4.2 Build Release IL2CPP ARM64 APK, run the fixed Xiaomi 10 workload, and record fast-path hit markers and Partial/Burst median/p95 results.
- [ ] 4.3 Update `Documentation~/todo.md` and the OpenSpec evidence with the result, including rollback or fallback decision if the 1 ms budget remains unmet.
