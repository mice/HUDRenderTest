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
