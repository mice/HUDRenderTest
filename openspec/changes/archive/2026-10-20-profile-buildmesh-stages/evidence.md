# Validation evidence

## Closure — 2026-10-20

- Reopened task 3.1 after the evidence audit, then added governed `UT_PREF_020` (exact aggregation, nested monotonic bounds, cold/warm cache, mixed targets and reset) and `UT_PREF_021` (calibrated zero managed allocation across 256 warmed disabled-diagnostic calls and output equivalence). Both cover Managed/Slim, sprite-backed and null-Sprite direct generation alongside Filled fallback.
- `Tools/Invoke-LocalValidation.ps1` passed: **142/142 EditMode**, **27/27 PlayMode**, no skips; package layout passed. Governance synchronization and validation passed **169 tests / 169 records**, preserving earlier records' run history.
- [EditMode XML](evidence/editmode-results-20261020.xml), [PlayMode XML](evidence/playmode-results-20261020.xml), [validation/source hashes](evidence/closure-validation.json).
- [Current Xiaomi 10 comparison](../2026-10-20-profile-simple-quad-write/evidence/android-results.md) preserves eight Release IL2CPP ARM64/Vulkan runs and raw JSON/CSV. The additional [stage-accounting audit](evidence/stage-accounting-audit.json), reproducible with [this script](verify-stage-accounting.py), passed all **14,400 frames / 4,960 refresh frames**. All **137 Runtime/runner source hashes** still match the device build snapshot; this closure changed tests/specs/docs only, so no replacement APK is claimed.
- Corrected the stale null-Sprite fallback specification to reflect the implemented direct path. Documented nested stages explicitly: Transform belongs inside Simple/fallback, sprite lookup inside drawing dimensions. The v2 unavailable-quad contract is owned by `simple-quad-write-profiling`.
- Both profiling changes are complete. Keep fine-grained quad timing off for baselines. Next investigate Slim merge-fill: Partial medians **2.028–2.059 ms** versus Managed **0.466–0.468 ms**, with similar generation/write costs. Full PERF-04 acceptance, clean candidate, coverage and Jenkins remain open.
- Main specs were synchronized and both changes archived separately. Final checks passed: OpenSpec strict **12/12** (4 active changes + 8 specs), layout, governance **169/169**, **81** local Markdown links, archive metadata/task integrity, preserved Android artifact hashes, closure source hashes and `git diff --check`. Original scenes/ProjectSettings and the user's existing manifest change were preserved.

## Historical measurement — 2026-09-29

The following snapshot predates null-Sprite direct generation and must not be read as the current next-task decision.

- Xiaomi 10 stage run: commit `73d0ebbf9f6346d908893d5b88f5f75fee9c1764`, source status `clean`, report `hud_rc_20260929_143324_438_a5b1c21f.json`. Partial regeneration median/p95 `0.73/2.20 ms`; Burst refresh median/p95 `10.73/20.29 ms`; generation p95 `19.56 ms`; merge p95 `1.11 ms`.
- Burst refresh stage medians: generation `9.6299 ms`, holder selection `1.1603 ms`, BuildMesh `8.2695 ms`, setup `0.1346 ms`, mesh-data initialization `0.3490 ms`, dispatch/loop overhead `0.7864 ms`, DoGenerate `6.5659 ms`, UGUI fallback `5.3727 ms`, transform `1.2709 ms`, merge plan `0.1758 ms`, merge fill `0.7007 ms`, mesh upload `0.1881 ms`.
- The runner's three `UIImage` targets have no Sprite, so `SimplePathMs` is zero and the measured generation uses the UGUI fallback. The next investigation is an equivalence test for a null-Sprite standard Image quad before enabling a direct application-layer path.
