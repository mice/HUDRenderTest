## Why

VERIFY-02 identifies a distribution defect: the package declares stock UGUI 1.0.0 but extraction currently calls the repository's custom `VertexHelper.CopyIndices` instance method. Sample compilation with that local override cannot establish the declared dependency contract.

## What Changes

- Add a package-owned public-API index extraction fallback, preserving indexed topology, destination offsets, and allocation-free repeated extraction after warmup.
- Keep the existing custom UGUI instance method as an optional fast path through normal C# method resolution.
- Own and release fallback scratch resources across editor reloads and runtime sessions, with explicit cleanup available to consumers.
- Add deterministic fallback tests and a reproducible clean stock-UGUI validation path with dependency provenance. Keep existing performance results historical.
- Document the stock `FillMesh` vertex limit and the fallback's extra mesh round trip.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `upm-package-distribution`: require stock-compatible index extraction, optional custom acceleration, scratch lifecycle, and validation using an actual stock dependency resolution.

## Impact

Changes affect `VertexHelperUtils`, package EditMode tests and governance records, clean validation tooling, and package/current task documentation. Package dependencies and the original Sample dependency overrides remain as configured. No version bump or release is included. Unity validation requires permission under the repository's editor ownership rule.
