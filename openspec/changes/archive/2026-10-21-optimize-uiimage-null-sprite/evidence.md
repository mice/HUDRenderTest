# Validation evidence

- Xiaomi 10 run: commit `a2e45e4a9174efc144ea99eea6695fd592ec69cf`, source status `clean`, report `hud_rc_20260929_144534_928_8e236d8b.json`.
- Burst refresh stage medians: generation `9.2163 ms`, BuildMesh `7.9520 ms`, DoGenerate `6.2409 ms`, Simple direct path `5.1265 ms`, UGUI fallback `0 ms`, transform `1.2972 ms`, merge `1.0900 ms`, merge plan `0.1739 ms`, merge fill `0.6934 ms`, mesh upload `0.1867 ms`.
- Compared with the previous no-Sprite UGUI fallback run (`73d0ebb`): generation improved `9.6299 -> 9.2163 ms` (4.3%), BuildMesh `8.2695 -> 7.9520 ms` (3.8%), and DoGenerate `6.5659 -> 6.2409 ms` (4.9%). The eliminated UGUI fallback was replaced by a similarly sized direct write stage, so the next target is `UIMeshData.WriteSimpleQuad` / `UIMeshDataX.WriteSimpleQuad`.

## Closure validation — 2026-10-20

UT_UIIMAGE_007 now verifies null-Sprite Managed/Slim full output equivalence with the forced UGUI fallback under a parent transform, including UVs, colors and indices. UT_UIIMAGE_005 retains the null texture-index contract. Full current regression is **147/147 EditMode, 28/28 PlayMode**, layout and **175/175 governance** passed; shared [closure results](../2026-10-21-optimize-holder-buildmesh-incremental/evidence/closure-validation.json) preserve the exact XML hashes. The original [device JSON](evidence/hud_rc_20260929_144534_928_8e236d8b.json) and [CSV](evidence/hud_rc_20260929_144534_928_8e236d8b.csv) remain historical evidence. All five tasks are supported; no additional overall-performance or release acceptance is implied.
