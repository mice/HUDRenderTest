---
testId: UT_MESH_029
module: Mesh
testType: EditMode
target: UIMeshData
testFile: Tests/EditMode/TestUIMeshDataX.cs
testMethod: SlimFill_PreservesColorSentinelAndOffsetGeometryForBothMethods
status: active
lastRun: ""
lastResult: unknown
---

# UT_MESH_029

## Requirement
Both Slim fill methods preserve the legacy all-zero color sentinel, nonzero channels, transforms, UV texture/flag channels and destination index rebasing.

## Risk
Replacing boxed equality could whiten a non-default color, change transparent-black behavior, mutate source storage or mishandle nonzero shared/destination offsets.

## Steps
1. Run the EditMode test assembly containing `SlimFill_PreservesColorSentinelAndOffsetGeometryForBothMethods`.
2. Let the NUnit/Unity Test Runner execute the assertions.

## Assertions
See `Tests/EditMode/TestUIMeshDataX.cs` / `SlimFill_PreservesColorSentinelAndOffsetGeometryForBothMethods`.

## Expected Result
The test passes without unexpected logs, leaks, or assertion failures.

## Latest Run Result
Passed in Unity 2022.3.53f1 on 2026-10-20. Full regression: 144/144 EditMode and 27/27 PlayMode. Evidence: openspec/changes/archive/2026-10-21-optimize-slim-fill-color-check/evidence/.

## Notes
Source: optimize-slim-fill-color-check. Covers all-zero, each single nonzero RGBA channel, opaque black, transparent colored, arbitrary RGBA and white. Both methods append to seeded lists from a nonzero shared slice; the matrix includes translation, rotation and scale. The legacy Equals expression remains only in this test's expected-value oracle.
