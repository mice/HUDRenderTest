## Why

Unity UI `Text` is a legacy path and the project needs a forward-compatible TextMeshPro adapter before later Unity upgrades remove or degrade support. The current HUD renderer depends on `Text.OnPopulateMesh` and a legacy font atlas shader path, so the migration must be isolated and specified before implementation.

## What Changes

- Add TextMeshPro as a project package dependency.
- Introduce a TextMeshPro rendering capability for the existing UI data rendering pipeline.
- Keep the current `UIText : Text` path available during migration; no legacy Text behavior is removed in this change.
- Define a low-level acceptance target where TMP output matches the current Text visual result for supported content.
- Define advanced acceptance targets for SDF-quality rendering and TMP outline support as staged work.
- Document known initial limits: single TMP font asset, single material/atlas, no fallback atlas or TMP sprite support in the first implementation stage.

## Capabilities

### New Capabilities
- `tmp-ui-rendering`: TextMeshPro UI elements can generate mesh data for the existing HUD batch renderer while preserving legacy Text compatibility.

### Modified Capabilities

None.

## Impact

- Packages: `Packages/manifest.json`, `Packages/packages-lock.json`, and TMP imported resources.
- Runtime: `Assets/Scripts/UIDataRender`, especially text draw targets, mesh data extraction, shader/material binding, and prefab registration.
- Tests: EditMode and PlayMode coverage for TMP mesh generation, text mutation, transform handling, and batch rendering.
- Assets/scenes: staged prefab migration; existing legacy Text scenes remain valid.
