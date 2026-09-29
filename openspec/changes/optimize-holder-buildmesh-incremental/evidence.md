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

## Stage decomposition

The comparison below uses the baseline report `2a9d23e` and the second cache run because both have the same Xiaomi 10 configuration and six full Burst refresh samples.

| Stage | Baseline median | Cache run median | Change | Interpretation |
|---|---:|---:|---:|---|
| Burst generation | 6.3532 ms | 6.4788 ms | +2.0% | Dominated by holder mesh generation. |
| `BuildMesh` | 5.0509 ms | 5.2091 ms | +3.1% | About 80% of generation; primary target. |
| Holder selection | 1.0598 ms | 1.0558 ms | -0.4% | Stable secondary cost. |
| Burst merge | 1.1322 ms | 1.1576 ms | +2.2% | Mostly `mergeFill`. |
| `mergePlan` | 0.2012 ms | 0.1833 ms | -8.9% | Small absolute change; cache benefit is below reliable device noise. |
| `mergeFill` | 0.6991 ms | 0.7303 ms | +4.5% | Main merge stage; not affected by plan cache. |
| Mesh upload | 0.1918 ms | 0.1987 ms | +3.6% | Stable engine-side cost. |

The next optimization should split and reduce repeated `DataPrefabHolder.BuildMesh` / `UIImage.DoGenerate` work. The 1 ms target is retained as a product budget, but stage share and repeatable percentage change are the acceptance signals for this investigation.
