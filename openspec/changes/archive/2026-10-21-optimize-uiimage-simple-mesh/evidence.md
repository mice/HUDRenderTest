# Validation evidence

- Commit: `2a9d23e240f07e07fbb050cfec3bcbeba296d228`
- Device: Xiaomi 10 (`791d02af`), Release IL2CPP ARM64, Vulkan
- Source status: `clean`
- EditMode: 133/133 passed; package/layout validation passed
- Partial regeneration: median/p95 `0.6246/1.8557 ms`
- Burst refresh: median/p95 `7.4737/7.5471 ms`
- Burst generation p95: `6.412 ms`; merge p95: `1.1574 ms`
- Decision: keep Sprite/texture cache and empty slot-remap early return. The end-to-end change is within run-to-run noise compared with `12d05a0`; these are not the primary bottleneck.
- Next focus: reduce repeated `DataPrefabHolder.BuildMesh`/`UIImage.DoGenerate` work and investigate incremental holder/batch mesh updates.

## Closure audit — 2026-10-20

The original report above used no Sprite and therefore did not prove the Sprite-backed fast path. Task 4.2 is now completed with an explicit Sprite workload, rather than treating that historical run as direct-path evidence.

UT_UIIMAGE_008 now compares Sprite-backed Managed/Slim output against forced fallback under parent translation/rotation and child scale. UT_PERF_017 verifies the Sprite workload, actual path selection, configuration snapshot, global switch restoration and Sprite cleanup. Latest full regression: **147/147 EditMode, 28/28 PlayMode**, layout and **175/175 governance** passed; see [shared closure results](../2026-10-21-optimize-holder-buildmesh-incremental/evidence/closure-validation.json).

The Sample runner adds `spriteBacked` and `forceUguiFallback` (both default false), records prepared Sprite/direct-path counts, and creates its shared white FullRect Sprite before warming. The same runtime source supports both comparison APKs; only the temporary build scene configuration differs. Existing committed scene and PlayerSettings remain unchanged. Fine quad timestamps remain off. No new package optimization is introduced for this evidence run.


The [four-run Sprite comparison](evidence/android-results.md) validates 7,200 frames and actual direct/fallback path hits. Direct and fallback regeneration remain close; no reliable speedup is claimed. Preserve the equivalent reversible implementation and its diagnostic switch. Task 4.2 is now supported by an actual Sprite workload, all 9 tasks are complete, and overall PERF-04 remains unaccepted.
