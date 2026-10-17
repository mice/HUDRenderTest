---
testId: UT_MESH_026
module: Mesh
testType: EditMode
target: UIMeshData
testFile: Tests/EditMode/TestUIMeshDataX.cs
testMethod: WriteSimpleQuad_WritesCanonicalSlimGeometry
status: active
lastRun: ""
lastResult: unknown
---

# UT_MESH_026

Validates the Slim mesh-data direct Simple Image quad contract.

Checks canonical geometry with quad profiling opted in and diagnostics enabled then disabled on the same instance. Stage timings are non-negative; RecordedWrites identifies a measured write even when timer resolution produces zero durations. Disabling diagnostics clears timings and the sample count. Both global switches are restored and the pooled mesh is disposed after the test.
