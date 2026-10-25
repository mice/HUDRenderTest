---
testId: UT_BATCH_010
module: Batching
testType: EditMode
target: Holder batching and scheduling
testFile: Tests/EditMode/TestHolderBatchRenderer.cs
testMethod: Plan_WarmAndColdPartitionsMatch_AndCollectionChangesInvalidate
status: active
lastRun: ""
lastResult: unknown
---

# UT_BATCH_010

## Requirement
Warm and cold planners produce identical multi-batch order, holder order, global slots and ascending local maps.

## Risk
Structural changes could reuse a stale partition.

## Steps
Compare two batches and their maps; independently change mesh count, holder identity and collection membership, then clear the input.

## Assertions
See the governed test method; no timing threshold is asserted.

## Expected Result
All output, state and lifetime assertions pass.

## Latest Run Result
2026-10-20: passed in full EditMode 147/147 and PlayMode 28/28 regression.

## Notes
SourceEvidence: openspec/changes/archive/2026-10-21-optimize-holder-buildmesh-incremental/evidence.md
