---
testId: UT_MESH_016
module: Mesh
testType: EditMode
target: VertexHelperUtils
testFile: Tests/EditMode/TestVertexHelperUtils.cs
testMethod: FillData2_PreservesIndexedTopologyAndVertexChannels
status: active
lastRun: ""
lastResult: unknown
---

# UT_MESH_016

## Requirement
VertexHelper extraction preserves indexed topology and all renderer-owned vertex channels.

## Risk
A portable UGUI extraction implementation could expand vertices, reorder triangles, or lose color and UV data.

## Steps
1. Build a four-vertex quad with distinct positions, colors, UVs, and indexed triangles.
2. Extract it through `FillData2` with non-zero texture and flag values.

## Assertions
The result keeps four vertices and six indices, preserves positions/colors/triangle order, and writes texture/flag metadata to every UV.

## Expected Result
The test passes for both the legacy modified-UGUI baseline and the stock-UGUI-compatible implementation.

## Latest Run Result
Unknown until the next Unity Test Runner execution.

## Notes
Added for the `complete-upm-migration` portable UGUI requirement.
