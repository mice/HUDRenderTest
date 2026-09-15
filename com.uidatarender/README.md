# UI Data Render

`com.uidatarender` provides the reusable UGUI and TextMeshPro mesh extraction, batching, rendering, and prefab lifecycle components used by the HUD renderer.

## Layout

- `Runtime/`: runtime assemblies, UIE shaders, TMP resources, and managed-task support code.
- `Tests/EditMode/`: deterministic package tests.
- `Documentation~/`: package design and test-governance records.
- `Third Party Notices.md`: notices for bundled font and sprite resources.

The repository's `HUDRenderTestSample` project references this package through a local `file:` dependency and contains scenes, prefabs, sample drivers, and PlayMode integration tests.

## Dependencies

The package targets Unity 2022.3 and declares Burst 1.8.28, TextMesh Pro 3.0.6, and stock UGUI 1.0.0. Runtime code uses public UGUI APIs and does not require a modified or embedded UGUI package.

The package retains the TMP settings, default font/fallback chain, atlas, sprite resource, line-breaking data, and shaders required by clean-install runtime and test behavior. Sample-only TMP material presets belong to `HUDRenderTestSample`.

## Validation

From the repository root, use `.\Tools\Test-PackageLayout.ps1` for fast structural checks, `.\Tools\Invoke-LocalValidation.ps1` for the complete working-tree suite, and `.\Tools\Invoke-CleanValidation.ps1 -TestPlatform Both` for distributable-state validation without an existing Unity `Library`.
