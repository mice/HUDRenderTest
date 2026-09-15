# TextMeshPro Adapter Notes

## Supported Initial Scope

- `UITMPText` can generate mesh data for the existing `IUIData` and `HolderBatchRenderer` path.
- The low-level path supports one TMP font asset and the primary TMP material atlas.
- `UIPrefabHolder.SetText` works through `IUITextDrawTarget` for both `UIText` and `UITMPText`.
- `Hidden/UIE-TMP-SDF` provides a project-owned SDF sampling path with face color and outline color/width copied from the TMP material.

## Unsupported Advanced Cases

- Fallback font atlases are not flattened into separate draw batches yet.
- TMP sprite assets are not included in the batch renderer path yet.
- Multi-material TMP text is not supported beyond the primary material reference.
- TMP underlay, glow, bevel, masking, and rich material effects are not reproduced by `Hidden/UIE-TMP-SDF`.
- Legacy `UIOutline` is not used for TMP text; outline support is material/shader driven.

## Migration Notes

Keep legacy `UIText` scenes and prefabs unchanged while adding TMP variants. Use `UITMPText` only where the TMP font asset and atlas are controlled by the prefab or sample scene. Add explicit validation before enabling fallback fonts, sprites, or rich text that changes material references.
