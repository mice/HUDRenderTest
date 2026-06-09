---
testId: UT_SCENE_004
module: Scenes
testType: PlayMode
target: TMP batch rendering
testFile: Assets/Tests/PlayMode/TestTMPBatchRendering.cs
testMethod: TMPText_Rebuild_ProducesBatchMesh
status: active
lastRun: ""
lastResult: unknown
---

# UT_SCENE_004

## Requirement
TMP text renders through the HUD batch renderer and produces a drawable mesh.

## Risk
TMP mesh generation could pass EditMode extraction tests but fail in the runtime batch renderer path.

## Steps
1. Create a Canvas, UIPrefabOwner, and UITMPText at runtime.
2. Generate holder data and rebuild through HolderBatchRenderer.

## Assertions
The test verifies a batch mesh exists and contains vertices.

## Expected Result
The test passes without unexpected logs, leaks, or assertion failures.

## Latest Run Result
Unknown until the next Unity Test Runner execution.

## Notes
Created for TextMeshPro adapter PlayMode coverage.
