## Why

PERF-04's representative Sprite/TMP workload records one allocation event per completed holder, while the historical Sprite fixture records none. TMP extraction constructs a VertexHelper on every call; removing that repeated allocation needs independently verified geometry and same-device measurement.

## What Changes

- Reuse main-thread TMP extraction scratch across calls without caching text geometry or bypassing TMP regeneration.
- Provide explicit, idempotent scratch release and runtime/editor cleanup, including safe nested extraction and exception recovery.
- Add governed output and allocation regressions, with positive-controlled allocation-event attribution before and after the fix.
- Compare preserved baseline and candidate APKs on Mi 10 using the existing representative fixture and report remaining PERF-04 gates honestly.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `tmp-ui-rendering`: Warm nonnested extraction reuses scratch without allocation events, preserves output across changing/empty content, and supports explicit safe cleanup.

## Impact

TMPMeshDataUtility, package EditMode tests and records, representative Sample allocation assertions, package documentation, and local Android evidence. Public Fill and UITMPText regeneration behavior remain compatible. No dependency, performance-budget, or release-acceptance changes.
