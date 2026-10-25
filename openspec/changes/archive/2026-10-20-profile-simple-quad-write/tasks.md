## 1. Instrumentation

- [x] 1.1 Add diagnostics write-stage data and optional source interface.
- [x] 1.2 Instrument managed and Slim `WriteSimpleQuad` implementations; restore optional timestamp sampling and clear stale results.
- [x] 1.3 Export write substages through UIImage and HUD RC JSON/CSV with recorded counts, explicit availability, and captured diagnostic settings.

## 2. Verification

- [x] 2.1 Run EditMode/PlayMode/layout/governance validation, covering both mesh paths, allocation, diagnostic reset and report validity.
- [x] 2.2 Build and run Xiaomi 10, compare managed/Slim write stage shares, and record evidence.

## Current handoff — 2026-10-20

Local validation passed: EditMode 140/140, PlayMode 27/27, governance 167/167, layout and OpenSpec strict. See [evidence](evidence.md).

The device subsequently became available. Task 2.2 passed with eight Release IL2CPP ARM64/Vulkan runs (Managed/Slim × quad timing off/on, two reverse-order repeats), 14,400 validated frames and matching CSVs. All builds share one source fingerprint. See [device results](evidence/android-results.md) and the preserved raw reports.

Implementation is 5/5. Fine-grained timing substantially perturbs write cost, so retain its opt-in default. The subsequent BuildMesh full-stage/disabled-allocation checks passed (142/142 EditMode, 27/27 PlayMode); both profiling specs are synced and both changes archived. PERF-04 remains open. Next: investigate the measured Slim merge-fill difference.
