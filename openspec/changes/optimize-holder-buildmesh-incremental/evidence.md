# Validation evidence

- Change: `optimize-holder-buildmesh-incremental`
- Validation date: `2026-09-29`
- EditMode: `135/135 passed`
- Package/layout validation: passed
- Unit-test governance: `161/161` records validated
- Cache correctness: stable plans hit the cache; holder order and texture-index changes invalidate it; cached local slot maps preserve ascending mapping.
- Device performance: not measured in this step. Editor validation does not represent Xiaomi 10 Release IL2CPP/ARM64/Vulkan performance.
- Next measurement: rebuild the Xiaomi 10 APK from the candidate commit and compare `mergePlanMs`, Burst generation/merge p95, and total refresh against `hud_rc_20260928_233614_483_4123bd91.json`.
