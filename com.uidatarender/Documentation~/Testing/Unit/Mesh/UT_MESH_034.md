---
testId: UT_MESH_034
module: Mesh
testType: EditMode
target: VertexHelperUtils
testFile: Tests/EditMode/TestVertexHelperUtils.cs
testMethod: StockCopyIndices_AfterWarmupDoesNotAllocateManagedMemory
status: active
lastRun: ""
lastResult: unknown
---

# UT_MESH_034

## Requirement
VERIFY-02: the forced stock index path produces zero managed allocation events after scratch warmup.

## Risk
Regression in VertexHelperUtils could break the documented Unity UIDataRender behavior.

## Steps
1. Run the EditMode test assembly containing `StockCopyIndices_AfterWarmupDoesNotAllocateManagedMemory`.
2. Let the NUnit/Unity Test Runner execute the assertions.

## Assertions
See `Tests/EditMode/TestVertexHelperUtils.cs` / `StockCopyIndices_AfterWarmupDoesNotAllocateManagedMemory`.

## Expected Result
The test passes without unexpected logs, leaks, or assertion failures.

## Latest Run Result
Passed on 2026-11-06 in both the custom Sample and actual BuiltIn UGUI 1.0.0 consumer: extraction 13/13 and complete EditMode 196/196 per environment, no failures or skips. See repository acceptance conclusions `Documentation~/verify-02-stock-ugui-results-2026-11-06.md`; raw XML/logs remain local. Frontmatter is maintained by the governance synchronizer.

## Notes
Warm 32 calls, measure 10,000 explicit static fallback calls with the existing calibrated GC.Alloc recorder, require its positive allocation control, assert zero measured events and exact output. Run in custom and stock dependency environments.

Explicit `VertexHelperUtils.CopyIndices` calls force the public-API fallback even when the installed UGUI has a custom instance method. The stock dependency's compiled assembly has no custom instance method; its recorded source hash matches the Editor's built-in package.
