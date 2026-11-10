## Why

PERF-04 lacks representative Sprite/TMP/multiple-texture device evidence. The existing null-Sprite workload and unavailable IL2CPP allocation counters cannot establish acceptance for resource-backed HUDs.

## What Changes

- Add an opt-in deterministic fixture using three repository Sprite textures and primary-atlas TMP text, retaining historical defaults.
- Extend build configuration and reports with resource, build and UGUI provenance.
- Add a bounded allocation-event diagnostic with a positive control; report missing or saturated counters as unavailable.
- Validate the fixture and collect repeated Android timing/diagnostic evidence through existing tools. Retain open acceptance gates when evidence or budget approval is missing.

## Capabilities

### New Capabilities

### Modified Capabilities

- `hud-rc-performance-acceptance`: representative resource fixture, provenance and calibrated diagnostic evidence.

## Impact

Sample runner, temporary build scene configuration, Android validation tooling, governed Sample tests and PERF-04 conclusion documents. No package runtime API changes or expansion of TMP fallback/multimaterial support.
