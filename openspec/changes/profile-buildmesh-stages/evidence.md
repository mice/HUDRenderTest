# Validation evidence

- Xiaomi 10 stage run: commit `73d0ebbf9f6346d908893d5b88f5f75fee9c1764`, source status `clean`, report `hud_rc_20260929_143324_438_a5b1c21f.json`. Partial regeneration median/p95 `0.73/2.20 ms`; Burst refresh median/p95 `10.73/20.29 ms`; generation p95 `19.56 ms`; merge p95 `1.11 ms`.
- Burst refresh stage medians: generation `9.6299 ms`, holder selection `1.1603 ms`, BuildMesh `8.2695 ms`, setup `0.1346 ms`, mesh-data initialization `0.3490 ms`, dispatch/loop overhead `0.7864 ms`, DoGenerate `6.5659 ms`, UGUI fallback `5.3727 ms`, transform `1.2709 ms`, merge plan `0.1758 ms`, merge fill `0.7007 ms`, mesh upload `0.1881 ms`.
- The runner's three `UIImage` targets have no Sprite, so `SimplePathMs` is zero and the measured generation uses the UGUI fallback. The next investigation is an equivalence test for a null-Sprite standard Image quad before enabling a direct application-layer path.
