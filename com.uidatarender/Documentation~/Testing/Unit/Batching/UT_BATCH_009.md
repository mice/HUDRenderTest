---
testId: UT_BATCH_009
module: Batching
testType: EditMode
target: MergeBatcher
testFile: Tests/EditMode/TestHolderBatchRenderer.cs
testMethod: Rebuild_GeometryOnlyChangesReusePlan_AndMatchColdOutput
status: active
lastRun: ""
lastResult: unknown
---

# UT_BATCH_009

## Requirement
Geometry-only changes preserve plan/local-map reuse while Managed and Slim warm rendering matches a fresh renderer.

## Risk
Stale geometry or remapping could survive a cache hit.

## Steps
Mutate positions, UVs, colors, vertex transform and holder position; compare vertices, UVs, colors, indices and bounds after warm/cold rebuilds.

## Assertions
See the governed test method; no timing threshold is asserted.

## Expected Result
All output, state and lifetime assertions pass.

## Latest Run Result
2026-10-20: passed in full EditMode 147/147 and PlayMode 28/28 regression.

## Notes
SourceEvidence: openspec/changes/archive/2026-10-21-optimize-holder-buildmesh-incremental/evidence.md
