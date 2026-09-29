# Validation evidence

- EditMode: `137/137` passed via `Tools/Invoke-LocalValidation.ps1 -SkipPlayMode`.
- Package/layout validation: passed. `openspec validate --all`: 12/12 items passed.
- Xiaomi 10 (`791d02af`), Release IL2CPP ARM64 Vulkan APK, source status `clean`, commit `d6bdecb00cc3041520ddad31b014087d171259e5`, report `Builds/HudRc/Xiaomi10/null-texture-report/hud_rc_20260929_154141_590_6ff09f5d.json`.
- Burst refresh stage medians: generation `8.4972 ms`, BuildMesh `7.3626 ms`, DoGenerate `5.6793 ms`, Simple path `4.8352 ms`, UGUI fallback `0 ms`, Transform `1.1600 ms`, merge `1.1793 ms`.
- Compared with the previous null-Sprite direct-quad run (`a2e45e4`, generation `9.2163 ms`, BuildMesh `7.9520 ms`), generation decreased about `7.8%` and BuildMesh about `7.4%`. The result supports the stable null texture cache as a low-risk improvement; BuildMesh and direct quad writing remain the dominant stages.
- The report was collected with the existing fixed runner configuration (`256` holders, `3` quads, `6` Burst refresh samples). IL2CPP scoped allocation counters remain unavailable by design and are not used to claim zero allocation.
