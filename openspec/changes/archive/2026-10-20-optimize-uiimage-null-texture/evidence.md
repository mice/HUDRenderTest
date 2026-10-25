# Validation evidence

- EditMode: `137/137` passed via `Tools/Invoke-LocalValidation.ps1 -SkipPlayMode`.
- Package/layout validation: passed. `openspec validate --all`: 12/12 items passed.
- Xiaomi 10 (`791d02af`), Release IL2CPP ARM64 Vulkan APK, source status `clean`, commit `d6bdecb00cc3041520ddad31b014087d171259e5`, [preserved raw report](evidence/hud_rc_20260929_154141_590_6ff09f5d.json).
- Burst refresh stage medians: generation `8.4972 ms`, BuildMesh `7.3626 ms`, DoGenerate `5.6793 ms`, Simple path `4.8352 ms`, UGUI fallback `0 ms`, Transform `1.1600 ms`, merge `1.1793 ms`.
- Compared with the previous null-Sprite direct-quad run (`a2e45e4`, generation `9.2163 ms`, BuildMesh `7.9520 ms`), observed generation decreased about `7.8%` and BuildMesh about `7.4%`. The comparison interval also includes instrumentation changes, so this single run does not isolate the cache's performance benefit. The behavior-preserving cache is retained; BuildMesh and direct quad writing remain the dominant measured stages.
- The report was collected with the existing fixed runner configuration (`256` holders, `3` quads, `6` Burst refresh samples). IL2CPP scoped allocation counters remain unavailable by design and are not used to claim zero allocation.

## Archive review — 2026-10-20

- Copied the original JSON byte-for-byte from `Builds/HudRc/Xiaomi10/null-texture-report/hud_rc_20260929_154141_590_6ff09f5d.json` into this change. SHA256: `086BC96CAA00D69D06DAD9C0A6FD463DD56BDE2CA6D42725C45849ADF9555719`.
- Actual report configuration is `useSlim=false`, `targetFrameRate=-1`. This is Managed stage evidence, not fixed-60-FPS dual-path release acceptance.
- The five quad write substage fields are unpopulated zeros after the switch to profiler markers. Their validity and Managed/Slim comparison remain open in `profile-simple-quad-write`; these zeros are not evidence of zero cost.
- The EditMode and device results above are historical. This archive review changes documentation/specifications only and does not claim a fresh Unity run or RC performance acceptance.
- Implementation and null-to-Sprite behavior are accepted for this bounded change. Overall performance, allocation-counter validity, dependency compatibility, and exact-candidate CI remain in PERF-04, VERIFY-01/02, and GOV-004.
