# Validation evidence

- Xiaomi 10 run: commit `a2e45e4a9174efc144ea99eea6695fd592ec69cf`, source status `clean`, report `hud_rc_20260929_144534_928_8e236d8b.json`.
- Burst refresh stage medians: generation `9.2163 ms`, BuildMesh `7.9520 ms`, DoGenerate `6.2409 ms`, Simple direct path `5.1265 ms`, UGUI fallback `0 ms`, transform `1.2972 ms`, merge `1.0900 ms`, merge plan `0.1739 ms`, merge fill `0.6934 ms`, mesh upload `0.1867 ms`.
- Compared with the previous no-Sprite UGUI fallback run (`73d0ebb`): generation improved `9.6299 -> 9.2163 ms` (4.3%), BuildMesh `8.2695 -> 7.9520 ms` (3.8%), and DoGenerate `6.5659 -> 6.2409 ms` (4.9%). The eliminated UGUI fallback was replaced by a similarly sized direct write stage, so the next target is `UIMeshData.WriteSimpleQuad` / `UIMeshDataX.WriteSimpleQuad`.
