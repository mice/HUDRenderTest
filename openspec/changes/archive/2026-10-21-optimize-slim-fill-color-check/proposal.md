## Why

The preserved Xiaomi 10 comparison shows Slim Partial merge-fill medians of 2.028–2.059 ms versus Managed 0.466–0.468 ms. Generated IL2CPP code confirms that Slim's per-vertex `Color32.Equals(default(Color32))` boxes the default color and invokes ValueType equality in both fill methods.

## What Changes

- Replace the boxed default-color test with an explicit byte-channel check in Slim output filling.
- Preserve the existing all-zero color sentinel, geometry, texture/flag UVs, transforms and index rebasing.
- Add focused output/allocation regressions and repeat the existing device workload with old/new Slim APKs.

## Capabilities

### New Capabilities

- `slim-fill-color-check`: Allocation-free color fallback during warmed Slim output filling without changing the existing output contract.

### Modified Capabilities

None.

## Impact

`UIMeshDataX.FillToDrawData`, `FillWithMatrix`, package EditMode tests and performance evidence. No API, dependency, scene or shader changes. List growth and whole-application/device allocation guarantees are outside this change.
