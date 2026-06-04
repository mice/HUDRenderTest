---
testId: UT_MESH_015
module: Mesh
testType: EditMode
target: Mesh.SetVertexBufferParams
testFile: Assets/Tests/EditMode/TestMeshVertexBufferLayout.cs
testMethod: OneStreamLayout_OrdersColorBeforeTexCoord0
status: active
lastRun: ""
lastResult: unknown
---

# UT_MESH_015

## Requirement
Verify Unity's actual one-stream vertex attribute offsets for the HUD mesh layout.

## Risk
If one-stream data is packed in C# field order while Unity reorders attributes internally, shader inputs can read shifted UV, color, and texture-slot data.

## Steps
1. Create a Mesh.
2. Set a one-stream layout with Position, TexCoord0, and Color descriptors.
3. Read vertex buffer stride and attribute offsets.

## Assertions
Position offset is 0, Color offset is 12, TexCoord0 offset is 16, and stride is 32.

## Expected Result
The test proves that Color is placed before TexCoord0 in a single stream.

## Latest Run Result

## Notes
This guards the diagnosis behind the TestGraphicsDataWithScaleOrRotation packed vertex issue.
