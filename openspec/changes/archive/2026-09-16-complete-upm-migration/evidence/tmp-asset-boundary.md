# TMP Asset Boundary Evidence

Captured on 2026-09-15 while separating reusable package resources from Sample-only content.

## Inventory Method

- Enumerated every file and `.meta` under `com.uidatarender/Runtime/TextMesh Pro`.
- Extracted asset GUIDs from the metadata and searched package and Sample serialized files for consumers.
- Searched runtime and test code for `TMP_Settings`, `Resources.Load`, and `Shader.Find` usage.
- Tested proposed removals in a disposable clean snapshot before changing the working tree.

The initial import contained 74 files totaling 3,580,966 bytes. The retained set contains 35 files totaling 2,796,610 bytes, a reduction of 784,356 bytes (21.9 percent).

## Package-Owned Runtime Assets

| Asset | GUID or lookup | Reason retained |
|---|---|---|
| `Resources/TMP Settings.asset` | `3f5b5dff67a942289a9defa416b206f3`; TMP `Resources` lookup | Defines the clean-install default font, sprite, style sheet, and line-breaking data. |
| `Resources/Fonts & Materials/LiberationSans SDF.asset` | `8f586378b4e144a9851e7b34d9b748ee` | Default font/material/atlas used by package tests and Sample scenes. |
| `Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset` | `2e498d1c8094910479dc3e1b768306a4` | Referenced by the default font fallback table. |
| `Fonts/LiberationSans.ttf` | `e3265ab4bf004d28a9537516768c1c75` | Serialized dynamic source for the fallback font; retained to preserve fallback glyph generation. |
| `Resources/Style Sheets/Default Style Sheet.asset` | `f952c082cb03451daed3ee968ac6c63e` | Referenced by TMP Settings. |
| `Resources/Sprite Assets/EmojiOne.asset` and `Sprites/EmojiOne.png` | `c41005c129ba4d66911b75229fd70b45`, `dffef66376be4fa480fb02b19edbe903` | Default sprite asset and its atlas, referenced by TMP Settings. |
| Leading/following line-breaking text assets | `d82c1b31c7e74239bff1220585707d2b`, `fade42e8bc714b018fac513c043d323b` | Referenced by TMP Settings. |
| `TMP_SDF.shader`, `TMP_SDF-Mobile.shader`, `TMP_Sprite.shader` and required includes | Shader names and serialized material GUIDs | Required for standard SDF tests, the default font material, and the default sprite material. |
| `Third Party Notices.md` | Package documentation | Preserves the Liberation Sans license and EmojiOne attribution outside Runtime. |

The declared `com.unity.textmeshpro` 3.0.6 dependency contains TMP Essentials as a `.unitypackage`, not as automatically available runtime shader assets. Removing all bundled TMP shaders caused `Shader.Find("TextMeshPro/Distance Field")` to return null in a clean snapshot. The three used shaders are therefore package requirements rather than redundant dependency files.

## Sample-Owned Assets

- `LiberationSans SDF - Outline.mat` (`79459efec17a4d00a321bdcc27bbc385`) moved to `HUDRenderTestSample/Assets/Art/Material`; referenced by `TestUITextMeshProPrefab.unity`.
- `LiberationSans SDF - Outline2.mat` (`a0b833dc542b72443b7052c5c6597fe6`) moved to `HUDRenderTestSample/Assets/Art/Material`; referenced by `TestUITextColor.unity`.
- Both materials retained their original `.meta` files and continue to reference the package-owned default font atlas.

## Removed Assets

- Legacy TMP user-guide PDF and its folder metadata.
- EmojiOne source JSON and the Runtime copy of attribution text.
- Unreferenced Drop Shadow material preset.
- Unused Bitmap, Overlay, SSD, Masking, and Surface shader variants and their unused include files.

## Verification

- Disposable clean-snapshot candidate: EditMode 118/118 and PlayMode 22/22 passed with no skipped tests.
- Actual working tree after the same boundary change: EditMode 118/118 and PlayMode 22/22 passed with no skipped tests.
- `UT_UI_003` through `UT_UI_006` cover TMP generation, relative transforms, text updates, and SDF material binding.
- `UT_SCENE_004` covers TMP text rebuilding into a non-empty batch mesh.
- Package Runtime contains no `HUDRenderTestSample` reference.
- Package/Sample asset scan reports complete `.meta` coverage and no duplicate GUIDs.
