## Context

The existing runner measures bounded Static/Partial/Burst work and bucket scheduling with immutable scalar snapshots. Historical defaults use null-Sprite Simple images. Android IL2CPP must never invoke GetAllocatedBytesForCurrentThread because this runtime can crash. Repository resources and the connected Mi 10 are available; user approved editor access and repository-derived resource loads.

## Goals / Non-Goals

**Goals:** opt-in three-texture Sprite plus primary-atlas TMP fixture; deterministic text and bar updates; fixed resource inventory; build/source/UGUI provenance; positive-controlled allocation diagnostics; repeated device measurements with honest remaining gates.

**Non-Goals:** package runtime optimization, fallback font/sprite assets, new budget exceptions, release/tag creation, full RC or coverage acceptance.

## Decisions

- Extend the existing partial runner and temporary build scene. Keep historical configuration defaults. The resource profile requires exactly three image draws plus a fixed-length ASCII TMP label. Precompute text variants before sampling; snapshot the variant index in bucket state so delayed updates reproduce the same content.
- Bind three immutable local image slots after each holder generation through UpdateTextureIndex and MaterialBinder; use the existing mixed SDF shader and primary font material. Avoid global registry mutations. Build tooling loads actual repository Sprite/font assets and fingerprints their files before sampling.
- Use a bounded, opt-in GC.Alloc event recorder restricted to the current thread. Explicitly stop/reset/start around measured HUD work, count events, reject saturation and require a known allocation positive control. Byte values additionally require a Bytes unit and a >=4096 byte positive control: Unity marker duration values cannot represent allocated bytes. Release timing uses diagnostics disabled; a Development diagnostic build is separate evidence if release events are unavailable. Missing evidence remains unavailable.
- Report source fingerprint, build GUID/type, asset inventory, UGUI source hash and direct CopyIndices method presence. Capture after sampling; do not log/export inside sampled work.
- Use existing scripts and the original project. Back up the installed benchmark application before replacement and restore device settings/app after capture. Record before/after thermal and battery conditions and immutable hashes locally.

## Risks / Trade-offs

- TMP generation may allocate or exceed the earlier null-Sprite budget → report measured results without extending budgets or closing PERF-04 automatically.
- Profiler instrumentation changes timing → keep diagnostic runs separate from Release timing and label scope/availability.
- Inactive TMP generation or shader bindings could produce incomplete geometry → assert visible glyph/UV/slot/material behavior and convergence in Unity tests before device capture.
- Resource profile increases vertices → bound configuration against actual fixture glyph count and preserve the existing mesh limit.

## Migration Plan

Opt-in flags preserve existing scenes and historical fixtures. New fields are additive. Revert the new profile/diagnostic flags to return to historical sampling; no runtime package migration required.

## Open Questions

- Whether GC.Alloc is available in this device's Release/Development Player is resolved by calibration.
- Whole PERF-04 acceptance remains subject to current budgets and missing full RC/coverage gates.
