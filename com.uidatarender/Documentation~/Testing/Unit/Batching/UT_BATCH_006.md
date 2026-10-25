---
testId: UT_BATCH_006
module: Batching
testType: EditMode
target: Holder batching and scheduling
testFile: Tests/EditMode/TestMergeBatcher.cs
testMethod: GetBatchTextures_ReusesBufferAndPreservesSlotOrder
status: active
lastRun: ""
lastResult: unknown
---

# UT_BATCH_006

`RenderBatch.GetBatchTextures` reuses its result buffer and keeps global texture slot order.

## Verification

Run the EditMode test assembly containing `GetBatchTextures_ReusesBufferAndPreservesSlotOrder`.
