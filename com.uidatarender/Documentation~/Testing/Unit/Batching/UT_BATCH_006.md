---
testId: UT_BATCH_006
category: Batching
testFile: Tests/EditMode/TestMergeBatcher.cs
testMethod: GetBatchTextures_ReusesBufferAndPreservesSlotOrder
testType: EditMode
---

# UT_BATCH_006

`RenderBatch.GetBatchTextures` reuses its result buffer and keeps global texture slot order.

## Verification

Run the EditMode test assembly containing `GetBatchTextures_ReusesBufferAndPreservesSlotOrder`.
