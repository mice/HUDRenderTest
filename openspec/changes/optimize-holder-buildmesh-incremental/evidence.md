# Validation evidence

- Change: `optimize-holder-buildmesh-incremental`
- Validation date: `2026-09-29`
- EditMode: `135/135 passed`
- Package/layout validation: passed
- Unit-test governance: `161/161` records validated
- Cache correctness: stable plans hit the cache; holder order and texture-index changes invalidate it; cached local slot maps preserve ascending mapping.
- Device performance: not measured in this step. Editor validation does not represent Xiaomi 10 Release IL2CPP/ARM64/Vulkan performance.
- Xiaomi 10 run 1: commit `6d1d0cd5a6a9dd65c34c9aabe6bf468abcecb40f`, source status `clean`, report `Builds/HudRc/Xiaomi10/plan-cache-report/hud_rc_20260929_135635_762_59af75ff.json`. Partial median/p95 `0.66/1.88 ms`; Burst refresh median/p95 `7.50/11.79 ms`; generation p95 `10.05 ms`; merge p95 `1.75 ms`.
- Xiaomi 10 run 2, same APK/configuration: report `Builds/HudRc/Xiaomi10/plan-cache-report/hud_rc_20260929_135950_902_52343695.json`. Partial median/p95 `0.62/1.86 ms`; Burst refresh median/p95 `7.64/7.90 ms`; generation p95 `6.74 ms`; merge p95 `1.36 ms`.
- Decision: the cached planning/local slot maps preserve behavior but do not produce a reliable end-to-end improvement versus the prior `2a9d23e` baseline. Keep the low-risk cache, stop optimizing this layer, and focus the next change on repeated `DataPrefabHolder.BuildMesh`/`UIImage.DoGenerate` work.
